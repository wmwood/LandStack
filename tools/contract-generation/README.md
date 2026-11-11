# C# to TypeScript contract generation

C# DTOs under `src/LandStack.Api/Infrastructure/Dto` remain authoritative.
The API `Convert` target retains MTT 0.7.2, which works under the selected
stable .NET 11 SDK. Build from the API directory before building the SPA.
Do not edit generated TypeScript files by hand.

The target anchors the input path to `MSBuildProjectDirectory`. In a complete
checkout it writes the existing `src/LandStack-Spa/src/app/_models` directory.
With API-only sources (as in the API Docker context), it writes
`$(IntermediateOutputPath)/TypeScriptModels` inside the API project instead.
It never requires or creates a sibling SPA tree for an API-only build.
`TypeScriptModelOutputDirectory` may explicitly override the output location.
Generation remains part of every normal API build/publish.

MTT maps nullable C# `string?` syntax to an *optional* TypeScript property rather
than a nullable required property. The DTO Description properties deliberately
use standard `[AllowNull, MaybeNull]` annotations with a `string` declaration.
This keeps nullable flow analysis truthful and preserves the existing generated
`description: string` client contract. Shared runtime validation still rejects
missing/null/empty/whitespace create descriptions. Historical invalid null
values are handled according to each transport's existing serialization rules.
Do not remove those annotations without checking generated and wire contracts.

For verification, compare all three model files before/after a clean Release
API build and a repeated build. Also build a source-only API copy without a
sibling SPA and compare its intermediate TypeScript models byte-for-byte with
the tracked models. The feature's external implementation report preserves the
actual commands, source-only path and hash results; it is not checked-in state.
Container image verification separately exercises this same API-only context.
