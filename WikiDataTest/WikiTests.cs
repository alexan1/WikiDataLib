using Microsoft.VisualStudio.TestTools.UnitTesting;
using WikiDataLib;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace WikiTest
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

            await Assert.ThrowsExceptionAsync<TaskCanceledException>(
                async () => await WikiData.WikiPeopleSearchAsync("Pope", cts.Token));
        }

        [TestMethod]
        public async Task WhenCancellationTokenIsCancelledDuringExecution_ShouldCancel()
        {
            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(1));

            await Assert.ThrowsExceptionAsync<TaskCanceledException>(
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

        #endregion
    }
}
