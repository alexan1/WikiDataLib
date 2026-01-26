# WikiDataLib

Consumer-focused README for the NuGet package.

This library provides simple access to WikiData (search people and fetch person details).
Targets: .NET Standard 2.0 and .NET 10.

Install
```
dotnet add package WikiDataLib
```

Quick usage
```csharp
using WikiDataLib;

// search people by name
var people = await WikiData.WikiPeopleSearch("Ada Lovelace");

// get a person by id (numeric part of Q)
var person = await WikiData.GetWikiPerson(239);
```

API (signatures)
```csharp
public static Task<Collection<WikiPerson>> WikiPeopleSearch(string searchString)

public static Task<WikiPerson> GetWikiPerson(int id)
```

WikiPerson
```csharp
public class WikiPerson
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public DateTime? Birthday { get; set; }
    public DateTime? Death { get; set; }
    public string Image { get; set; }
    public string Link { get; set; }
}
```

Notes
- Method names and samples use `async/await`.
- The package README is the project README (`WikiDataLib/README.md`) and is included in the nupkg.

Repository and license
- Source: https://github.com/alexan1/WikiDataLib
- License: see `LICENSE` in the package
