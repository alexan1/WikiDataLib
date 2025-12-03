using Microsoft.VisualStudio.TestTools.UnitTesting;
using WikiDataLib;
using System.Threading.Tasks;

namespace WikiTest
{
    [TestClass]
    public class WikiTests
    {
        [TestMethod]
        public async Task WikiSearch1()
        {
            var people = await WikiData.WikiPeopleSearch("Pope");
            Assert.AreNotEqual(0, people.Count);
        }

        [TestMethod]
        public async Task GetPerson1()
        {
            var person = await WikiData.GetWikiPerson(303);

            Assert.IsNotNull(person);

            Assert.AreEqual("Elvis Presley", person.Name);
        }
    }
}
