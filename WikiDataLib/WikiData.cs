using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WikiDataLib
{
    /// <summary>
    /// Provides access to WikiData SPARQL queries for searching and retrieving person information.
    /// </summary>
    public static class WikiData
    {
        private const string WikiDataSparqlEndpoint = "https://query.wikidata.org/sparql";
        private const string WikiDataEntityUrlPrefix = "http://www.wikidata.org/entity/Q";
        private const int WikiDataEntityPrefixLength = 32;

        // SPARQL query field names
        private const string FieldItem = "item";
        private const string FieldItemLabel = "itemLabel";
        private const string FieldItemDescription = "itemDescription";
        private const string FieldBirthDate = "DR";
        private const string FieldDeathDate = "RIP";
        private const string FieldImage = "image";
        private const string FieldArticle = "article";

        /// <summary>
        /// Searches for people in WikiData by name.
        /// </summary>
        /// <param name="searchString">The search term to find people.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>A collection of matching WikiPerson objects.</returns>
        /// <exception cref="ArgumentException">Thrown when searchString is null or whitespace.</exception>
        /// <exception cref="HttpRequestException">Thrown when the WikiData API request fails.</exception>
        /// <exception cref="JsonException">Thrown when the response cannot be parsed.</exception>
        public static async Task<Collection<WikiPerson>> WikiPeopleSearchAsync(string searchString, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(searchString))
            {
                throw new ArgumentException("Search string cannot be null or empty.", nameof(searchString));
            }

            var searchPattern = BuildSearchPattern(searchString);

            try
            {
                var entityIds = await SearchEntityIdsAsync(searchString, cancellationToken).ConfigureAwait(false);
                if (entityIds.Count == 0)
                {
                    return new Collection<WikiPerson>();
                }

                var query = BuildSearchQuery(entityIds, searchPattern);
                var root = await ExecuteSparqlQueryAsync(query, cancellationToken).ConfigureAwait(false);

                if (!TryGetBindings(root, out var bindings))
                {
                    return new Collection<WikiPerson>();
                }

                var idOrder = entityIds
                    .Select((id, index) => new { id, index })
                    .ToDictionary(item => item.id, item => item.index);

                // Collect all raw image URLs, batch-resolve them in a single API call, then
                // build persons synchronously using the pre-resolved map.
                var bindingList = bindings.EnumerateArray().ToList();
                var rawImageUrls = bindingList.Select(b => ExtractStringProperty(b, FieldImage)).ToList();
                var resolvedImages = await WikiApi.ResolveCommonsFileUrlsBatchAsync(rawImageUrls, cancellationToken).ConfigureAwait(false);

                var foundPersons = new System.Collections.Generic.List<WikiPerson>();
                foreach (var binding in bindingList)
                {
                    var rawImage = ExtractStringProperty(binding, FieldImage);
                    string? resolvedImage = null;
                    if (rawImage != null) resolvedImages.TryGetValue(rawImage, out resolvedImage);

                    var person = GetPersonFromJsonElement(binding, resolvedImage ?? rawImage);
                    if (person.Name != null && Regex.IsMatch(person.Name, searchPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                    {
                        foundPersons.Add(person);
                    }
                }
                foundPersons.Sort((a, b) =>
                {
                    var ai = idOrder.TryGetValue(a.Id, out var ai2) ? ai2 : int.MaxValue;
                    var bi = idOrder.TryGetValue(b.Id, out var bi2) ? bi2 : int.MaxValue;
                    return ai.CompareTo(bi);
                });

                return new Collection<WikiPerson>(foundPersons);
            }
            catch (HttpRequestException ex)
            {
                throw new HttpRequestException($"Failed to retrieve WikiData search results for '{searchString}'.", ex);
            }
            catch (JsonException ex)
            {
                throw new JsonException("Failed to parse WikiData response.", ex);
            }
        }

        private static async Task<Collection<int>> SearchEntityIdsAsync(string searchString, CancellationToken cancellationToken)
        {
            var url = $"https://www.wikidata.org/w/api.php?action=query&list=search&srsearch={Uri.EscapeDataString(searchString)}&format=json&srlimit=50&origin=*";
            var root = await WikiHttp.GetJsonAsync(url, cancellationToken).ConfigureAwait(false);

            var entityIds = new Collection<int>();

            if (!root.TryGetProperty("query", out var queryElement) ||
                !queryElement.TryGetProperty("search", out var searchResults))
            {
                return entityIds;
            }

            foreach (var item in searchResults.EnumerateArray())
            {
                if (!item.TryGetProperty("title", out var titleProperty))
                {
                    continue;
                }

                var title = titleProperty.GetString();
                if (title == null || title.Length <= 1 || title[0] != 'Q')
                {
                    continue;
                }

                if (int.TryParse(title.Substring(1), out var entityId))
                {
                    entityIds.Add(entityId);
                }
            }

            return entityIds;
        }

        /// <summary>
        /// Gets a specific person from WikiData by their numeric ID.
        /// </summary>
        /// <param name="id">The WikiData entity ID (numeric part of Q-identifier, e.g., 303 for Q303).</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>A WikiPerson object with the person's information.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when id is less than or equal to 0.</exception>
        /// <exception cref="HttpRequestException">Thrown when the WikiData API request fails.</exception>
        /// <exception cref="JsonException">Thrown when the response cannot be parsed.</exception>
        /// <exception cref="InvalidOperationException">Thrown when no person is found with the given ID.</exception>
        public static async Task<WikiPerson> GetWikiPersonAsync(int id, CancellationToken cancellationToken = default)
        {
            if (id <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(id), "ID must be greater than 0.");
            }

            var query = BuildPersonByIdQuery(id);

            try
            {
                var root = await ExecuteSparqlQueryAsync(query, cancellationToken).ConfigureAwait(false);

                if (!TryGetBindings(root, out var bindings) || bindings.GetArrayLength() == 0)
                {
                    throw new InvalidOperationException($"No person found with WikiData ID Q{id}.");
                }

                var item = bindings[0];
                var rawImage = ExtractStringProperty(item, FieldImage);
                // Single-person path: one image URL, no batching needed.
                var resolvedImage = await WikiApi.ResolveCommonsFileUrlAsync(rawImage, cancellationToken).ConfigureAwait(false);
                var person = GetPersonFromJsonElement(item, resolvedImage ?? rawImage);

                return person;
            }
            catch (HttpRequestException ex)
            {
                throw new HttpRequestException($"Failed to retrieve WikiData person with ID Q{id}.", ex);
            }
            catch (JsonException ex)
            {
                throw new JsonException($"Failed to parse WikiData response for person Q{id}.", ex);
            }
        }

        /// <summary>
        /// Gets a specific person from Wikipedia by their page title.
        /// </summary>
        public static Task<WikiPerson> GetWikiPersonAsync(string wikipediaTitle, CancellationToken cancellationToken = default)
        {
            return WikiApi.GetWikiPersonAsync(wikipediaTitle, cancellationToken);
        }

        /// <summary>
        /// Gets people born on a specific month and day.
        /// </summary>
        public static Task<Collection<WikiPerson>> GetPeopleBornOnDateAsync(
            int month,
            int day,
            int limit,
            CancellationToken cancellationToken = default)
        {
            ValidateMonthDayLimit(month, day, limit);
            return WikiApi.GetPeopleOnThisDayAsync("births", month, day, null, limit, cancellationToken);
        }

        /// <summary>
        /// Gets people who died on a specific month and day.
        /// </summary>
        public static Task<Collection<WikiPerson>> GetPeopleDiedOnDateAsync(
            int month,
            int day,
            int limit,
            CancellationToken cancellationToken = default)
        {
            ValidateMonthDayLimit(month, day, limit);
            return WikiApi.GetPeopleOnThisDayAsync("deaths", month, day, null, limit, cancellationToken);
        }

        /// <summary>
        /// Gets people who died on a specific year, month, and day.
        /// </summary>
        public static Task<Collection<WikiPerson>> GetPeopleDiedOnDateAsync(
            int year,
            int month,
            int day,
            int limit,
            CancellationToken cancellationToken = default)
        {
            ValidateYearMonthDayLimit(year, month, day, limit);
            return WikiApi.GetPeopleOnThisDayAsync("deaths", month, day, year, limit, cancellationToken);
        }

        /// <summary>
        /// Gets people born on a specific year, month, and day.
        /// </summary>
        public static Task<Collection<WikiPerson>> GetPeopleBornOnDateAsync(
            int year,
            int month,
            int day,
            int limit,
            CancellationToken cancellationToken = default)
        {
            ValidateYearMonthDayLimit(year, month, day, limit);
            return WikiApi.GetPeopleOnThisDayAsync("births", month, day, year, limit, cancellationToken);
        }

        private static string BuildSearchQuery(Collection<int> entityIds, string searchPattern)
        {
            var itemValues = string.Join(" ", entityIds.Select(id => $"wd:Q{id}"));
            var escapedSearchPattern = EscapeSparqlStringLiteral(searchPattern);

            return "SELECT distinct (SAMPLE(?image)as ?image) ?item ?itemLabel ?itemDescription" +
                " (SAMPLE(?DR) as ?DR)(SAMPLE(?RIP) as ?RIP)(SAMPLE(?article) as ?article) " +
                "WHERE {VALUES ?item { " + itemValues + " } ?item wdt:P31 wd:Q5. ?item rdfs:label ?searchLabel. " +
                "FILTER(LANG(?searchLabel) IN ('en','mul')). FILTER(REGEX(?searchLabel, \"" + escapedSearchPattern + "\", 'i')). " +
                "OPTIONAL{?item wdt:P569 ?DR .}" +
                " ?article schema:about ?item . ?article schema:inLanguage 'en'. ?article schema:isPartOf <https://en.wikipedia.org/>. " +
                "OPTIONAL{?item wdt:P570 ?RIP .} " +
                "OPTIONAL{?item wdt:P18 ?image .} " +
                "SERVICE wikibase:label { bd:serviceParam wikibase:language 'en,mul'. }} " +
                "GROUP BY ?item ?itemLabel ?itemDescription LIMIT 50";
        }        

        private static string BuildSearchPattern(string searchString)
        {
            return Regex.Escape(searchString)
                .Replace(@"\*", ".*")
                .Replace(@"\?", ".");
        }

        private static string EscapeSparqlStringLiteral(string value)
        {
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }

        private static string BuildPersonByIdQuery(int id)
        {
            return "SELECT distinct (SAMPLE(?image)as ?image)  ?item ?itemLabel ?itemDescription" +
                " (SAMPLE(?DR) as ?DR)(SAMPLE(?RIP) as ?RIP)(SAMPLE(?article) as ?article) " +
                "WHERE{ ?article  schema:about ?item ; schema:inLanguage  'en' ; schema:isPartOf    <https://en.wikipedia.org/>" +
                $"FILTER ( ?item = <{WikiDataEntityUrlPrefix}{id}> )" +
                "OPTIONAL { ?item  wdt:P569  ?DR }" +
                "OPTIONAL { ?item  wdt:P570  ?RIP }" +
                "OPTIONAL { ?item  wdt:P18  ?image }" +
                "SERVICE wikibase:label { bd:serviceParam wikibase:language 'en,mul'}}" +
                "GROUP BY ?item ?itemLabel ?itemDescription";
        }

        private static void ValidateMonthDayLimit(int month, int day, int limit)
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

            if (limit <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be greater than 0.");
            }
        }

        private static void ValidateYearMonthDayLimit(int year, int month, int day, int limit)
        {
            if (year <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(year), "Year must be greater than 0.");
            }

            ValidateMonthDayLimit(month, day, limit);
        }

        private static async Task<JsonElement> ExecuteSparqlQueryAsync(string query, CancellationToken cancellationToken)
        {
            var url = $"{WikiDataSparqlEndpoint}?query={Uri.EscapeDataString(query)}&format=json";

            return await WikiHttp.GetJsonAsync(url, cancellationToken).ConfigureAwait(false);
        }

        private static bool TryGetBindings(JsonElement root, out JsonElement bindings)
        {
            bindings = default;

            if (!root.TryGetProperty("results", out var results))
            {
                return false;
            }

            if (!results.TryGetProperty("bindings", out bindings))
            {
                return false;
            }

            return true;
        }

        private static WikiPerson GetPersonFromJsonElement(JsonElement item, string? resolvedImageUrl)
        {
            return new WikiPerson
            {
                Id = ExtractId(item),
                Name = ExtractStringProperty(item, FieldItemLabel),
                Description = ExtractStringProperty(item, FieldItemDescription),
                Birthday = ExtractDateProperty(item, FieldBirthDate),
                Death = ExtractDateProperty(item, FieldDeathDate),
                Image = resolvedImageUrl,
                Link = ExtractStringProperty(item, FieldArticle)
            };
        }

        private static int ExtractId(JsonElement item)
        {
            if (!item.TryGetProperty(FieldItem, out var itemId))
            {
                return 0;
            }

            var idString = itemId.GetProperty("value").GetString();
            if (idString == null || idString.Length <= WikiDataEntityPrefixLength)
            {
                return 0;
            }

            int.TryParse(idString.Substring(WikiDataEntityPrefixLength), out var id);
            return id;
        }

        private static string? ExtractStringProperty(JsonElement item, string propertyName)
        {
            if (!item.TryGetProperty(propertyName, out var property))
            {
                return null;
            }

            return property.GetProperty("value").GetString();
        }

        private static DateTime? ExtractDateProperty(JsonElement item, string propertyName)
        {
            if (!item.TryGetProperty(propertyName, out var property))
            {
                return null;
            }

            var dateString = property.GetProperty("value").GetString();
            if (dateString != null)
            {
                var datePart = dateString.Split('T')[0].TrimStart('+');
                if (DateTime.TryParseExact(datePart, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    return date;
                }
            }

            return null;
        }
    }
}
