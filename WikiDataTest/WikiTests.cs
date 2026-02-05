using Microsoft.VisualStudio.TestTools.UnitTesting;
using WikiDataLib;
using System.Threading.Tasks;

namespace WikiTest
{
    [TestClass]
    public class WikiTests
    {
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
    }
}
