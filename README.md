# WikiDataLib

This is .NET Standard 2.0 library for access WikiData from your application.

Right now it has only two methods for get ino about persons, who has Wiki pages.

Namespace: WikiDataLib

### WikiDataLib.WikiSearch Method

Get collection of people with specific name.

<b>Remark:</b> This is anync method.

### WikiSearch(String)

`People = await WikiData.WikiSearch(SearchName);`

### WikiDataLib.GetWikiPerson Method

`public async static Task<WikiPerson> GetWikiPerson(int id)`

where

```
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
```

<b>Sample:</b> 
`People = await WikiData.WikiSearch(SearchName);`
