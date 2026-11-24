using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace WikiDataLib
{
    /// <summary>
    /// Shared HTTP transport for all Wikimedia requests: one pooled HttpClient with
    /// response compression, a descriptive User-Agent, retries, and a bounded TTL cache
    /// that also coalesces concurrent requests for the same URL.
    /// </summary>
    internal static class WikiHttp
    {
        private const int MaxRetryAttempts = 3;

        // Cache bounds. Entries are evicted least-recently-used once either limit is exceeded.
        internal const int MaxCacheEntries = 512;
        internal const long MaxCacheBytes = 64L * 1024 * 1024;
        internal static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

        // Wikimedia's User-Agent policy asks clients to identify themselves with contact info.
        // Generic browser user agents are more aggressively rate-limited.
        internal static readonly string UserAgent = BuildUserAgent();

        // volatile ensures reads/writes to the field are not reordered by the JIT.
        // SetHttpMessageHandlerForTesting is not thread-safe and must only be called
        // from single-threaded test setup/teardown — never from concurrent production code.
        private static volatile HttpClient _httpClient = CreateHttpClient(null);

        private static readonly ConcurrentDictionary<string, CacheEntry> _cache =
            new ConcurrentDictionary<string, CacheEntry>(StringComparer.Ordinal);

        private static long _cacheBytes;
        private static int _evicting;

        private sealed class CacheEntry
        {
            public CacheEntry(Task<FetchResult> task, DateTime expiresUtc)
            {
                Task = task;
                ExpiresUtc = expiresUtc;
                LastAccessTicks = DateTime.UtcNow.Ticks;
            }

            public Task<FetchResult> Task { get; }
            public DateTime ExpiresUtc { get; }
            public long LastAccessTicks;
            public long Bytes;
        }

        private readonly struct FetchResult
        {
            public FetchResult(JsonElement root, long bytes)
            {
                Root = root;
                Bytes = bytes;
            }

            public JsonElement Root { get; }
            public long Bytes { get; }
        }

        private static string BuildUserAgent()
        {
            var assembly = typeof(WikiHttp).Assembly;
            var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? assembly.GetName().Version?.ToString()
                ?? "0.0.0";

            // Drop SourceLink commit hash suffix ("1.3.1+abcdef") to keep the UA short.
            var plus = version.IndexOf('+');
            if (plus >= 0)
            {
                version = version.Substring(0, plus);
            }

            return $"WikiDataLib/{version} (+https://github.com/alexan1/WikiDataLib)";
        }

        private static HttpClient CreateHttpClient(HttpMessageHandler? handler)
        {
            HttpClient client;
            if (handler != null)
            {
                client = new HttpClient(handler, disposeHandler: false);
            }
            else
            {
#if NET
                var socketsHandler = new SocketsHttpHandler
                {
                    AutomaticDecompression = DecompressionMethods.All,
                    // Recycle connections periodically so DNS changes are picked up by the static client.
                    PooledConnectionLifetime = TimeSpan.FromMinutes(5)
                };
                client = new HttpClient(socketsHandler);
#else
                var clientHandler = new HttpClientHandler
                {
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
                };
                client = new HttpClient(clientHandler);
#endif
            }

            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
            return client;
        }

        // Replaces the HTTP handler used for all requests — intended for unit tests only.
        internal static void SetHttpMessageHandlerForTesting(HttpMessageHandler? handler)
        {
            _httpClient = CreateHttpClient(handler);
            ClearCache();
        }

        internal static void ClearCache()
        {
            _cache.Clear();
            Interlocked.Exchange(ref _cacheBytes, 0);
        }

        internal static int CacheCount => _cache.Count;

        /// <summary>
        /// Fetches and parses a JSON document, serving it from the cache when possible.
        /// Concurrent callers for the same URL share a single in-flight request.
        /// </summary>
        internal static async Task<JsonElement> GetJsonAsync(
            string url,
            CancellationToken cancellationToken,
            string? notFoundMessage = null)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var now = DateTime.UtcNow;
            CacheEntry entry;
            while (true)
            {
                entry = _cache.GetOrAdd(url, key => new CacheEntry(FetchAsync(key, notFoundMessage), now + CacheTtl));

                var expired = entry.ExpiresUtc <= now;
                var faulted = entry.Task.IsFaulted || entry.Task.IsCanceled;
                if (!expired && !faulted)
                {
                    break;
                }

                // Stale or failed entry: drop it (only if it is still the one we saw) and retry.
                TryRemove(url, entry);
            }

            Interlocked.Exchange(ref entry.LastAccessTicks, now.Ticks);

            FetchResult result;
            try
            {
                // The shared fetch is not bound to this caller's token, so one caller cancelling
                // does not cancel the request for others; this caller just stops waiting.
                result = await WaitWithCancellationAsync(entry.Task, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // Never keep failures cached.
                TryRemove(url, entry);
                throw;
            }

            // First caller to observe completion accounts for the entry's size.
            if (result.Bytes > 0 && Interlocked.CompareExchange(ref entry.Bytes, result.Bytes, 0) == 0)
            {
                Interlocked.Add(ref _cacheBytes, result.Bytes);

                // If the entry was evicted before its size was recorded, undo the accounting.
                if (!_cache.TryGetValue(url, out var current) || !ReferenceEquals(current, entry))
                {
                    Interlocked.Add(ref _cacheBytes, -result.Bytes);
                }
                else
                {
                    EvictIfNeeded();
                }
            }

            return result.Root;
        }

        private static void TryRemove(string url, CacheEntry entry)
        {
            // Remove only if the key still maps to this exact entry (atomic compare-and-remove).
            if (((ICollection<KeyValuePair<string, CacheEntry>>)_cache)
                .Remove(new KeyValuePair<string, CacheEntry>(url, entry)))
            {
                Interlocked.Add(ref _cacheBytes, -Interlocked.Read(ref entry.Bytes));
            }
        }

        private static void EvictIfNeeded()
        {
            if (_cache.Count <= MaxCacheEntries && Interlocked.Read(ref _cacheBytes) <= MaxCacheBytes)
            {
                return;
            }

            // One evictor at a time; others skip (the cache may briefly exceed its bounds).
            if (Interlocked.CompareExchange(ref _evicting, 1, 0) != 0)
            {
                return;
            }

            try
            {
                var now = DateTime.UtcNow;

                // Remove expired entries first, then least-recently-used until under 90% of both bounds.
                var snapshot = _cache.ToArray();
                foreach (var kv in snapshot.Where(kv => kv.Value.ExpiresUtc <= now))
                {
                    TryRemove(kv.Key, kv.Value);
                }

                var targetCount = MaxCacheEntries * 9 / 10;
                var targetBytes = MaxCacheBytes * 9 / 10;
                if (_cache.Count <= targetCount && Interlocked.Read(ref _cacheBytes) <= targetBytes)
                {
                    return;
                }

                foreach (var kv in _cache.ToArray()
                    .Where(kv => kv.Value.Task.IsCompleted)
                    .OrderBy(kv => Interlocked.Read(ref kv.Value.LastAccessTicks)))
                {
                    if (_cache.Count <= targetCount && Interlocked.Read(ref _cacheBytes) <= targetBytes)
                    {
                        break;
                    }

                    TryRemove(kv.Key, kv.Value);
                }
            }
            finally
            {
                Interlocked.Exchange(ref _evicting, 0);
            }
        }

        private static async Task<T> WaitWithCancellationAsync<T>(Task<T> task, CancellationToken cancellationToken)
        {
            if (task.IsCompleted || !cancellationToken.CanBeCanceled)
            {
                return await task.ConfigureAwait(false);
            }

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(state => ((TaskCompletionSource<bool>)state!).TrySetResult(true), tcs))
            {
                if (await Task.WhenAny(task, tcs.Task).ConfigureAwait(false) != task)
                {
                    throw new OperationCanceledException(cancellationToken);
                }
            }

            return await task.ConfigureAwait(false);
        }

        private static async Task<FetchResult> FetchAsync(string url, string? notFoundMessage)
        {
            // Yield so GetOrAdd's value factory returns immediately and never runs network I/O inline.
            await Task.Yield();

            for (var attempt = 1; attempt <= MaxRetryAttempts; attempt++)
            {
                HttpResponseMessage response;
                byte[]? bytes = null;
                try
                {
                    response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode)
                    {
                        // Read as UTF-8 bytes (no UTF-16 string copy); parsed below.
                        using (response)
                        {
                            bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                        }
                    }
                }
                catch (Exception ex) when (attempt < MaxRetryAttempts &&
                    (ex is HttpRequestException || ex is OperationCanceledException || ex is System.IO.IOException))
                {
                    // Transport error or timeout — retry with backoff.
                    await Task.Delay(GetBackoff(attempt)).ConfigureAwait(false);
                    continue;
                }

                if (bytes != null)
                {
                    using (var doc = JsonDocument.Parse(bytes))
                    {
                        return new FetchResult(doc.RootElement.Clone(), bytes.LongLength);
                    }
                }

                // Status handling sits outside the try so non-transient errors fail fast
                // instead of being caught and retried as if they were transport errors.
                using (response)
                {
                    var code = (int)response.StatusCode;
                    if (code == 404 && notFoundMessage != null)
                    {
                        throw new InvalidOperationException(notFoundMessage);
                    }

                    var isTransient = code == 429 || code >= 500;
                    if (isTransient && attempt < MaxRetryAttempts)
                    {
                        await Task.Delay(GetRetryDelay(response, attempt, code == 429)).ConfigureAwait(false);
                        continue;
                    }

                    response.EnsureSuccessStatusCode();
                }
            }

            throw new InvalidOperationException("Retry loop exhausted unexpectedly.");
        }

        private static TimeSpan GetRetryDelay(HttpResponseMessage response, int attempt, bool isRateLimited)
        {
            var retryAfter = response.Headers.RetryAfter;
            if (retryAfter != null)
            {
                TimeSpan? delay = retryAfter.Delta;
                if (!delay.HasValue && retryAfter.Date.HasValue)
                {
                    delay = retryAfter.Date.Value - DateTimeOffset.UtcNow;
                }

                if (delay.HasValue && delay.Value > TimeSpan.Zero)
                {
                    // Cap so a hostile/huge Retry-After cannot stall callers indefinitely.
                    return delay.Value < TimeSpan.FromSeconds(30) ? delay.Value : TimeSpan.FromSeconds(30);
                }
            }

            return GetBackoff(isRateLimited ? attempt + 1 : attempt);
        }

        private static readonly ThreadLocal<Random> _jitter =
            new ThreadLocal<Random>(() => new Random(Guid.NewGuid().GetHashCode()));

        // Exponential backoff (2^n seconds) with up to 25% jitter so clients do not retry in lockstep.
        private static TimeSpan GetBackoff(int exponent)
        {
            var baseMs = Math.Pow(2, exponent) * 1000;
            return TimeSpan.FromMilliseconds(baseMs + _jitter.Value!.NextDouble() * baseMs * 0.25);
        }
    }
}
