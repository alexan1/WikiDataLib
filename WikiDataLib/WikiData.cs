using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;


namespace WikiDataLib
{
    public static class WikiData
    {
        public async static Task<Collection<WikiPerson>> WikiPeopleSearch(string searchString)
        {
            var urlBase = "https://query.wikidata.org/sparql";
            var query = "SELECT distinct (SAMPLE(?image)as ?image) ?item ?itemLabel ?itemDescription" +
                " (SAMPLE(?DR) as ?DR)(SAMPLE(?RIP) as ?RIP)(SAMPLE(?article) as ?article) " +
                "WHERE {?item wdt:P31 wd:Q5. ?item ?label '" + searchString + "'@en. OPTIONAL{?item wdt:P569 ?DR .}" +
                " ?article schema:about ?item . ?article schema:inLanguage 'en'. ?article schema:isPartOf <https://en.wikipedia.org/>. " +
                "OPTIONAL{?item wdt:P570 ?RIP .} " +
                "OPTIONAL{?item wdt:P18 ?image .} " +
                "SERVICE wikibase:label { bd:serviceParam wikibase:language 'en'. }} " +
                "GROUP BY ?item ?itemLabel ?itemDescription";
            var url = urlBase + "?query=" + query + "&format=json";
            var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/79.0.3945.88 Safari/537.36");
            var json = await client.GetStringAsync(url);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var entities = root.GetProperty("results").GetProperty("bindings");

            var FoundPersons = new Collection<WikiPerson>();               
           
            foreach (var item in entities.EnumerateArray())
            {
                var person = GetPersonFromJsonElement(item);
                FoundPersons.Add(person);                
            }
            return FoundPersons;
        }

        public async static Task<WikiPerson> GetWikiPerson(int id)
        {
            var urlBase = "https://query.wikidata.org/sparql";
            var query = "SELECT distinct (SAMPLE(?image)as ?image)  ?item ?itemLabel ?itemDescription" +
            " (SAMPLE(?DR) as ?DR)(SAMPLE(?RIP) as ?RIP)(SAMPLE(?article) as ?article) " +
            "WHERE{ ?article  schema:about ?item ; schema:inLanguage  'en' ; schema:isPartOf    <https://en.wikipedia.org/>" +
            "FILTER ( ?item = <http://www.wikidata.org/entity/Q" + id +"> )" +
            "OPTIONAL { ?item  wdt:P569  ?DR }" +
            "OPTIONAL { ?item  wdt:P570  ?RIP }" +
            "OPTIONAL { ?item  wdt:P18  ?image }" +
            "SERVICE wikibase:label { bd:serviceParam wikibase:language  'en'}}" +
            "GROUP BY ?item ?itemLabel ?itemDescription";
            var url = urlBase + "?query=" + query + "&format=json";
            var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/79.0.3945.88 Safari/537.36");
            var json = await client.GetStringAsync(url);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var item = root.GetProperty("results").GetProperty("bindings")[0];

            var person = GetPersonFromJsonElement(item);

            return person;
        }

        private static WikiPerson GetPersonFromJsonElement(JsonElement item)
        {
            int id = 0;
            if (item.TryGetProperty("item", out JsonElement itemId))
            {
                int.TryParse(itemId.GetProperty("value").ToString().Substring(32), out id);
            }

            var name = String.Empty;
            if (item.TryGetProperty("itemLabel", out JsonElement itemLabel))
            {
                name = itemLabel.GetProperty("value").ToString();
            }

            var description = String.Empty;
            if (item.TryGetProperty("itemDescription", out JsonElement itemDescription))
            {
                description = itemDescription.GetProperty("value").ToString();
            }

            DateTime birthday = DateTime.MinValue;
            if (item.TryGetProperty("DR", out JsonElement DR))
            {
                DateTime.TryParse(DR.GetProperty("value").ToString(), out birthday);
            }

            DateTime death = DateTime.MinValue;
            if (item.TryGetProperty("RIP", out JsonElement RIP))
            {
                DateTime.TryParse(RIP.GetProperty("value").ToString(), out death);
            }

            var image = string.Empty;
            if (item.TryGetProperty("image", out JsonElement imageE))
            {
                image = imageE.GetProperty("value").ToString();
            }

            var link = String.Empty;
            if (item.TryGetProperty("article", out JsonElement article))
            {
                link = article.GetProperty("value").ToString();
            }


            var person = new WikiPerson
            {
                Id = id,
                Name = name,
                Description = description,
                Birthday = birthday,
                Death = death,
                Image = image,
                Link = link,
                //Rating = rating
            };

            return person;
        }
    }
}
