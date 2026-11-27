using System;
using System.Net.Http;

namespace WikiDataLib
{
    /// <summary>
    /// Thrown when Wikimedia answers HTTP 429 (Too Many Requests), or when a request is skipped because
    /// an earlier 429 started a cooldown. The library does not retry rate-limited requests.
    /// Derives from <see cref="HttpRequestException"/>, so existing <c>catch (HttpRequestException)</c> blocks still apply.
    /// </summary>
    public class WikiRateLimitException : HttpRequestException
    {
        public WikiRateLimitException(string message, TimeSpan retryAfter, Exception? innerException = null)
#if NET
            : base(message, innerException, System.Net.HttpStatusCode.TooManyRequests)
#else
            : base(message, innerException)
#endif
        {
            RetryAfter = retryAfter;
        }

        /// <summary>
        /// How long to wait before sending another request, from the server's <c>Retry-After</c> header
        /// (or a default when the header is missing).
        /// </summary>
        public TimeSpan RetryAfter { get; }
    }
}
