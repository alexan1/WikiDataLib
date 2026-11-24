using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
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
        private const string CommonsUploadBaseUrl = "https://upload.wikimedia.org/wikipedia/commons/";

        // Replaces the HTTP handler used for all requests (WikiApi and WikiData) — intended for unit tests only.
        internal static void SetHttpMessageHandlerForTesting(HttpMessageHandler? handler)
        {
            WikiHttp.SetHttpMessageHandlerForTesting(handler);
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
                var root = await WikiHttp.GetJsonAsync(
                    url,
                    cancellationToken,
                    notFoundMessage: $"No Wikipedia page found for title '{wikipediaTitle}'.").ConfigureAwait(false);

                return await BuildPersonFromSummaryAsync(root, cancellationToken).ConfigureAwait(false);
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

        private static Task<Collection<WikiPerson>> GetPeopleOnThisDayAsync(
            string eventType,
            int month,
            int day,
            int? yearFilter,
            CancellationToken cancellationToken)
        {
            return GetPeopleOnThisDayAsync(eventType, month, day, yearFilter, null, cancellationToken);
        }

        // limit: when set, stops collecting pages once `limit` are found, so image resolution
        // and object construction only happen for the results actually returned.
        internal static async Task<Collection<WikiPerson>> GetPeopleOnThisDayAsync(
            string eventType,
            int month,
            int day,
            int? yearFilter,
            int? limit,
            CancellationToken cancellationToken)
        {
            if (limit.HasValue && limit.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be greater than 0.");
            }

            ValidateMonthDay(month, day);

            if (yearFilter.HasValue && yearFilter.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(yearFilter), "Year must be greater than 0.");
            }

            var url = $"{WikiApiBaseUrl}/feed/onthisday/{eventType}/{month}/{day}";

            try
            {
                var root = await WikiHttp.GetJsonAsync(url, cancellationToken).ConfigureAwait(false);

                if (!root.TryGetProperty(eventType, out var events) || events.ValueKind != JsonValueKind.Array)
                {
                    return new Collection<WikiPerson>();
                }

                // Collect all candidate pages before any network call.
                var pageEntries = new System.Collections.Generic.List<(JsonElement Page, int Year, string? Thumb)>();
                foreach (var item in events.EnumerateArray())
                {
                    var year = ExtractIntProperty(item, "year");
                    if (yearFilter.HasValue && year != yearFilter.Value)
                        continue;

                    if (!item.TryGetProperty("pages", out var pages) || pages.ValueKind != JsonValueKind.Array)
                        continue;

                    foreach (var page in pages.EnumerateArray())
                    {
                        if (limit.HasValue && pageEntries.Count >= limit.Value)
                            break;
                        pageEntries.Add((page, year, ExtractRawThumbnailSource(page)));
                    }

                    if (limit.HasValue && pageEntries.Count >= limit.Value)
                        break;
                }

                // Batch-resolve all Commons thumbnail URLs (only for the pages being returned).
                var resolvedThumbnails = await ResolveCommonsFileUrlsBatchAsync(
                    pageEntries.Select(e => e.Thumb), cancellationToken).ConfigureAwait(false);

                var people = new Collection<WikiPerson>();
                foreach (var entry in pageEntries)
                {
                    string? resolvedThumb = null;
                    if (entry.Thumb != null)
                        resolvedThumbnails.TryGetValue(entry.Thumb, out resolvedThumb);

                    people.Add(BuildPersonFromPage(entry.Page, month, day, entry.Year, eventType, resolvedThumb));
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

        private static async Task<WikiPerson> BuildPersonFromSummaryAsync(JsonElement root, CancellationToken cancellationToken)
        {
            var rawThumb = ExtractRawThumbnailSource(root);
            var resolvedThumb = await ResolveCommonsFileUrlAsync(rawThumb, cancellationToken).ConfigureAwait(false);
            return new WikiPerson
            {
                Id = ExtractWikiEntityId(root),
                Name = ExtractNormalizedTitle(root),
                Description = ExtractStringProperty(root, "description"),
                Image = resolvedThumb,
                Link = ExtractPageUrl(root)
            };
        }

        private static WikiPerson BuildPersonFromPage(JsonElement page, int month, int day, int year, string eventType, string? resolvedThumb)
        {
            var person = new WikiPerson
            {
                Id = ExtractWikiEntityId(page),
                Name = ExtractNormalizedTitle(page),
                Description = ExtractStringProperty(page, "description"),
                Image = resolvedThumb,
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

        // Extracts the raw thumbnail source URL from a page element without any resolution.
        private static string? ExtractRawThumbnailSource(JsonElement item)
        {
            if (item.TryGetProperty("thumbnail", out var thumbnail) &&
                thumbnail.TryGetProperty("source", out var source))
            {
                return source.GetString();
            }

            return null;
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

        // Resolves a single commons.wikimedia.org Special:FilePath URL via the imageinfo API.
        // Used for single-person lookups; use ResolveCommonsFileUrlsBatchAsync for collections.
        internal static async Task<string?> ResolveCommonsFileUrlAsync(string? source, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(source))
                return source;

            var batch = new System.Collections.Generic.List<string?> { source };
            var result = await ResolveCommonsFileUrlsBatchAsync(batch, cancellationToken).ConfigureAwait(false);
            return result.TryGetValue(source, out var resolved) ? resolved : source;
        }

        // Resolves a collection of commons.wikimedia.org Special:FilePath URLs to direct
        // upload.wikimedia.org URLs via the Commons imageinfo API (one call per unique width value,
        // with up to 50 pipe-separated titles per call). Non-Commons URLs pass through unchanged.
        // Returns a dictionary keyed by the original source URL.
        internal static async Task<System.Collections.Generic.IReadOnlyDictionary<string, string?>> ResolveCommonsFileUrlsBatchAsync(
            System.Collections.Generic.IEnumerable<string?> sources,
            CancellationToken cancellationToken)
        {
            const string commonsHost = "commons.wikimedia.org";
            const string specialFilePathPrefix = "/wiki/Special:FilePath/";
            const int maxTitlesPerCall = 50;

            var result = new System.Collections.Generic.Dictionary<string, string?>(StringComparer.Ordinal);

            // Group Commons FilePath URLs by their ?width param. Other URLs pass through.
            // titleToSource: "File:Name" → original source URL, grouped by width.
            // Empty string "" is the sentinel key for "no width specified".
            var byWidth = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string>>(StringComparer.Ordinal);

            foreach (var source in sources)
            {
                if (source == null) continue;
                if (result.ContainsKey(source)) continue; // deduplicate
                result[source] = source; // default: pass through unchanged

                if (!Uri.TryCreate(source, UriKind.Absolute, out var uri)) continue;
                if (!commonsHost.Equals(uri.Host, StringComparison.OrdinalIgnoreCase)) continue;
                if (!uri.AbsolutePath.StartsWith(specialFilePathPrefix, StringComparison.OrdinalIgnoreCase)) continue;

                var fileNameEncoded = uri.AbsolutePath.Substring(specialFilePathPrefix.Length);
                var fileName = Uri.UnescapeDataString(fileNameEncoded);
                if (string.IsNullOrWhiteSpace(fileName)) continue;

                // No width requested: the original file URL is a pure function of the file name,
                // so compute it locally instead of calling the imageinfo API.
                var widthKey = ExtractWidthFromQuery(uri.Query);
                if (widthKey == null)
                {
                    result[source] = BuildCommonsOriginalUrl(fileName);
                    continue;
                }

                // Thumbnails still go through the API: their naming varies by file type
                // (SVG→PNG, TIFF/PDF page prefixes) and widths larger than the original are not served.
                if (!byWidth.TryGetValue(widthKey, out var group))
                {
                    group = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    byWidth[widthKey] = group;
                }
                group["File:" + fileName] = source;
            }

            foreach (var widthGroup in byWidth)
            {
                string? widthParam = widthGroup.Key;
                var titleToSource = widthGroup.Value;

                // Chunk into batches of maxTitlesPerCall to stay within API limits.
                var titleList = new System.Collections.Generic.List<string>(titleToSource.Keys);
                for (var offset = 0; offset < titleList.Count; offset += maxTitlesPerCall)
                {
                    var chunk = titleList.GetRange(offset, Math.Min(maxTitlesPerCall, titleList.Count - offset));

                    // Pipe-separate titles; encode each filename but keep | as literal separator.
                    var titlesParam = string.Join("|", chunk.Select(t => Uri.EscapeDataString(t)));
                    var apiUrl = $"https://commons.wikimedia.org/w/api.php?action=query&titles={titlesParam}&prop=imageinfo&iiprop=url&iiurlwidth={Uri.EscapeDataString(widthParam)}&format=json&origin=*";

                    try
                    {
                        var root = await WikiHttp.GetJsonAsync(apiUrl, cancellationToken).ConfigureAwait(false);
                        ParseImageInfoResponse(root, widthParam, titleToSource, result);
                    }
                    catch (Exception ex) when (ex is HttpRequestException || ex is JsonException || ex is InvalidOperationException)
                    {
                        // Leave this chunk's entries at their default (original URL) passthrough.
                    }
                }
            }

            return result;
        }

        // Parses one imageinfo API response page and writes resolved URLs into `result`.
        private static void ParseImageInfoResponse(
            JsonElement root,
            string? widthParam,
            System.Collections.Generic.Dictionary<string, string> titleToSource,
            System.Collections.Generic.Dictionary<string, string?> result)
        {
            if (!root.TryGetProperty("query", out var query)) return;

            // The `normalized` array maps from the title we sent → the canonical title the API used.
            // We need this to look up results when the API normalizes our input title.
            var normalizedMap = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (query.TryGetProperty("normalized", out var normalized))
            {
                foreach (var n in normalized.EnumerateArray())
                {
                    if (n.TryGetProperty("from", out var from) && n.TryGetProperty("to", out var to))
                    {
                        var fromStr = from.GetString();
                        var toStr = to.GetString();
                        if (fromStr != null && toStr != null)
                            normalizedMap[toStr] = fromStr; // canonical title → our sent title
                            // Note: if two sent titles normalize to the same canonical title (rare),
                            // the last entry wins; the other image falls back to its original URL.
                    }
                }
            }

            if (!query.TryGetProperty("pages", out var pages)) return;

            foreach (var page in pages.EnumerateObject())
            {
                if (page.Value.TryGetProperty("missing", out _)) continue;
                if (!page.Value.TryGetProperty("title", out var titleProp)) continue;

                var pageTitle = titleProp.GetString();
                if (pageTitle == null) continue;

                // Find the original source URL: try direct match, then via normalized map.
                if (!titleToSource.TryGetValue(pageTitle, out var originalSource))
                {
                    if (!normalizedMap.TryGetValue(pageTitle, out var sentTitle) ||
                        !titleToSource.TryGetValue(sentTitle, out originalSource))
                        continue;
                }

                if (!page.Value.TryGetProperty("imageinfo", out var imageinfo) || imageinfo.GetArrayLength() == 0)
                    continue;

                var info = imageinfo[0];
                string? urlProp;
                if (widthParam != null && info.TryGetProperty("thumburl", out var thumbUrl))
                    urlProp = thumbUrl.GetString();
                else if (info.TryGetProperty("url", out var fullUrl))
                    urlProp = fullUrl.GetString();
                else
                    urlProp = null;

                if (!string.IsNullOrWhiteSpace(urlProp))
                    result[originalSource] = StripQueryParams(urlProp, "utm_source", "utm_campaign", "utm_content");
            }
        }

        // Builds the direct upload.wikimedia.org URL for a Commons file, following MediaWiki's
        // hashed upload directory layout: /{md5[0]}/{md5[0..2]}/{File_name}.
        // Example: "Elvis Presley promoting Jailhouse Rock.jpg"
        //   → https://upload.wikimedia.org/wikipedia/commons/9/99/Elvis_Presley_promoting_Jailhouse_Rock.jpg
        internal static string BuildCommonsOriginalUrl(string fileName)
        {
            var dbKey = NormalizeCommonsFileName(fileName);

            string hash;
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                // Only the first byte (two hex chars) of the digest is needed for the path.
                var digest = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(dbKey));
                hash = digest[0].ToString("x2", CultureInfo.InvariantCulture);
            }

            // Upload URLs use plain RFC 3986 encoding (e.g. ',' → %2C, '(' → %28, '&' → %26),
            // which is exactly what Uri.EscapeDataString produces.
            return CommonsUploadBaseUrl + hash[0] + "/" + hash + "/" + Uri.EscapeDataString(dbKey);
        }

        // MediaWiki title normalization for file names: spaces → underscores, runs collapsed,
        // ends trimmed, and first character upper-cased (File namespace is first-letter case-insensitive).
        private static string NormalizeCommonsFileName(string fileName)
        {
            var sb = new System.Text.StringBuilder(fileName.Length);
            foreach (var c in fileName.Trim())
            {
                var ch = c == ' ' ? '_' : c;
                if (ch == '_' && sb.Length > 0 && sb[sb.Length - 1] == '_')
                    continue;
                sb.Append(ch);
            }

            while (sb.Length > 0 && sb[sb.Length - 1] == '_')
                sb.Length--;
            while (sb.Length > 0 && sb[0] == '_')
                sb.Remove(0, 1);

            if (sb.Length > 0 && !char.IsSurrogate(sb[0]))
                sb[0] = char.ToUpperInvariant(sb[0]);

            return sb.ToString();
        }

        // Parses ?width=N or &width=N from a raw query string (e.g. "?width=330").
        private static string? ExtractWidthFromQuery(string? querySuffix)
        {
            if (string.IsNullOrEmpty(querySuffix))
                return null;
            var q = querySuffix.TrimStart('?');
            foreach (var pair in q.Split('&'))
            {
                var eq = pair.IndexOf('=');
                if (eq < 0) continue;
                var key = pair.Substring(0, eq);
                if (string.Equals(key, "width", StringComparison.OrdinalIgnoreCase))
                {
                    var val = pair.Substring(eq + 1);
                    return string.IsNullOrEmpty(val) ? null : val;
                }
            }
            return null;
        }

        // Removes specified query parameters from a URL, returning a clean URL.
        private static string StripQueryParams(string url, params string[] paramsToRemove)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Query))
                return url;

            var q = uri.Query.TrimStart('?');
            var kept = new System.Collections.Generic.List<string>();
            foreach (var pair in q.Split('&'))
            {
                var eq = pair.IndexOf('=');
                var key = eq >= 0 ? pair.Substring(0, eq) : pair;
                var skip = false;
                foreach (var p in paramsToRemove)
                {
                    if (string.Equals(key, p, StringComparison.OrdinalIgnoreCase)) { skip = true; break; }
                }
                if (!skip) kept.Add(pair);
            }

            var baseUrl = url.IndexOf('?') >= 0 ? url.Substring(0, url.IndexOf('?')) : url;
            return kept.Count > 0 ? $"{baseUrl}?{string.Join("&", kept)}" : baseUrl;
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
    }
}
