# Repository instructions

## Shell commands

When running shell commands, prefer `rtk` (Rust Token Killer) to reduce
command-output token usage.

- Prefix shell commands with `rtk` by default.
- In command chains, prefix each command separately:
  `rtk git add . && rtk git commit -m "msg"`
- If debugging requires unfiltered/raw output, run the command without `rtk`.
- Use `rtk proxy <cmd>` when raw output is needed but usage should still be tracked.
- PowerShell cmdlets are not executables; run them directly, or invoke PowerShell
  through `rtk proxy pwsh` when tracking is needed. Check `$LASTEXITCODE` after
  each external command before a dependent operation.

## Architecture and change boundaries

This repository demonstrates LAND (LiteDB, Angular, Nginx, .NET) with a to-do
application. Read [GLOSSARY.md](GLOSSARY.md) before changing domain terms or
cross-language contracts.

- `src/LandStack.Api`: ASP.NET Core API targeting `net11.0`. Controllers and
  `Hubs/ToDoHub.cs` send the same local dispatcher requests to
  `Infrastructure/Features`; keep shared operations in those handlers so REST
  and realtime behavior remain aligned.
- `Infrastructure/Services/LiteDbProvider.cs` defaults to content-root
  `App_Data/database.db`; `LiteDb:DataDirectory` provides the isolated data seam.
  Preserve application data; use disposable copies for verification. Creation
  uses `TimeProvider` UTC; preserve historical stored instants.
- `Infrastructure/Dto` owns the transport definitions. The API project's MTT
  `Convert` target runs before build and generates the tracked TypeScript files
  in `src/LandStack-Spa/src/app/_models`. Change the C# source and build from
  the API directory, then inspect generated diffs instead of hand-editing models.
  For DTO nullability or API-only output changes, read
  [contract generation](tools/contract-generation/README.md).
- `src/LandStack-Spa`: Angular 22 with standalone components, strict TypeScript,
  RxJS services and SCSS. Application runtime retains Zone.js; Vitest TestBed uses
  zoneless change detection in the separate testing configuration.
  Follow its `.editorconfig`. `todo-rest` uses HTTP and reloads after mutations;
  `todo-realtime` maintains a list from SignalR messages. Both use `BusyService`.
- Keep REST routes, hub method names and notification names coordinated with
  both SPA services. `NotificationHandler` broadcasts camel-case event names
  (`itemCreated`, `itemUpdated`, `itemDeleted`); deletion carries an ID, creation
  and update carry an item. Trace serialization before changing payloads.
- `docker-compose.yml` and `src/.docker` own packaging. Nginx serves the SPA
  with a client-route fallback; it does not proxy API or hub traffic. The API
  image runs non-root on internal 8080 with host 8181. Before changing existing
  volume ownership or upgrading data, follow [backup and rollback](README.md#upgrade-backup-and-rollback).

## Setup, builds and checks

Use the Node release in `.node-version` and npm recorded in the SPA
`packageManager`. For initial setup or test authoring, read [README.md](README.md).
Use the stable .NET SDK selected by `global.json`, including its native MTP test
runner. Run each command from the directory shown. Use `rtk proxy` for tools not covered
by an RTK filter. There is no root solution or root `package.json`.

| Directory | Command | Purpose |
| --- | --- | --- |
| `src/LandStack.Api` | `rtk proxy dotnet build -c Release` | Restore, generate SPA models and compile API. |
| Repository root | `rtk proxy dotnet test --project tests/LandStack.Api.Tests/LandStack.Api.Tests.csproj -c Release` | Run isolated backend behavioral tests. |
| Repository root | `rtk proxy dotnet list src/LandStack.Api/LandStack.Api.csproj package --include-transitive --vulnerable` | Review advisory findings, not just exit status. |
| `src/LandStack-Spa` | `rtk proxy npm ci --no-audit --no-fund` | Install dependencies from the committed lockfile. |
| `src/LandStack-Spa` | `rtk proxy npm run lint` | ESLint handwritten TypeScript and templates. |
| `src/LandStack-Spa` | `rtk proxy npm test -- --watch=false` | Run mocked Vitest/jsdom tests once; failed assertions must fail the command. |
| `src/LandStack-Spa` | `rtk proxy npm run build -- --configuration production` | Compile production browser assets and enforce size budgets. |
| Repository root | `rtk proxy docker compose config --quiet` | Validate Compose configuration without starting containers. |
| Repository root | `rtk git diff --check` | Check patch whitespace. |

For API/contract changes, build the API first and review regenerated models,
then run the SPA checks. For frontend changes, run lint, tests and the production
build. For packaging changes, validate Compose and build/verify the affected
image separately. Keep the lockfile and resolve tool compatibility without
silently upgrading dependencies. The frontend GitHub Actions workflow runs clean
install, lint, one-shot tests and production build; report hosted execution only
when actually observed.

Tests cover REST requests/components, fake SignalR commands/events/lifecycle,
routing, and busy timing/concurrency/focus/cleanup. Add behavior assertions and
failure/teardown coverage beside affected code. Generated models are excluded
from handwritten-code ESLint rules. Karma/Jasmine and TSLint were replaced;
the incomplete Protractor setup and e2e command were retired. Backend behavioral
tests are tracked under `tests/LandStack.Api.Tests`; no automated browser
end-to-end suite is tracked. Build output alone is not
behavioral test coverage; report unavailable required checks explicitly.

Known findings retained from initialization: NETSDK1138 for `net6.0`, NU1904 for
LiteDB 5.0.11 (GHSA-3x49-g6rc-c284), and Compose's obsolete `version` field.
The .NET 11/LiteDB 5.0.21 backend upgrade resolves those shipping findings and
removes the Compose field; retain the original findings as history. Old LiteDB
is used only by excluded manual synthetic fixture generators, not runtime
dependencies. npm 12
can report blocked dependency install scripts; observed frontend/image builds
passed with the default policy. Inspect failures rather than weakening it.

## Running and manual verification

`rtk proxy npm start` from the SPA directory uses port 8080, matching API CORS
origin `http://localhost:8080`. The API launch profile uses 5000; development SPA
URLs target 5000. Production URLs directly target localhost:8181; Compose
publishes SPA 8080 and API 8181. Verify URLs and CORS together. Production browser
assets are `dist/LandStack-Spa/browser`; the Docker output override uses
`dist/out/browser`. Preserve Nginx's client-route fallback.

For behavior changes, follow the disposable API/two-client procedure in
[README.md](README.md#disposable-manual-verification). Set explicit external `LiteDb:DataDirectory` configuration (or an isolated
content root with that data override) and record its
absolute path, owned processes, client URLs and exact commit. Exercise list,
create, toggle and delete in both `/` and `/realtime`, plus two-client broadcasts,
validation, navigation/fallback and busy focus behavior. Record browser results
separately from mocked tests and HTTP asset checks. Preserve application data and
managed resource/evidence ownership throughout verification.

## Ownership and feature workflow

Human owners decide product terminology, requirements, scope approval and merge
or publication authority. No CODEOWNERS file or component-owner roster is tracked.
The glossary records observed code vocabulary; ask the owner about business
ambiguities rather than inventing meanings or renaming existing contracts.

When using the feature-workflow skills, start planning from a committed baseline
containing reviewed `AGENTS.md` and `GLOSSARY.md`. Preserve the original checkout,
including uncommitted work; perform managed work on unique branches and external
worktrees. Keep specifications, workflow state and reports outside all worktrees.
The initializer changes only these two documents. Subsequently the orchestrator
serializes instruction/glossary integration; workers propose scoped diffs. Human
approval governs specification revisions and merging. Initialization does not
approve a feature, publish a PR or merge its branch.
