using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;

namespace WikiDataLib
{
    /// <summary>
    /// Provides access to WikiData SPARQL queries for searching and retrieving person information.
    /// </summary>
    public static class WikiData
    {
        private const string WikiDataSparqlEndpoint = "https://query.wikidata.org/sparql";
        private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/79.0.3945.88 Safari/537.36";
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

        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        static WikiData()
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        }

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

            var encodedSearchString = Uri.EscapeDataString(searchString);
            var query = BuildSearchQuery(encodedSearchString);

            try
            {
                var root = await ExecuteSparqlQueryAsync(query, cancellationToken).ConfigureAwait(false);

                if (!TryGetBindings(root, out var bindings))
                {
                    return new Collection<WikiPerson>();
                }

                var foundPersons = new Collection<WikiPerson>();

                foreach (var item in bindings.EnumerateArray())
                {
                    var person = GetPersonFromJsonElement(item);
                    foundPersons.Add(person);
                }

                return foundPersons;
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
                var person = GetPersonFromJsonElement(item);

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

        private static string BuildSearchQuery(string encodedSearchString)
        {
            return "SELECT distinct (SAMPLE(?image)as ?image) ?item ?itemLabel ?itemDescription" +
                " (SAMPLE(?DR) as ?DR)(SAMPLE(?RIP) as ?RIP)(SAMPLE(?article) as ?article) " +
                "WHERE {?item wdt:P31 wd:Q5. ?item ?label '" + encodedSearchString + "'@en. OPTIONAL{?item wdt:P569 ?DR .}" +
                " ?article schema:about ?item . ?article schema:inLanguage 'en'. ?article schema:isPartOf <https://en.wikipedia.org/>. " +
                "OPTIONAL{?item wdt:P570 ?RIP .} " +
                "OPTIONAL{?item wdt:P18 ?image .} " +
                "SERVICE wikibase:label { bd:serviceParam wikibase:language 'en'. }} " +
                "GROUP BY ?item ?itemLabel ?itemDescription";
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
                "SERVICE wikibase:label { bd:serviceParam wikibase:language  'en'}}" +
                "GROUP BY ?item ?itemLabel ?itemDescription";
        }

        private static async Task<JsonElement> ExecuteSparqlQueryAsync(string query, CancellationToken cancellationToken)
        {
            var url = $"{WikiDataSparqlEndpoint}?query={Uri.EscapeDataString(query)}&format=json";

            using (var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var doc = JsonDocument.Parse(json);
                return doc.RootElement;
            }
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

        private static WikiPerson GetPersonFromJsonElement(JsonElement item)
        {
            var id = ExtractId(item);
            var name = ExtractStringProperty(item, FieldItemLabel);
            var description = ExtractStringProperty(item, FieldItemDescription);
            var birthday = ExtractDateProperty(item, FieldBirthDate);
            var death = ExtractDateProperty(item, FieldDeathDate);
            var image = ExtractStringProperty(item, FieldImage);
            var link = ExtractStringProperty(item, FieldArticle);

            return new WikiPerson
            {
                Id = id,
                Name = name,
                Description = description,
                Birthday = birthday,
                Death = death,
                Image = image,
                Link = link
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
            if (dateString != null && DateTime.TryParse(dateString, out var date))
            {
                return date;
            }

            return null;
        }
    }
}
