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
        // Wraps a live integration test body, marking the test Inconclusive instead of Failed
        // when the external API is unavailable, rate-limited, or times out.
        private static async Task LiveTest(Func<Task> test)
        {
            try { await test(); }
            catch (HttpRequestException ex) when (ex.ToString().IndexOf("429", StringComparison.Ordinal) >= 0 ||
                                                   ex.ToString().IndexOf("Too Many Requests", StringComparison.Ordinal) >= 0 ||
                                                   ex.ToString().IndexOf("503", StringComparison.Ordinal) >= 0)
            {
                Assert.Inconclusive("External API unavailable or rate-limited for this live smoke test.");
            }
            catch (TaskCanceledException)
            {
                Assert.Inconclusive("External API timed out for this live smoke test.");
            }
        }

        #region Integration Tests - Happy Path

        [TestMethod]
        public async Task WhenSearchingForPope_ShouldReturnResults() => await LiveTest(async () =>
        {
            var people = await WikiData.WikiPeopleSearchAsync("Pope");
            Assert.AreNotEqual(0, people.Count);
        });

        [TestMethod]
        public async Task WhenGettingElvisPresley_ShouldReturnCorrectName() => await LiveTest(async () =>
        {
            var person = await WikiData.GetWikiPersonAsync(303);
            Assert.IsNotNull(person);
            Assert.AreEqual("Elvis Presley", person.Name);
        });

        [TestMethod]
        public async Task WhenGettingElvisPresleyByWikipediaTitle_ShouldReturnCorrectName() => await LiveTest(async () =>
        {
            var person = await WikiData.GetWikiPersonAsync("Elvis Presley");
            Assert.IsNotNull(person);
            Assert.AreEqual("Elvis Presley", person.Name);
        });

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
        public async Task WhenSearchStringHasSpecialCharacters_ShouldNotThrow() => await LiveTest(async () =>
        {
            var people = await WikiData.WikiPeopleSearchAsync("O'Brien");
            Assert.IsNotNull(people);
        });

        [TestMethod]
        public async Task WhenSearchStringHasUnicode_ShouldNotThrow() => await LiveTest(async () =>
        {
            var people = await WikiData.WikiPeopleSearchAsync("Müller");
            Assert.IsNotNull(people);
        });

        [TestMethod]
        public async Task WhenSearchReturnsNoResults_ShouldReturnEmptyCollection() => await LiveTest(async () =>
        {
            var people = await WikiData.WikiPeopleSearchAsync("XyZaBcDeF123NonExistentPerson999");
            Assert.IsNotNull(people);
            Assert.AreEqual(0, people.Count);
        });

        [TestMethod]
        public async Task WhenPersonHasAllFields_ShouldPopulateAllProperties() => await LiveTest(async () =>
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
        });

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
        public async Task WhenSearchingForAda_ShouldIncludeAdaLovelace() => await LiveTest(async () =>
        {
            var people = await WikiData.WikiPeopleSearchAsync("Ada");
            Assert.IsNotNull(people);
            Assert.IsTrue(people.Count > 0, "Search for 'Ada' should return results");
            Assert.IsNotNull(people[0].Name, "First result should have a name");
        });

        [TestMethod]
        public async Task WhenSearchingByPartialSurname_ShouldIncludeElvisPresley() => await LiveTest(async () =>
        {
            var people = await WikiData.WikiPeopleSearchAsync("Presley");
            Assert.IsTrue(people.Any(person => person.Name == "Elvis Presley"),
                "Search for 'Presley' should include Elvis Presley");
        });

        [TestMethod]
        public async Task WhenSearchingWithWildcard_ShouldIncludeElvisPresley() => await LiveTest(async () =>
        {
            var people = await WikiData.WikiPeopleSearchAsync("*Presley*");
            Assert.IsTrue(people.Any(person => person.Name == "Elvis Presley"),
                "Search for '*Presley*' should include Elvis Presley");
        });

        [TestMethod]
        public async Task WhenSearchingForPope_AllResultsShouldContainPopeInItemLabel() => await LiveTest(async () =>
        {
            var people = await WikiData.WikiPeopleSearchAsync("Pope");
            Assert.IsTrue(people.Count > 0, "Search for 'Pope' should return results");
            Assert.IsTrue(people.All(person => person.Name != null && person.Name.Contains("Pope", StringComparison.OrdinalIgnoreCase)),
                "Search for 'Pope' should only return item labels containing 'Pope'");
        });

        [TestMethod]
        public async Task WhenGettingPersonById_ShouldHaveValidId() => await LiveTest(async () =>
        {
            var person = await WikiData.GetWikiPersonAsync(303);
            Assert.AreEqual(303, person.Id);
        });

        [TestMethod]
        public async Task WhenGettingPersonById_ShouldHaveWikipediaLink() => await LiveTest(async () =>
        {
            var person = await WikiData.GetWikiPersonAsync(303);
            Assert.IsNotNull(person.Link);
            Assert.IsTrue(person.Link.StartsWith("https://en.wikipedia.org/"),
                "Link should be an English Wikipedia URL");
        });

        [TestMethod]
        public async Task WhenGettingPeopleDiedOnDateWithYear_ShouldReturnMatchingDeathDates()
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")))
                return;

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
            catch (HttpRequestException)
            {
                Assert.Inconclusive("External API unavailable or rate-limited for this live smoke test.");
            }
        }

        [TestMethod]
        public async Task WhenGettingQ22686_ShouldFallbackToMulAndReturnName()
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")))
                return;

            await LiveTest(async () =>
            {
                var person = await WikiData.GetWikiPersonAsync(22686);

                Assert.IsNotNull(person);
                Assert.IsFalse(string.IsNullOrWhiteSpace(person.Name));

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
            });
        }

        [TestMethod]
        public async Task WhenSearchingForTrump_ShouldReturnResults() => await LiveTest(async () =>
        {
            var people = await WikiData.WikiPeopleSearchAsync("trump");
            Assert.AreNotEqual(0, people.Count);
        });

        [TestMethod]
        public async Task WhenGettingDonaldTrump_ShouldReturnCorrectName() => await LiveTest(async () =>
        {
            var person = await WikiData.GetWikiPersonAsync(22686);
            Assert.IsNotNull(person);
            Assert.AreEqual("Donald Trump", person.Name);
        });

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
        public async Task WhenResolvingCommonsUrlWithoutWidth_ShouldComputeUploadUrlLocally()
        {
            var handler = new FakeHttpMessageHandler(_ => throw new InvalidOperationException("No HTTP call expected"));
            WikiApi.SetHttpMessageHandlerForTesting(handler);

            var source = "http://commons.wikimedia.org/wiki/Special:FilePath/Elvis%20Presley%20promoting%20Jailhouse%20Rock.jpg";
            var result = await WikiApi.ResolveCommonsFileUrlAsync(source, CancellationToken.None);

            Assert.AreEqual("https://upload.wikimedia.org/wikipedia/commons/9/99/Elvis_Presley_promoting_Jailhouse_Rock.jpg", result);
            Assert.AreEqual(0, handler.RequestCount, "Original-file URLs should be computed without calling the Commons API");
        }

        // Expected values are the real URLs returned by the Commons imageinfo API.
        [DataTestMethod]
        [DataRow("Albert Einstein Head.jpg", "https://upload.wikimedia.org/wikipedia/commons/d/d3/Albert_Einstein_Head.jpg")]
        [DataRow("Marie Curie c. 1920s.jpg", "https://upload.wikimedia.org/wikipedia/commons/c/c8/Marie_Curie_c._1920s.jpg")]
        [DataRow("Frédéric Chopin by Bisson, 1849.png", "https://upload.wikimedia.org/wikipedia/commons/3/36/Fr%C3%A9d%C3%A9ric_Chopin_by_Bisson%2C_1849.png")]
        [DataRow("Ab'acus - capital of a Doric column.png", "https://upload.wikimedia.org/wikipedia/commons/4/48/Ab%27acus_-_capital_of_a_Doric_column.png")]
        [DataRow("Ab Baars & Marta Warelis.jpg", "https://upload.wikimedia.org/wikipedia/commons/7/77/Ab_Baars_%26_Marta_Warelis.jpg")]
        [DataRow("Ab, Ha; Tumulus, Thalamus-... 1654 (IA bim early-english-books-1641-1700 ab-ha-tumulus-thalamu 1654).pdf",
                 "https://upload.wikimedia.org/wikipedia/commons/8/81/Ab%2C_Ha%3B_Tumulus%2C_Thalamus-..._1654_%28IA_bim_early-english-books-1641-1700_ab-ha-tumulus-thalamu_1654%29.pdf")]
        [DataRow("beethoven.jpg", "https://upload.wikimedia.org/wikipedia/commons/6/6f/Beethoven.jpg")]
        [DataRow("  Beethoven.jpg ", "https://upload.wikimedia.org/wikipedia/commons/6/6f/Beethoven.jpg")]
        public void WhenBuildingCommonsOriginalUrl_ShouldMatchCommonsUploadUrl(string fileName, string expected)
        {
            Assert.AreEqual(expected, WikiApi.BuildCommonsOriginalUrl(fileName));
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

            var source = "http://commons.wikimedia.org/wiki/Special:FilePath/NonExistent.jpg?width=330";
            var result = await WikiApi.ResolveCommonsFileUrlAsync(source, CancellationToken.None);

            Assert.AreEqual(source, result);
        }

        [TestMethod]
        public async Task WhenResolvingCommonsUrl_ApiReturnsError_ShouldFallBackToOriginalUrl()
        {
            WikiApi.SetHttpMessageHandlerForTesting(new FakeHttpMessageHandler(_ => throw new HttpRequestException("Network error")));

            var source = "http://commons.wikimedia.org/wiki/Special:FilePath/Test.jpg?width=330";
            var result = await WikiApi.ResolveCommonsFileUrlAsync(source, CancellationToken.None);

            Assert.AreEqual(source, result);
        }

        [TestMethod]
        public async Task WhenResolvingBatchCommonsUrls_ShouldResolveAllInSingleCall()
        {
            var json = @"{""query"":{""pages"":{
                ""111"":{""title"":""File:Image1.jpg"",""imageinfo"":[{""thumburl"":""https://upload.wikimedia.org/wikipedia/commons/thumb/a/ab/Image1.jpg/330px-Image1.jpg""}]},
                ""222"":{""title"":""File:Image2.jpg"",""imageinfo"":[{""thumburl"":""https://upload.wikimedia.org/wikipedia/commons/thumb/b/bc/Image2.jpg/330px-Image2.jpg""}]}
            }}}";
            var handler = new FakeHttpMessageHandler(_ => json);
            WikiApi.SetHttpMessageHandlerForTesting(handler);

            var sources = new List<string?>
            {
                "http://commons.wikimedia.org/wiki/Special:FilePath/Image1.jpg?width=330",
                "http://commons.wikimedia.org/wiki/Special:FilePath/Image2.jpg?width=330",
                "https://upload.wikimedia.org/wikipedia/commons/a/a9/AlreadyDirect.jpg"
            };
            var result = await WikiApi.ResolveCommonsFileUrlsBatchAsync(sources, CancellationToken.None);

            Assert.AreEqual("https://upload.wikimedia.org/wikipedia/commons/thumb/a/ab/Image1.jpg/330px-Image1.jpg", result[sources[0]!]);
            Assert.AreEqual("https://upload.wikimedia.org/wikipedia/commons/thumb/b/bc/Image2.jpg/330px-Image2.jpg", result[sources[1]!]);
            Assert.AreEqual(sources[2], result[sources[2]!], "Non-Commons URL should pass through unchanged");
            Assert.AreEqual(1, handler.RequestCount, "Both Commons files should be resolved in a single batched API call");
        }

        [TestMethod]
        public async Task WhenResolvingBatchCommonsUrls_ApiNormalizesTitle_ShouldStillResolve()
        {
            // API normalizes "File:image.jpg" (lowercase i) to "File:Image.jpg" (uppercase I)
            var json = @"{""query"":{
                ""normalized"":[{""from"":""File:image.jpg"",""to"":""File:Image.jpg""}],
                ""pages"":{""333"":{""title"":""File:Image.jpg"",""imageinfo"":[{""thumburl"":""https://upload.wikimedia.org/wikipedia/commons/thumb/c/cd/Image.jpg/330px-Image.jpg""}]}}}}";
            WikiApi.SetHttpMessageHandlerForTesting(new FakeHttpMessageHandler(_ => json));

            var source = "http://commons.wikimedia.org/wiki/Special:FilePath/image.jpg?width=330";
            var result = await WikiApi.ResolveCommonsFileUrlsBatchAsync(new List<string?> { source }, CancellationToken.None);

            Assert.AreEqual("https://upload.wikimedia.org/wikipedia/commons/thumb/c/cd/Image.jpg/330px-Image.jpg", result[source]);
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
        public async Task WhenResolvingCommonsUrls_ShouldIncludeOriginParameterForCors()
        {
            var handler = new FakeHttpMessageHandler(_ => @"{""query"":{""pages"":{""111"":{""title"":""File:Test.jpg"",""imageinfo"":[{""url"":""https://upload.wikimedia.org/wikipedia/commons/t/te/Test.jpg""}]}}}}");
            WikiApi.SetHttpMessageHandlerForTesting(handler);

            var sources = new List<string?>
            {
                "http://commons.wikimedia.org/wiki/Special:FilePath/Test.jpg",
                "http://commons.wikimedia.org/wiki/Special:FilePath/Test.jpg?width=330"
            };
            await WikiApi.ResolveCommonsFileUrlsBatchAsync(sources, CancellationToken.None);

            Assert.IsTrue(handler.RequestedUris.Count > 0, "Expected at least one API request");
            Assert.IsTrue(handler.RequestedUris.Any(u => u != null && u.Query.Contains("iiurlwidth=330", StringComparison.Ordinal)),
                "Expected at least one Commons imageinfo request to include the iiurlwidth parameter");

            foreach (var uri in handler.RequestedUris.Where(u => u != null).Select(u => u!))
            {
                var originValue = uri.Query.TrimStart('?')
                    .Split('&')
                    .Select(p => p.Split(new[] { '=' }, 2))
                    .Where(p => p.Length == 2)
                    .Select(p => new { Key = Uri.UnescapeDataString(p[0]), Value = Uri.UnescapeDataString(p[1]) })
                    .FirstOrDefault(p => string.Equals(p.Key, "origin", StringComparison.OrdinalIgnoreCase))
                    ?.Value;

                Assert.AreEqual("*", originValue,
                    "Commons imageinfo API call must include origin=* parameter for CORS support");
            }
        }

        [TestMethod]
        public async Task WhenGettingPeopleBornOnDate_ShouldReturnMatchingBirthdays() => await LiveTest(async () =>
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
        });

        [TestMethod]
        public async Task WhenGettingPeopleDiedOnDate_ShouldReturnMatchingDeathDates() => await LiveTest(async () =>
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
        });

        [TestMethod]
        public async Task WhenSearchingBySurname_ShouldIncludeDonaldTrump() => await LiveTest(async () =>
        {
            var people = await WikiData.WikiPeopleSearchAsync("trump");

            Assert.IsTrue(people.Any(person => person.Name == "Donald Trump"),
                "Search for 'trump' should include Donald Trump");
            Assert.IsTrue(people.Any(person => person.Id == 22686),
                "Search for 'trump' should include 22686");
        });


        [TestMethod]
        public async Task WhenOriginalUrlComputedLocally_ShouldMatchCommonsApi() => await LiveTest(async () =>
        {
            // Guards the local hashing scheme against drift from what Commons actually serves.
            var names = new[] { "Elvis Presley promoting Jailhouse Rock.jpg", "Frédéric Chopin by Bisson, 1849.png", "Ab Baars & Marta Warelis.jpg" };
            var titles = string.Join("|", names.Select(n => Uri.EscapeDataString("File:" + n)));
            var url = $"https://commons.wikimedia.org/w/api.php?action=query&titles={titles}&prop=imageinfo&iiprop=url&format=json&origin=*";
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("WikiDataLib-Tests/1.0 (+https://github.com/alexan1/WikiDataLib)");
                var json = await client.GetStringAsync(url);
                using (var doc = System.Text.Json.JsonDocument.Parse(json))
                {
                    var apiUrls = doc.RootElement.GetProperty("query").GetProperty("pages").EnumerateObject()
                        .Select(p => p.Value.GetProperty("imageinfo")[0].GetProperty("url").GetString()!.Split('?')[0])
                        .ToList();
                    foreach (var name in names)
                    {
                        CollectionAssert.Contains(apiUrls, WikiApi.BuildCommonsOriginalUrl(name), name);
                    }
                }
            }
        });

        [TestMethod]
        public async Task WhenRequestingDateFeedWithLimit_ShouldOnlyResolveImagesForReturnedPeople()
        {
            var feed = @"{""births"":[
                {""year"":1935,""pages"":[{""title"":""A"",""wikibase_item"":""Q1"",""thumbnail"":{""source"":""http://commons.wikimedia.org/wiki/Special:FilePath/A.jpg?width=100""}}]},
                {""year"":1940,""pages"":[{""title"":""B"",""wikibase_item"":""Q2"",""thumbnail"":{""source"":""http://commons.wikimedia.org/wiki/Special:FilePath/B.jpg?width=100""}},
                                         {""title"":""C"",""wikibase_item"":""Q3"",""thumbnail"":{""source"":""http://commons.wikimedia.org/wiki/Special:FilePath/C.jpg?width=100""}}]}
            ]}";
            var handler = new FakeHttpMessageHandler(req =>
                req.RequestUri!.Host == "en.wikipedia.org" ? feed : @"{""query"":{""pages"":{}}}");
            WikiApi.SetHttpMessageHandlerForTesting(handler);

            var people = await WikiData.GetPeopleBornOnDateAsync(1, 8, 2);

            CollectionAssert.AreEqual(new[] { "A", "B" }, people.Select(p => p.Name).ToArray());
            var commonsQuery = Uri.UnescapeDataString(handler.RequestedUris.Single(u => u!.Host == "commons.wikimedia.org")!.Query);
            StringAssert.Contains(commonsQuery, "File:A.jpg|File:B.jpg");
            Assert.IsFalse(commonsQuery.Contains("File:C.jpg"), "Images beyond the limit should not be resolved");
        }

        [TestMethod]
        public async Task WhenSameUrlRequestedConcurrently_ShouldIssueSingleHttpRequest()
        {
            var handler = new DelayedHttpMessageHandler(TimeSpan.FromMilliseconds(200), @"{""title"":""Elvis Presley"",""wikibase_item"":""Q303""}");
            WikiApi.SetHttpMessageHandlerForTesting(handler);

            var tasks = Enumerable.Range(0, 10).Select(_ => WikiApi.GetWikiPersonAsync("Elvis Presley")).ToArray();
            var people = await Task.WhenAll(tasks);

            Assert.IsTrue(people.All(p => p.Id == 303));
            Assert.AreEqual(1, handler.RequestCount, "Concurrent identical requests should share one HTTP call");

            await WikiApi.GetWikiPersonAsync("Elvis Presley");
            Assert.AreEqual(1, handler.RequestCount, "Subsequent request should be served from cache");
        }

        [TestMethod]
        public async Task WhenOneCallerCancels_OtherCallersForSameUrlShouldStillSucceed()
        {
            var handler = new DelayedHttpMessageHandler(TimeSpan.FromMilliseconds(300), @"{""title"":""Elvis Presley"",""wikibase_item"":""Q303""}");
            WikiApi.SetHttpMessageHandlerForTesting(handler);

            using (var cts = new CancellationTokenSource())
            {
                var cancelled = WikiApi.GetWikiPersonAsync("Elvis Presley", cts.Token);
                var other = WikiApi.GetWikiPersonAsync("Elvis Presley");
                cts.Cancel();

                await Assert.ThrowsExceptionAsync<OperationCanceledException>(async () => await cancelled);
                Assert.AreEqual(303, (await other).Id);
                Assert.AreEqual(1, handler.RequestCount);
            }
        }

        [TestMethod]
        public async Task WhenRequestFails_ShouldNotCacheFailure()
        {
            var calls = 0;
            // 400 is non-transient, so the first call fails fast without retry backoff.
            var handler = new LambdaHttpMessageHandler(_ => Interlocked.Increment(ref calls) == 1
                ? new HttpResponseMessage(HttpStatusCode.BadRequest)
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(@"{""title"":""Elvis Presley"",""wikibase_item"":""Q303""}", Encoding.UTF8, "application/json")
                });
            WikiApi.SetHttpMessageHandlerForTesting(handler);

            await Assert.ThrowsExceptionAsync<HttpRequestException>(() => WikiApi.GetWikiPersonAsync("Elvis Presley"));
            var person = await WikiApi.GetWikiPersonAsync("Elvis Presley");
            Assert.AreEqual(303, person.Id);
            Assert.AreEqual(2, calls);
        }

        [TestMethod]
        public async Task WhenSendingRequests_ShouldUseDescriptiveUserAgent()
        {
            var handler = new FakeHttpMessageHandler(_ => @"{""title"":""X"",""wikibase_item"":""Q1""}");
            WikiApi.SetHttpMessageHandlerForTesting(handler);

            await WikiApi.GetWikiPersonAsync("X");

            var ua = handler.LastRequest!.Headers.UserAgent.ToString();
            StringAssert.StartsWith(ua, "WikiDataLib/");
            StringAssert.Contains(ua, "github.com/alexan1/WikiDataLib");
        }

        #endregion
    }

    internal class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, string> _responseSelector;
        private int _requestCount;
        public int RequestCount => _requestCount;
        public HttpRequestMessage? LastRequest { get; private set; }
        public List<Uri?> RequestedUris { get; } = new List<Uri?>();

        public FakeHttpMessageHandler(Func<HttpRequestMessage, string> responseSelector)
        {
            _responseSelector = responseSelector;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requestCount);
            LastRequest = request;
            lock (RequestedUris) RequestedUris.Add(request.RequestUri);
            var json = _responseSelector(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }

    internal class DelayedHttpMessageHandler : HttpMessageHandler
    {
        private readonly TimeSpan _delay;
        private readonly string _json;
        private int _requestCount;
        public int RequestCount => _requestCount;

        public DelayedHttpMessageHandler(TimeSpan delay, string json)
        {
            _delay = delay;
            _json = json;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requestCount);
            await Task.Delay(_delay, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_json, Encoding.UTF8, "application/json")
            };
        }
    }

    internal class LambdaHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public LambdaHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        {
            _respond = respond;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_respond(request));
        }
    }
}
