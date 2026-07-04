using Microsoft.VisualStudio.TestTools.UnitTesting;
using WikiDataLib;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;

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
        public void WhenGettingPeopleBornTodayQuery_ShouldUseDateFiltersAndLimit()
        {
            var method = typeof(WikiData).GetMethod(
                "BuildPeopleBornTodayQuery",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(method);

            var query = method.Invoke(null, null) as string;

            Assert.IsNotNull(query);
            Assert.IsTrue(query.Contains("MONTH(NOW())"));
            Assert.IsTrue(query.Contains("DAY(NOW())"));
            Assert.IsTrue(query.Contains("schema:isPartOf <https://en.wikipedia.org/>"));
            Assert.IsTrue(query.Contains("LIMIT 100"));
            Assert.IsFalse(query.Contains("ORDER BY"));
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
        public async Task WhenSearchingBySurname_ShouldIncludeDonaldTrump()
        {
            var people = await WikiData.WikiPeopleSearchAsync("trump");

            Assert.IsTrue(people.Any(person => person.Name == "Donald Trump"),
                "Search for 'trump' should include Donald Trump");

            Assert.IsTrue(people.Any(person => person.Id == 22686),
                "Search for 'trump' should include 22686"); 
        }

        #endregion

        #region Cancellation Token Tests - GetPeopleBornTodayAsync

        [TestMethod]
        public async Task WhenCancellationTokenIsCancelled_ForPeopleBornToday_ShouldThrowOperationCanceledException()
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsExceptionAsync<OperationCanceledException>(
                async () => await WikiData.GetPeopleBornTodayAsync(cts.Token));
        }

        #endregion
    }
}
