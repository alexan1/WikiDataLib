# WikiDataLib

A modern, robust .NET library for accessing WikiData SPARQL queries to search and retrieve person information.

[![NuGet](https://img.shields.io/nuget/v/WikiDataLib.svg)](https://www.nuget.org/packages/WikiDataLib/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

## Features

✅ **Modern & Safe**: C# 8.0 with nullable reference types  
✅ **Async/Await**: Full async support with cancellation tokens  
✅ **Well-tested**: Comprehensive test coverage (17 tests)  
✅ **Error Handling**: Robust error handling with meaningful exceptions  
✅ **Cross-platform**: Targets .NET Standard 2.0 and .NET 10  
✅ **Production-ready**: Input validation, resource management, XML documentation  

## Installation

```bash
dotnet add package WikiDataLib
```

Or via Package Manager:
```powershell
Install-Package WikiDataLib
```

## Quick Start

```csharp
using WikiDataLib;
using System.Threading;

// Search for people by name
var people = await WikiData.WikiPeopleSearchAsync("Ada Lovelace");
foreach (var person in people)
{
    Console.WriteLine($"{person.Name}: {person.Description}");
}

// Get a specific person by WikiData ID (numeric part of Q-identifier)
var elvis = await WikiData.GetWikiPersonAsync(303); // Q303 = Elvis Presley
Console.WriteLine($"{elvis.Name} was born on {elvis.Birthday:yyyy-MM-dd}");
```

## Advanced Usage

### Cancellation Support

```csharp
using var cts = new CancellationTokenSource();
cts.CancelAfter(TimeSpan.FromSeconds(5));

try
{
    var people = await WikiData.WikiPeopleSearchAsync("Pope", cts.Token);
}
catch (TaskCanceledException)
{
    Console.WriteLine("Search was cancelled");
}
```

### Error Handling

```csharp
try
{
    var person = await WikiData.GetWikiPersonAsync(999999999);
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"Person not found: {ex.Message}");
}
catch (HttpRequestException ex)
{
    Console.WriteLine($"Network error: {ex.Message}");
}
```

## API Reference

### WikiData.WikiPeopleSearchAsync

Searches for people in WikiData by name.

```csharp
public static Task<Collection<WikiPerson>> WikiPeopleSearchAsync(
    string searchString, 
    CancellationToken cancellationToken = default)
```

**Parameters:**
- `searchString` - The search term to find people (required, non-empty)
- `cancellationToken` - Cancellation token to cancel the operation (optional)

**Returns:**
- `Collection<WikiPerson>` - Collection of matching people (empty if no results)

**Exceptions:**
- `ArgumentException` - When searchString is null, empty, or whitespace
- `HttpRequestException` - When the WikiData API request fails
- `JsonException` - When the response cannot be parsed
- `TaskCanceledException` - When the operation is cancelled

### WikiData.GetWikiPersonAsync

Gets a specific person from WikiData by their numeric ID.

```csharp
public static Task<WikiPerson> GetWikiPersonAsync(
    int id, 
    CancellationToken cancellationToken = default)
```

**Parameters:**
- `id` - The WikiData entity ID (numeric part of Q-identifier, e.g., 303 for Q303)
- `cancellationToken` - Cancellation token to cancel the operation (optional)

**Returns:**
- `WikiPerson` - The person's information

**Exceptions:**
- `ArgumentOutOfRangeException` - When id is less than or equal to 0
- `InvalidOperationException` - When no person is found with the given ID
- `HttpRequestException` - When the WikiData API request fails
- `JsonException` - When the response cannot be parsed
- `TaskCanceledException` - When the operation is cancelled

### WikiPerson Class

Represents a person entity from WikiData.

```csharp
public class WikiPerson
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public DateTime? Birthday { get; set; }
    public DateTime? Death { get; set; }
    public string? Image { get; set; }
    public string? Link { get; set; }
}
```

**Properties:**
- `Id` - WikiData numeric identifier
- `Name` - Person's name (nullable)
- `Description` - Short description of the person (nullable)
- `Birthday` - Date of birth (nullable)
- `Death` - Date of death (nullable, null if still alive or unknown)
- `Image` - URL to person's image (nullable)
- `Link` - Wikipedia article URL (nullable)

## Migration Guide (v1.1.3 → v1.1.4)

### Breaking Changes

#### 1. Method Names Changed (Async Suffix)

**Before (v1.1.3):**
```csharp
var people = await WikiData.WikiPeopleSearch("Pope");
var person = await WikiData.GetWikiPerson(303);
```

**After (v1.1.4):**
```csharp
var people = await WikiData.WikiPeopleSearchAsync("Pope");
var person = await WikiData.GetWikiPersonAsync(303);
```

#### 2. Nullable String Properties

**Before (v1.1.3):**
```csharp
public string Name { get; set; }  // Non-nullable
```

**After (v1.1.4):**
```csharp
public string? Name { get; set; }  // Nullable
```

**Migration:**
- Enable nullable reference types in your project: `<Nullable>enable</Nullable>`
- Add null checks when accessing WikiPerson properties
- Or use null-forgiving operator `!` if you're certain the value exists

```csharp
// Recommended approach
var name = person.Name ?? "Unknown";

// Or with null-conditional
Console.WriteLine(person.Name?.ToUpper());
```

### New Features in v1.1.4

✅ **Cancellation Token Support** - Cancel long-running operations  
✅ **Comprehensive Error Handling** - Specific exceptions with meaningful messages  
✅ **Input Validation** - Validates parameters before making API calls  
✅ **Security Fix** - Prevents SPARQL injection attacks  
✅ **Resource Management** - Fixed HttpClient resource leak  
✅ **XML Documentation** - Full IntelliSense support  

### Non-Breaking Improvements

- DateTime properties now use `null` instead of `DateTime.MinValue` for missing values
- Better performance and reliability
- Improved code maintainability

## Examples

### Find People with Special Characters

```csharp
// Handles special characters and Unicode automatically
var people = await WikiData.WikiPeopleSearchAsync("O'Brien");
var german = await WikiData.WikiPeopleSearchAsync("Müller");
```

### Check for Missing Information

```csharp
var person = await WikiData.GetWikiPersonAsync(303);

if (person.Birthday.HasValue)
{
    Console.WriteLine($"Born: {person.Birthday.Value:yyyy-MM-dd}");
}

if (person.Death.HasValue)
{
    Console.WriteLine($"Died: {person.Death.Value:yyyy-MM-dd}");
}
else
{
    Console.WriteLine("Still alive or death date unknown");
}
```

### Batch Processing with Error Handling

```csharp
var ids = new[] { 303, 42, 239, 5879 };

foreach (var id in ids)
{
    try
    {
        var person = await WikiData.GetWikiPersonAsync(id);
        Console.WriteLine($"Q{id}: {person.Name}");
    }
    catch (InvalidOperationException)
    {
        Console.WriteLine($"Q{id}: Not found");
    }
}
```

## Requirements

- .NET Standard 2.0+ or .NET 5+
- C# 8.0+ (for nullable reference types support)

## Dependencies

- `System.Text.Json` (10.0.0)
- `System.ComponentModel.Annotations` (5.0.0)

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Repository

- **Source Code**: https://github.com/alexan1/WikiDataLib
- **NuGet Package**: https://www.nuget.org/packages/WikiDataLib/
- **Issues**: https://github.com/alexan1/WikiDataLib/issues

## Changelog

### v1.1.4 (Current)
- **BREAKING**: Renamed methods with `Async` suffix
- **BREAKING**: String properties now nullable
- Added cancellation token support
- Added comprehensive error handling
- Added input validation
- Fixed SPARQL injection vulnerability
- Fixed HttpClient resource leak
- Added XML documentation
- Improved code quality and maintainability
- Expanded test coverage (2 → 17 tests)

### v1.1.3
- Initial stable release
- Basic search and get person functionality
