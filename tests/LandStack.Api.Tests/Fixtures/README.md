# LiteDB 5.0.11 compatibility fixture

`legacy-5.0.11.db` is disposable synthetic data, created with the actual baseline
LiteDB 5.0.11 assembly and the baseline `ToDoItem` model/collection convention.
It contains no application or user data. Its SHA256 is:

`c2ce58e71dd32f8267c603995c9cac73f7afd54783fa5286fc921dac8244d8ec`

`GenerateLegacyFixture.cs` records the complete source and package version used.
From the repository root with the selected .NET SDK, generate a **new** fixture:

```text
dotnet run --file tests/LandStack.Api.Tests/Fixtures/GenerateLegacyFixture.cs -- <new-output-path.db>
```

The generator refuses to overwrite existing files. LiteDB 5.0.11 has the known
GHSA-3x49-g6rc-c284 critical advisory: this source is solely an explicit,
manual reproduction tool for synthetic compatibility data, excluded from the
test project's compilation. The API and test project use LiteDB 5.0.21; the old
package is not a shipping or test runtime dependency. Do not run this tool on
an application database. Regenerated binary hashes can differ because database
headers contain creation-specific metadata; preserve and record new provenance
if intentionally replacing the fixture.

The two records were inserted in reverse date order to exercise list ordering:

| ID | Stored description | Completed | Stored UTC instant |
| --- | --- | --- | --- |
| `11111111-1111-1111-1111-111111111111` | `preserved legacy text` | false | `2021-02-03T10:20:30Z` |
| `22222222-2222-2222-2222-222222222222` | `Legacy café ✓` | true | `2021-02-04T12:30:45Z` |

The first source description includes outer spaces. The baseline LiteDB mapper
trims these when storing; assertions intentionally match the actual historical
stored text. The approved modernization separately preserves newly supplied
valid text by configuring a provider-local mapper, without rewriting old data.
Tests copy this immutable fixture to a unique temporary directory, mutate only
the copy, and reopen it to verify both old fields and new writes persist.

## Historical unspecified-kind clock fixture

`legacy-unspecified-5.0.11.db` is a second independent synthetic fixture made
with actual LiteDB 5.0.11 and the baseline clock pattern
`new DateTime(utcDateTime.Ticks)`. Its companion `.db.json` records the source
unspecified timestamp, originating local time zone, actual stored UTC instant,
assembly version and SHA256. `GenerateUnspecifiedLegacyFixture.cs` is the
manual reproduction source, excluded from automated test compilation.

This fixture intentionally records `2021-02-05T12:30:45` as the actual persisted
instant `2021-02-05T19:30:45Z` in the originating US Mountain Standard Time zone.
The regression tests preserve that stored instant through REST, hub, toggle
and restart, independently of the machine timezone now running the tests.
They do not retrospectively fix or rewrite historical timestamps. Newly created
items use TimeProvider UTC and avoid this old unspecified-kind ambiguity.

SHA256: `e09238649478e7f22b663526bfde937a43bbe523753855ff08b7aaa74f2484ac`.
As with the first fixture, the vulnerable old library is manual-generation-only;
shipping application and automated tests do not reference it. The generator
uses Utf8JsonWriter for metadata so it also works with .NET 11 file-app defaults
that disable reflection-based System.Text.Json serialization.
