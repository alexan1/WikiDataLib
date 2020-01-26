# WikiDataLib

This is .NET Standard 2.0 library for access WikiData from your application.

Right now it has only two methods for get ino about persons, who has Wiki pages.

Namespace: WikiDataLib

### WikiDataLib.WikiSearch Method

Get collection of people with specific name.

Remark: This is anync method.

### WikiSearch(String)

`public async static Task<WikiPerson> GetWikiPerson(int id)`
