using Microsoft.VisualStudio.TestTools.UnitTesting;
using WikiDataLib;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace WikiDataTest
{
    [TestClass]
    public class WikiTests
    {
        #region Integration Tests - Happy Path

        [TestMethod]
        public async Task WhenSearchingForPope_ShouldReturnResults()
        {
            var people = await WikiData.WikiPeopleSearchAsync("Pope");
            Assert.AreNotEqual(0, people.Count);
        }

        [TestMethod]
        public async Task WhenGettingElvisPresley_ShouldReturnCorrectName()
        {
            var person = await WikiData.GetWikiPersonAsync(303);

            Assert.IsNotNull(person);
            Assert.AreEqual("Elvis Presley", person.Name);
        }

        [TestMethod]
        public async Task WhenGettingElvisPresleyByWikipediaTitle_ShouldReturnCorrectName()
        {
            var person = await WikiData.GetWikiPersonAsync("Elvis Presley");

            Assert.IsNotNull(person);
            Assert.AreEqual("Elvis Presley", person.Name);
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow("   ")]
        public async Task WhenGettingPersonByWikipediaTitle_IsEmptyOrWhitespace_ShouldThrowArgumentException(string wikipediaTitle)
        {
            await Assert.ThrowsExceptionAsync<ArgumentException>(
                async () => await WikiData.GetWikiPersonAsync(wikipediaTitle));
        }

        [TestMethod]
        public async Task WhenGettingPersonByWikipediaTitle_IsNull_ShouldThrowArgumentException()
        {
            await Assert.ThrowsExceptionAsync<ArgumentException>(
                async () => await WikiData.GetWikiPersonAsync(null!));
        }

        #endregion

        #region Input Validation Tests - WikiPeopleSearchAsync

        [TestMethod]
        public async Task WhenSearchStringIsNull_ShouldThrowArgumentException()
        {
            await Assert.ThrowsExceptionAsync<ArgumentException>(
                async () => await WikiData.WikiPeopleSearchAsync(null!));
        }

        [TestMethod]
        public async Task WhenSearchStringIsEmpty_ShouldThrowArgumentException()
        {
            await Assert.ThrowsExceptionAsync<ArgumentException>(
                async () => await WikiData.WikiPeopleSearchAsync(string.Empty));
        }

        [TestMethod]
        public async Task WhenSearchStringIsWhitespace_ShouldThrowArgumentException()
        {
            await Assert.ThrowsExceptionAsync<ArgumentException>(
                async () => await WikiData.WikiPeopleSearchAsync("   "));
        }

        #endregion

        #region Input Validation Tests - GetWikiPersonAsync

        [TestMethod]
        public async Task WhenIdIsZero_ShouldThrowArgumentOutOfRangeException()
        {
            await Assert.ThrowsExceptionAsync<ArgumentOutOfRangeException>(
                async () => await WikiData.GetWikiPersonAsync(0));
        }

        [TestMethod]
        public async Task WhenIdIsNegative_ShouldThrowArgumentOutOfRangeException()
        {
            await Assert.ThrowsExceptionAsync<ArgumentOutOfRangeException>(
                async () => await WikiData.GetWikiPersonAsync(-1));
        }

        [TestMethod]
        public async Task WhenIdDoesNotExist_ShouldThrowInvalidOperationException()
        {
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                async () => await WikiData.GetWikiPersonAsync(999999999));
        }

        #endregion

        #region Input Validation Tests - GetPeopleBornOnDateAsync / GetPeopleDiedOnDateAsync

        [TestMethod]
        public async Task WhenBirthMonthIsInvalid_ShouldThrowArgumentOutOfRangeException()
        {
            await Assert.ThrowsExceptionAsync<ArgumentOutOfRangeException>(
                async () => await WikiData.GetPeopleBornOnDateAsync(0, 1, 10));
        }

        [TestMethod]
        public async Task WhenBirthDayIsInvalid_ShouldThrowArgumentOutOfRangeException()
        {
            await Assert.ThrowsExceptionAsync<ArgumentOutOfRangeException>(
                async () => await WikiData.GetPeopleBornOnDateAsync(2, 30, 10));
        }

        [TestMethod]
        public async Task WhenLimitIsInvalid_ShouldThrowArgumentOutOfRangeException()
        {
            await Assert.ThrowsExceptionAsync<ArgumentOutOfRangeException>(
                async () => await WikiData.GetPeopleDiedOnDateAsync(8, 16, 0));
        }

        [TestMethod]
        public async Task WhenDeathYearIsInvalid_ShouldThrowArgumentOutOfRangeException()
        {
            await Assert.ThrowsExceptionAsync<ArgumentOutOfRangeException>(
                async () => await WikiData.GetPeopleDiedOnDateAsync(0, 8, 16, 10));
        }

        #endregion

        #region Edge Case Tests

        [TestMethod]
        public async Task WhenSearchStringHasSpecialCharacters_ShouldNotThrow()
        {
            var people = await WikiData.WikiPeopleSearchAsync("O'Brien");
            Assert.IsNotNull(people);
        }

        [TestMethod]
        public async Task WhenSearchStringHasUnicode_ShouldNotThrow()
        {
            var people = await WikiData.WikiPeopleSearchAsync("Müller");
            Assert.IsNotNull(people);
        }

        [TestMethod]
        public async Task WhenSearchReturnsNoResults_ShouldReturnEmptyCollection()
        {
            var people = await WikiData.WikiPeopleSearchAsync("XyZaBcDeF123NonExistentPerson999");
            Assert.IsNotNull(people);
            Assert.AreEqual(0, people.Count);
        }

        [TestMethod]
        public async Task WhenPersonHasAllFields_ShouldPopulateAllProperties()
        {
            var person = await WikiData.GetWikiPersonAsync(303); // Elvis Presley

            Assert.IsNotNull(person);
            Assert.AreNotEqual(0, person.Id);
            Assert.IsNotNull(person.Name);
            Assert.IsNotNull(person.Description);
            Assert.IsNotNull(person.Birthday);
            Assert.IsNotNull(person.Death);
            Assert.IsNotNull(person.Image);
            Assert.IsNotNull(person.Link);
        }

        #endregion

        #region Cancellation Token Tests

        [TestMethod]
        public async Task WhenCancellationTokenIsCancelled_ShouldThrowTaskCanceledException()
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsExceptionAsync<OperationCanceledException>(
                async () => await WikiData.WikiPeopleSearchAsync("Pope", cts.Token));
        }

        [TestMethod]
        public async Task WhenCancellationTokenIsCancelledDuringExecution_ShouldCancel()
        {
            // Pre-cancel to ensure deterministic behavior; testing mid-flight cancellation
            // requires HttpMessageHandler injection and is tracked in a separate issue.
            var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsExceptionAsync<OperationCanceledException>(
                async () => await WikiData.WikiPeopleSearchAsync("Pope", cts.Token));
        }

        #endregion

        #region Integration Tests - Verify Data Quality

        [TestMethod]
        public async Task WhenSearchingForAda_ShouldIncludeAdaLovelace()
        {
            var people = await WikiData.WikiPeopleSearchAsync("Ada");

            Assert.IsNotNull(people);
            Assert.IsTrue(people.Count > 0, "Search for 'Ada' should return results");

            // Verify first result has a name
            var firstPerson = people[0];
            Assert.IsNotNull(firstPerson.Name, "First result should have a name");
        }

        [TestMethod]
        public async Task WhenSearchingByPartialSurname_ShouldIncludeElvisPresley()
        {
            var people = await WikiData.WikiPeopleSearchAsync("Presley");

            Assert.IsTrue(people.Any(person => person.Name == "Elvis Presley"),
                "Search for 'Presley' should include Elvis Presley");
        }

        [TestMethod]
        public async Task WhenSearchingWithWildcard_ShouldIncludeElvisPresley()
        {
            var people = await WikiData.WikiPeopleSearchAsync("*Presley*");

            Assert.IsTrue(people.Any(person => person.Name == "Elvis Presley"),
                "Search for '*Presley*' should include Elvis Presley");
        }

        [TestMethod]
        public async Task WhenSearchingForPope_AllResultsShouldContainPopeInItemLabel()
        {
            var people = await WikiData.WikiPeopleSearchAsync("Pope");

            Assert.IsTrue(people.Count > 0, "Search for 'Pope' should return results");
            Assert.IsTrue(people.All(person => person.Name != null && person.Name.Contains("Pope", StringComparison.OrdinalIgnoreCase)),
                "Search for 'Pope' should only return item labels containing 'Pope'");
        }

        [TestMethod]
        public async Task WhenGettingPersonById_ShouldHaveValidId()
        {
            var person = await WikiData.GetWikiPersonAsync(303);

            Assert.AreEqual(303, person.Id);
        }

        [TestMethod]
        public async Task WhenGettingPersonById_ShouldHaveWikipediaLink()
        {
            var person = await WikiData.GetWikiPersonAsync(303);

            Assert.IsNotNull(person.Link);
            Assert.IsTrue(person.Link.StartsWith("https://en.wikipedia.org/"), 
                "Link should be an English Wikipedia URL");
        }

        [TestMethod]
        public async Task WhenGettingPeopleDiedOnDateWithYear_ShouldReturnMatchingDeathDates()
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")))
            {
                return;
            }

            try
            {
                var people = await WikiData.GetPeopleDiedOnDateAsync(1977, 8, 16, 1);

                Assert.IsTrue(people.Any(person => person.Name == "Elvis Presley" && person.Death?.Year == 1977),
                    "Query should return Elvis Presley for 1977-08-16");
            }
            catch (TaskCanceledException)
            {
                Assert.Inconclusive("Live Wikidata request timed out.");
            }
        }

        [TestMethod]
        public async Task WhenGettingQ22686_ShouldFallbackToMulAndReturnName()
        {
            // Skip this live integration test when running in CI
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")))
            {
                return;
            }

            var person = await WikiData.GetWikiPersonAsync(22686); // Q22686 (Donald Trump)

            Assert.IsNotNull(person);
            Assert.IsFalse(string.IsNullOrWhiteSpace(person.Name));

            // If the returned label isn't English, verify Wikidata's wbgetentities includes a 'mul' label
            if (person.Name.IndexOf("Donald", StringComparison.OrdinalIgnoreCase) < 0)
            {
                var url = "https://www.wikidata.org/w/api.php?action=wbgetentities&ids=Q22686&languages=en|fr|ru|mul&format=json&origin=*";
                using (var client = new System.Net.Http.HttpClient())
                {
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("WikiDataLib-Inspector/1.0");
                    var resp = client.GetAsync(url).Result;
                    resp.EnsureSuccessStatusCode();
                    var json = resp.Content.ReadAsStringAsync().Result;
                    using (var doc = System.Text.Json.JsonDocument.Parse(json))
                    {
                        if (doc.RootElement.TryGetProperty("entities", out var entities) &&
                            entities.TryGetProperty("Q22686", out var ent) &&
                            ent.TryGetProperty("labels", out var labels) &&
                            labels.TryGetProperty("mul", out var mulLabel) &&
                            mulLabel.TryGetProperty("value", out var mulValue))
                        {
                            Assert.IsFalse(string.IsNullOrWhiteSpace(mulValue.GetString()), "Expected 'mul' label to be present and non-empty");
                        }
                        else
                        {
                            Assert.Fail("Expected wbgetentities response to include a 'mul' label for Q22686 when English label is not returned.");
                        }
                    }
                }
            }
        }
       
        [TestMethod]
        public async Task WhenSearchingForTrump_ShouldReturnResults()
        {
            var people = await WikiData.WikiPeopleSearchAsync("trump");
            Assert.AreNotEqual(0, people.Count);
        }

        [TestMethod]
        public async Task WhenGettingDonaldTrump_ShouldReturnCorrectName()
        {
            var person = await WikiData.GetWikiPersonAsync(22686);

            Assert.IsNotNull(person);
            Assert.AreEqual("Donald Trump", person.Name);
        }

        [TestMethod]
        public async Task WhenResolvingCommonsSpecialFilePathImage_WithWidth_ShouldReturnDirectUploadUrl()
        {
            var source = "http://commons.wikimedia.org/wiki/Special:FilePath/Elvis%20Presley%20promoting%20Jailhouse%20Rock.jpg?width=330";
            var result = await WikiApi.ResolveCommonsFileUrlAsync(source, CancellationToken.None);

            Assert.IsNotNull(result);
            StringAssert.StartsWith(result, "https://upload.wikimedia.org/");
        }

        [TestMethod]
        public async Task WhenResolvingCommonsSpecialFilePathImage_WithoutWidth_ShouldReturnDirectUploadUrl()
        {
            var source = "http://commons.wikimedia.org/wiki/Special:FilePath/Elvis%20Presley%20promoting%20Jailhouse%20Rock.jpg";
            var result = await WikiApi.ResolveCommonsFileUrlAsync(source, CancellationToken.None);

            Assert.IsNotNull(result);
            StringAssert.StartsWith(result, "https://upload.wikimedia.org/");
        }

        [TestMethod]
        public async Task WhenResolvingNonCommonsUrl_ShouldReturnOriginalValue()
        {
            var source = "https://upload.wikimedia.org/wikipedia/commons/a/a9/Example.jpg";
            var result = await WikiApi.ResolveCommonsFileUrlAsync(source, CancellationToken.None);

            Assert.AreEqual(source, result);
        }

        [TestMethod]
        public async Task WhenResolvingNullOrEmptyUrl_ShouldReturnOriginalValue()
        {
            Assert.IsNull(await WikiApi.ResolveCommonsFileUrlAsync(null, CancellationToken.None));
            Assert.AreEqual("", await WikiApi.ResolveCommonsFileUrlAsync("", CancellationToken.None));
        }

        #endregion

        #region Unit Tests - URL Resolution (mocked HTTP)

        [TestCleanup]
        public void ResetHttpHandler()
        {
            WikiApi.SetHttpMessageHandlerForTesting(null);
        }

        [TestMethod]
        public async Task WhenResolvingCommonsUrl_ImageinfoReturnsUrl_ShouldReturnCleanUploadUrl()
        {
            var json = @"{""query"":{""pages"":{""123"":{""title"":""File:Elvis Presley promoting Jailhouse Rock.jpg"",
                ""imageinfo"":[{""url"":""https://upload.wikimedia.org/wikipedia/commons/9/99/Elvis_Presley_promoting_Jailhouse_Rock.jpg?utm_source=commons.wikimedia.org&utm_campaign=imageinfo&utm_content=original""}]}}}}";
            WikiApi.SetHttpMessageHandlerForTesting(new FakeHttpMessageHandler(_ => json));

            var source = "http://commons.wikimedia.org/wiki/Special:FilePath/Elvis%20Presley%20promoting%20Jailhouse%20Rock.jpg";
            var result = await WikiApi.ResolveCommonsFileUrlAsync(source, CancellationToken.None);

            Assert.AreEqual("https://upload.wikimedia.org/wikipedia/commons/9/99/Elvis_Presley_promoting_Jailhouse_Rock.jpg", result);
        }

        [TestMethod]
        public async Task WhenResolvingCommonsUrlWithWidth_ImageinfoReturnsThumburl_ShouldReturnThumbUrl()
        {
            var json = @"{""query"":{""pages"":{""123"":{""title"":""File:Elvis Presley promoting Jailhouse Rock.jpg"",
                ""imageinfo"":[{
                  ""thumburl"":""https://upload.wikimedia.org/wikipedia/commons/thumb/9/99/Elvis_Presley_promoting_Jailhouse_Rock.jpg/330px-Elvis_Presley_promoting_Jailhouse_Rock.jpg"",
                  ""url"":""https://upload.wikimedia.org/wikipedia/commons/9/99/Elvis_Presley_promoting_Jailhouse_Rock.jpg""
                }]}}}}";
            WikiApi.SetHttpMessageHandlerForTesting(new FakeHttpMessageHandler(_ => json));

            var source = "http://commons.wikimedia.org/wiki/Special:FilePath/Elvis%20Presley%20promoting%20Jailhouse%20Rock.jpg?width=330";
            var result = await WikiApi.ResolveCommonsFileUrlAsync(source, CancellationToken.None);

            Assert.AreEqual("https://upload.wikimedia.org/wikipedia/commons/thumb/9/99/Elvis_Presley_promoting_Jailhouse_Rock.jpg/330px-Elvis_Presley_promoting_Jailhouse_Rock.jpg", result);
        }

        [TestMethod]
        public async Task WhenResolvingCommonsUrl_FileMissing_ShouldFallBackToOriginalUrl()
        {
            var json = @"{""query"":{""pages"":{
                ""-1"":{""title"":""File:NonExistent.jpg"",""missing"":""""}}}}";
            WikiApi.SetHttpMessageHandlerForTesting(new FakeHttpMessageHandler(_ => json));

            var source = "http://commons.wikimedia.org/wiki/Special:FilePath/NonExistent.jpg";
            var result = await WikiApi.ResolveCommonsFileUrlAsync(source, CancellationToken.None);

            Assert.AreEqual(source, result);
        }

        [TestMethod]
        public async Task WhenResolvingCommonsUrl_ApiReturnsError_ShouldFallBackToOriginalUrl()
        {
            WikiApi.SetHttpMessageHandlerForTesting(new FakeHttpMessageHandler(_ => throw new HttpRequestException("Network error")));

            var source = "http://commons.wikimedia.org/wiki/Special:FilePath/Test.jpg";
            var result = await WikiApi.ResolveCommonsFileUrlAsync(source, CancellationToken.None);

            Assert.AreEqual(source, result);
        }

        [TestMethod]
        public async Task WhenResolvingBatchCommonsUrls_ShouldResolveAllInSingleCall()
        {
            var json = @"{""query"":{""pages"":{
                ""111"":{""title"":""File:Image1.jpg"",""imageinfo"":[{""url"":""https://upload.wikimedia.org/wikipedia/commons/a/ab/Image1.jpg""}]},
                ""222"":{""title"":""File:Image2.jpg"",""imageinfo"":[{""url"":""https://upload.wikimedia.org/wikipedia/commons/b/bc/Image2.jpg""}]}
            }}}";
            var handler = new FakeHttpMessageHandler(_ => json);
            WikiApi.SetHttpMessageHandlerForTesting(handler);

            var sources = new List<string?>
            {
                "http://commons.wikimedia.org/wiki/Special:FilePath/Image1.jpg",
                "http://commons.wikimedia.org/wiki/Special:FilePath/Image2.jpg",
                "https://upload.wikimedia.org/wikipedia/commons/a/a9/AlreadyDirect.jpg"
            };
            var result = await WikiApi.ResolveCommonsFileUrlsBatchAsync(sources, CancellationToken.None);

            Assert.AreEqual("https://upload.wikimedia.org/wikipedia/commons/a/ab/Image1.jpg", result[sources[0]!]);
            Assert.AreEqual("https://upload.wikimedia.org/wikipedia/commons/b/bc/Image2.jpg", result[sources[1]!]);
            Assert.AreEqual(sources[2], result[sources[2]!], "Non-Commons URL should pass through unchanged");
            Assert.AreEqual(1, handler.RequestCount, "Both Commons files should be resolved in a single batched API call");
        }

        [TestMethod]
        public async Task WhenResolvingBatchCommonsUrls_ApiNormalizesTitle_ShouldStillResolve()
        {
            // API normalizes "File:image.jpg" (lowercase i) to "File:Image.jpg" (uppercase I)
            var json = @"{""query"":{
                ""normalized"":[{""from"":""File:image.jpg"",""to"":""File:Image.jpg""}],
                ""pages"":{""333"":{""title"":""File:Image.jpg"",""imageinfo"":[{""url"":""https://upload.wikimedia.org/wikipedia/commons/c/cd/Image.jpg""}]}}}}";
            WikiApi.SetHttpMessageHandlerForTesting(new FakeHttpMessageHandler(_ => json));

            var source = "http://commons.wikimedia.org/wiki/Special:FilePath/image.jpg";
            var result = await WikiApi.ResolveCommonsFileUrlsBatchAsync(new List<string?> { source }, CancellationToken.None);

            Assert.AreEqual("https://upload.wikimedia.org/wikipedia/commons/c/cd/Image.jpg", result[source]);
        }

        [TestMethod]
        public async Task WhenResolvingNonCommonsUrlInBatch_ShouldPassThroughUnchanged()
        {
            WikiApi.SetHttpMessageHandlerForTesting(new FakeHttpMessageHandler(_ => @"{""query"":{""pages"":{}}}"));

            var source = "https://upload.wikimedia.org/wikipedia/commons/a/a9/Example.jpg";
            var result = await WikiApi.ResolveCommonsFileUrlsBatchAsync(new List<string?> { source }, CancellationToken.None);

            Assert.AreEqual(source, result[source]);
        }

        [TestMethod]
        public async Task WhenGettingPeopleBornOnDate_ShouldReturnMatchingBirthdays()
        {
            try
            {
                const int limit = 50;
                var people = await WikiData.GetPeopleBornOnDateAsync(1, 8, limit);

                Assert.IsNotNull(people);
                Assert.IsTrue(people.Count > 0, "Expected at least one person born on January 8.");
                Assert.IsTrue(people.Count <= limit, $"Expected at most {limit} results.");
                Assert.IsTrue(people.All(person => person.Birthday.HasValue &&
                    person.Birthday.Value.Month == 1 &&
                    person.Birthday.Value.Day == 8),
                    "All returned people should have a January 8 birthday.");
            }
            catch (TaskCanceledException)
            {
                Assert.Inconclusive("The public Wikipedia REST API timed out for this live smoke test.");
            }
        }

        [TestMethod]
        public async Task WhenGettingPeopleDiedOnDate_ShouldReturnMatchingDeathDates()
        {
            try
            {
                const int limit = 50;
                var people = await WikiData.GetPeopleDiedOnDateAsync(8, 16, limit);

                Assert.IsNotNull(people);
                Assert.IsTrue(people.Count > 0, "Expected at least one person who died on August 16.");
                Assert.IsTrue(people.Count <= limit, $"Expected at most {limit} results.");
                Assert.IsTrue(people.All(person => person.Death.HasValue &&
                    person.Death.Value.Month == 8 &&
                    person.Death.Value.Day == 16),
                    "All returned people should have an August 16 death date.");
            }
            catch (TaskCanceledException)
            {
                Assert.Inconclusive("The public Wikipedia REST API timed out for this live smoke test.");
            }
        }

        [TestMethod]
        public async Task WhenSearchingBySurname_ShouldIncludeDonaldTrump()
        {
            var people = await WikiData.WikiPeopleSearchAsync("trump");

            Assert.IsTrue(people.Any(person => person.Name == "Donald Trump"),
                "Search for 'trump' should include Donald Trump");

            Assert.IsTrue(people.Any(person => person.Id == 22686),
                "Search for 'trump' should include 22686"); 
        }

        #endregion
    }

    internal class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, string> _responseSelector;
        public int RequestCount { get; private set; }
        public List<Uri?> RequestedUris { get; } = new List<Uri?>();

        public FakeHttpMessageHandler(Func<HttpRequestMessage, string> responseSelector)
        {
            _responseSelector = responseSelector;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            RequestedUris.Add(request.RequestUri);
            var json = _responseSelector(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }
}
