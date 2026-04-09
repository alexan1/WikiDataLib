# Copilot Instructions — WikiDataLib

## Project
C# library for querying WikiData entities and properties.
Published to NuGet.org via GitHub Actions CI/CD.

## Stack
- **Framework**: netstandard2.0 and net10.0
- **Language**: C#
- **Package**: System.Text.Json (conditional for netstandard2.0)
- **Testing**: XUnit
- **CI/CD**: GitHub Actions (publishes to NuGet.org on master push)

## Key Files
- `WikiDataLib/WikiDataLib.csproj` — project configuration, dependencies, NuGet metadata
- `WikiDataLib/WikiData.cs` — main API
- `WikiDataTest/WikiTests.cs` — unit tests
- `.github/workflows/main.yml` — publish workflow (triggers on master push)

## Conventions
- Follow C# naming conventions (PascalCase for public members)
- Add unit tests for new features in `WikiDataTest`
- Update version in `.csproj` before publishing
- NuGet package metadata (description, tags, authors) in `.csproj` `<PropertyGroup>`

## Branch Strategy
- `master` → stable, published version
- `dev` → active development
- Feature branches → branched from `master`, merged via PR

## Workflow for All Changes
1. Create a new branch from `master` (or `main`)
2. Make changes on the new branch
3. Create a Pull Request for review
4. Merge only after PR review is approved
