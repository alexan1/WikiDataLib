This is .NET Standard 2.0 library for access WikiData from your application.

Right now it has only two methods for get people info from WikiPedia.

Namespace: WikiDataLib

WikiDataLib.WikiPeopleSearch Method
Get collection of people with specific name.

remark: This is anync method.

public async static Task<Collection<WikiPerson>> WikiPeopleSearch(string searchString)

sample: People = await WikiData.WikiPeopleSearch(SearchName);

parameter: string: name of person to looking for.

WikiDataLib.GetWikiPerson Method
Get information of specific person.

remark: This is anync method.

public async static Task<WikiPerson> GetWikiPerson(int id)

sample: People = await WikiData.WikiSearch(SearchName);

parameter: int: id of specific person (without first Q).

WikiPerson Class
    public class WikiPerson
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy}")]
        public DateTime? Birthday { get; set; }
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy}")]
        public DateTime? Death { get; set; }
        public string Image { get; set; }
        public string Link { get; set; }       
    }