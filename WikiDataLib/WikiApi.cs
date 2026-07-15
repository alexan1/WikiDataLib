using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;

namespace WikiDataLib
{
    /// <summary>
    /// Provides access to Wikipedia REST API endpoints.
    /// </summary>
    public static class WikiApi
    {
        private const string WikiApiBaseUrl = "https://en.wikipedia.org/api/rest_v1";
        private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/79.0.3945.88 Safari/537.36";
        private const int MaxRetryAttempts = 3;

        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        private static readonly ConcurrentDictionary<string, JsonElement> _cache =
            new ConcurrentDictionary<string, JsonElement>();

        static WikiApi()
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        }

        /// <summary>
        /// Gets people born on a specific month and day from Wikipedia's "On this day" feed.
        /// </summary>
        public static Task<Collection<WikiPerson>> GetBornOnDateAsync(
            int month,
            int day,
            CancellationToken cancellationToken = default)
        {
            return GetPeopleOnThisDayAsync("births", month, day, null, cancellationToken);
        }

        /// <summary>
        /// Gets people born on a specific year, month, and day from Wikipedia's "On this day" feed.
        /// </summary>
        public static Task<Collection<WikiPerson>> GetBornOnDateAsync(
            int year,
            int month,
            int day,
            CancellationToken cancellationToken = default)
        {
            return GetPeopleOnThisDayAsync("births", month, day, year, cancellationToken);
        }

        /// <summary>
        /// Gets people who died on a specific month and day from Wikipedia's "On this day" feed.
        /// </summary>
        public static Task<Collection<WikiPerson>> GetDiedOnDateAsync(
            int month,
            int day,
            CancellationToken cancellationToken = default)
        {
            return GetPeopleOnThisDayAsync("deaths", month, day, null, cancellationToken);
        }

        /// <summary>
        /// Gets people who died on a specific year, month, and day from Wikipedia's "On this day" feed.
        /// </summary>
        public static Task<Collection<WikiPerson>> GetDiedOnDateAsync(
            int year,
            int month,
            int day,
            CancellationToken cancellationToken = default)
        {
            return GetPeopleOnThisDayAsync("deaths", month, day, year, cancellationToken);
        }

        /// <summary>
        /// Gets a person summary from Wikipedia by title.
        /// </summary>
        public static async Task<WikiPerson> GetWikiPersonAsync(
            string wikipediaTitle,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(wikipediaTitle))
            {
                throw new ArgumentException("Wikipedia title cannot be null or empty.", nameof(wikipediaTitle));
            }

            var url = $"{WikiApiBaseUrl}/page/summary/{Uri.EscapeDataString(wikipediaTitle)}";

            try
            {
                var root = await ExecuteJsonRequestAsync(
                    url,
                    cancellationToken,
                    notFoundMessage: $"No Wikipedia page found for title '{wikipediaTitle}'.").ConfigureAwait(false);

                return BuildPersonFromSummary(root);
            }
            catch (HttpRequestException ex)
            {
                throw new HttpRequestException($"Failed to retrieve Wikipedia summary for '{wikipediaTitle}'.", ex);
            }
            catch (JsonException ex)
            {
                throw new JsonException($"Failed to parse Wikipedia response for title '{wikipediaTitle}'.", ex);
            }
        }

        private static async Task<Collection<WikiPerson>> GetPeopleOnThisDayAsync(
            string eventType,
            int month,
            int day,
            int? yearFilter,
            CancellationToken cancellationToken)
        {
            ValidateMonthDay(month, day);

            if (yearFilter.HasValue && yearFilter.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(yearFilter), "Year must be greater than 0.");
            }

            var url = $"{WikiApiBaseUrl}/feed/onthisday/{eventType}/{month}/{day}";

            try
            {
                var root = await ExecuteJsonRequestAsync(url, cancellationToken).ConfigureAwait(false);

                if (!root.TryGetProperty(eventType, out var events) || events.ValueKind != JsonValueKind.Array)
                {
                    return new Collection<WikiPerson>();
                }

                var people = new Collection<WikiPerson>();

                foreach (var item in events.EnumerateArray())
                {
                    var year = ExtractIntProperty(item, "year");
                    if (yearFilter.HasValue && year != yearFilter.Value)
                    {
                        continue;
                    }

                    if (!item.TryGetProperty("pages", out var pages) || pages.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (var page in pages.EnumerateArray())
                    {
                        people.Add(BuildPersonFromPage(page, month, day, year, eventType));
                    }
                }

                return people;
            }
            catch (HttpRequestException ex)
            {
                throw new HttpRequestException($"Failed to retrieve Wikipedia people {eventType} on {month}/{day}.", ex);
            }
            catch (JsonException ex)
            {
                throw new JsonException($"Failed to parse Wikipedia response for people {eventType} on {month}/{day}.", ex);
            }
        }

        private static WikiPerson BuildPersonFromSummary(JsonElement root)
        {
            return new WikiPerson
            {
                Id = ExtractWikiEntityId(root),
                Name = ExtractNormalizedTitle(root),
                Description = ExtractStringProperty(root, "description"),
                Image = ExtractThumbnailSource(root),
                Link = ExtractPageUrl(root)
            };
        }

        private static WikiPerson BuildPersonFromPage(JsonElement page, int month, int day, int year, string eventType)
        {
            var person = new WikiPerson
            {
                Id = ExtractWikiEntityId(page),
                Name = ExtractNormalizedTitle(page),
                Description = ExtractStringProperty(page, "description"),
                Image = ExtractThumbnailSource(page),
                Link = ExtractPageUrl(page)
            };

            var eventDate = TryCreateDate(year, month, day);
            if (eventType == "births")
            {
                person.Birthday = eventDate;
            }
            else if (eventType == "deaths")
            {
                person.Death = eventDate;
            }

            return person;
        }

        private static int ExtractWikiEntityId(JsonElement item)
        {
            if (item.TryGetProperty("wikibase_item", out var wikibaseItem))
            {
                var itemValue = wikibaseItem.GetString();
                if (!string.IsNullOrWhiteSpace(itemValue) &&
                    itemValue.Length > 1 &&
                    itemValue[0] == 'Q' &&
                    int.TryParse(itemValue.Substring(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var wikidataId))
                {
                    return wikidataId;
                }
            }

            if (item.TryGetProperty("pageid", out var pageId) && pageId.TryGetInt32(out var wikipediaPageId))
            {
                return wikipediaPageId;
            }

            return 0;
        }

        private static string? ExtractNormalizedTitle(JsonElement item)
        {
            if (item.TryGetProperty("titles", out var titles) &&
                titles.TryGetProperty("normalized", out var normalizedTitle))
            {
                return normalizedTitle.GetString();
            }

            if (item.TryGetProperty("title", out var title))
            {
                var titleValue = title.GetString();
                return titleValue?.Replace('_', ' ');
            }

            return null;
        }

        private static string? ExtractStringProperty(JsonElement item, string propertyName)
        {
            if (!item.TryGetProperty(propertyName, out var property))
            {
                return null;
            }

            return property.GetString();
        }

        private static string? ExtractThumbnailSource(JsonElement item)
        {
            if (item.TryGetProperty("thumbnail", out var thumbnail) &&
                thumbnail.TryGetProperty("source", out var source))
            {
                return source.GetString();
            }

            return null;
        }

        private static string? ExtractPageUrl(JsonElement item)
        {
            if (!item.TryGetProperty("content_urls", out var contentUrls))
            {
                return null;
            }

            if (contentUrls.TryGetProperty("desktop", out var desktop) &&
                desktop.TryGetProperty("page", out var pageUrl))
            {
                return pageUrl.GetString();
            }

            if (contentUrls.TryGetProperty("mobile", out var mobile) &&
                mobile.TryGetProperty("page", out var mobilePageUrl))
            {
                return mobilePageUrl.GetString();
            }

            return null;
        }

        private static int ExtractIntProperty(JsonElement item, string propertyName)
        {
            if (!item.TryGetProperty(propertyName, out var property))
            {
                return 0;
            }

            if (property.TryGetInt32(out var value))
            {
                return value;
            }

            return 0;
        }

        private static DateTime? TryCreateDate(int year, int month, int day)
        {
            if (year <= 0)
            {
                return null;
            }

            try
            {
                return new DateTime(year, month, day);
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        private static void ValidateMonthDay(int month, int day)
        {
            if (month < 1 || month > 12)
            {
                throw new ArgumentOutOfRangeException(nameof(month), "Month must be between 1 and 12.");
            }

            var maxDay = DateTime.DaysInMonth(2000, month);
            if (day < 1 || day > maxDay)
            {
                throw new ArgumentOutOfRangeException(nameof(day), $"Day must be between 1 and {maxDay} for month {month}.");
            }
        }

        private static async Task<JsonElement> ExecuteJsonRequestAsync(
            string url,
            CancellationToken cancellationToken,
            string? notFoundMessage = null)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_cache.TryGetValue(url, out var cached))
            {
                return cached;
            }

            for (var attempt = 1; attempt <= MaxRetryAttempts; attempt++)
            {
                try
                {
                    using (var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            var code = (int)response.StatusCode;
                            if (code == 404 && notFoundMessage != null)
                            {
                                throw new InvalidOperationException(notFoundMessage);
                            }

                            var isTransient = code == 429 || code >= 500;
                            if (isTransient && attempt < MaxRetryAttempts)
                            {
                                var delay = code == 429
                                    ? TimeSpan.FromSeconds(Math.Pow(2, attempt + 1))
                                    : TimeSpan.FromSeconds(Math.Pow(2, attempt));

                                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                                continue;
                            }

                            response.EnsureSuccessStatusCode();
                        }

                        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        using (var doc = JsonDocument.Parse(json))
                        {
                            var result = doc.RootElement.Clone();
                            _cache.TryAdd(url, result);
                            return result;
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex) when (attempt < MaxRetryAttempts &&
                    (ex is HttpRequestException || ex is OperationCanceledException || ex is TaskCanceledException))
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), cancellationToken).ConfigureAwait(false);
                }
            }

            throw new InvalidOperationException("Retry loop exhausted unexpectedly.");
        }
    }
}
