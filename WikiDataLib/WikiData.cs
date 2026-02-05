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
            var query = "SELECT distinct (SAMPLE(?image)as ?image) ?item ?itemLabel ?itemDescription" +
                " (SAMPLE(?DR) as ?DR)(SAMPLE(?RIP) as ?RIP)(SAMPLE(?article) as ?article) " +
                "WHERE {?item wdt:P31 wd:Q5. ?item ?label '" + encodedSearchString + "'@en. OPTIONAL{?item wdt:P569 ?DR .}" +
                " ?article schema:about ?item . ?article schema:inLanguage 'en'. ?article schema:isPartOf <https://en.wikipedia.org/>. " +
                "OPTIONAL{?item wdt:P570 ?RIP .} " +
                "OPTIONAL{?item wdt:P18 ?image .} " +
                "SERVICE wikibase:label { bd:serviceParam wikibase:language 'en'. }} " +
                "GROUP BY ?item ?itemLabel ?itemDescription";

            var url = $"{WikiDataSparqlEndpoint}?query={Uri.EscapeDataString(query)}&format=json";

            try
            {
                using (var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (!root.TryGetProperty("results", out var results) ||
                        !results.TryGetProperty("bindings", out var bindings))
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

            var query = "SELECT distinct (SAMPLE(?image)as ?image)  ?item ?itemLabel ?itemDescription" +
                " (SAMPLE(?DR) as ?DR)(SAMPLE(?RIP) as ?RIP)(SAMPLE(?article) as ?article) " +
                "WHERE{ ?article  schema:about ?item ; schema:inLanguage  'en' ; schema:isPartOf    <https://en.wikipedia.org/>" +
                $"FILTER ( ?item = <{WikiDataEntityUrlPrefix}{id}> )" +
                "OPTIONAL { ?item  wdt:P569  ?DR }" +
                "OPTIONAL { ?item  wdt:P570  ?RIP }" +
                "OPTIONAL { ?item  wdt:P18  ?image }" +
                "SERVICE wikibase:label { bd:serviceParam wikibase:language  'en'}}" +
                "GROUP BY ?item ?itemLabel ?itemDescription";

            var url = $"{WikiDataSparqlEndpoint}?query={Uri.EscapeDataString(query)}&format=json";

            try
            {
                using (var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (!root.TryGetProperty("results", out var results) ||
                        !results.TryGetProperty("bindings", out var bindings) ||
                        bindings.GetArrayLength() == 0)
                    {
                        throw new InvalidOperationException($"No person found with WikiData ID Q{id}.");
                    }

                    var item = bindings[0];
                    var person = GetPersonFromJsonElement(item);

                    return person;
                }
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

        private static WikiPerson GetPersonFromJsonElement(JsonElement item)
        {
            int id = 0;
            if (item.TryGetProperty("item", out JsonElement itemId))
            {
                var idString = itemId.GetProperty("value").GetString();
                if (idString != null && idString.Length > WikiDataEntityPrefixLength)
                {
                    int.TryParse(idString.Substring(WikiDataEntityPrefixLength), out id);
                }
            }

            string? name = null;
            if (item.TryGetProperty("itemLabel", out JsonElement itemLabel))
            {
                name = itemLabel.GetProperty("value").GetString();
            }

            string? description = null;
            if (item.TryGetProperty("itemDescription", out JsonElement itemDescription))
            {
                description = itemDescription.GetProperty("value").GetString();
            }

            DateTime? birthday = null;
            if (item.TryGetProperty("DR", out JsonElement DR))
            {
                var dateString = DR.GetProperty("value").GetString();
                if (dateString != null && DateTime.TryParse(dateString, out var date))
                {
                    birthday = date;
                }
            }

            DateTime? death = null;
            if (item.TryGetProperty("RIP", out JsonElement RIP))
            {
                var dateString = RIP.GetProperty("value").GetString();
                if (dateString != null && DateTime.TryParse(dateString, out var date))
                {
                    death = date;
                }
            }

            string? image = null;
            if (item.TryGetProperty("image", out JsonElement imageE))
            {
                image = imageE.GetProperty("value").GetString();
            }

            string? link = null;
            if (item.TryGetProperty("article", out JsonElement article))
            {
                link = article.GetProperty("value").GetString();
            }

            var person = new WikiPerson
            {
                Id = id,
                Name = name,
                Description = description,
                Birthday = birthday,
                Death = death,
                Image = image,
                Link = link
            };

            return person;
        }
    }
}
