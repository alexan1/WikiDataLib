# WikiDataLib

[![NuGet](https://img.shields.io/nuget/v/WikiDataLib.svg)](https://www.nuget.org/packages/WikiDataLib/) [![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A .NET library for accessing WikiData from your application. Targets `netstandard2.0` and `net10.0`.

Namespace: `WikiDataLib`

## Installation

```
dotnet add package WikiDataLib
```

## Methods

### `WikiData.WikiPeopleSearchAsync`

Search for people in WikiData by name. Returns a collection of matching people.

```csharp
public static async Task<Collection<WikiPerson>> WikiPeopleSearchAsync(
    string searchString,
    CancellationToken cancellationToken = default)
```

**Example:**
```csharp
var people = await WikiData.WikiPeopleSearchAsync("Ada");
```

**Parameters:**
- `searchString` — name to search for (throws `ArgumentException` if null or whitespace)
- `cancellationToken` — optional cancellation token

### `WikiData.GetWikiPersonAsync`

Get information about a specific person by their WikiData numeric ID (the number after `Q`).

```csharp
public static async Task<WikiPerson> GetWikiPersonAsync(
    int id,
    CancellationToken cancellationToken = default)
```

**Example:**
```csharp
var person = await WikiData.GetWikiPersonAsync(303); // Q303 = Elvis Presley
```

**Parameters:**
- `id` — numeric WikiData entity ID, e.g. `303` for `Q303` (throws `ArgumentOutOfRangeException` if ≤ 0)
- `cancellationToken` — optional cancellation token

## WikiPerson Class

```csharp
public class WikiPerson
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public DateTime? Birthday { get; set; }
    public DateTime? Death { get; set; }
    public string? Image { get; set; }   // URL to image
    public string? Link { get; set; }    // English Wikipedia URL
}
```
