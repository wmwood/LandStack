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

- `src/LandStack.Api`: ASP.NET Core API targeting `net6.0`. Controllers and
  `Hubs/ToDoHub.cs` send the same MediatR requests to
  `Infrastructure/Features`; keep shared operations in those handlers so REST
  and realtime behavior remain aligned.
- `Infrastructure/Services/LiteDbProvider.cs` opens `App_Data/database.db`.
  Preserve application data; use disposable data for manual verification.
  `IClock` is the existing time seam for item creation.
- `Infrastructure/Dto` owns the transport definitions. The API project's MTT
  `Convert` target runs before build and generates the tracked TypeScript files
  in `src/LandStack-Spa/src/app/_models`. Change the C# source and build from
  the API directory, then inspect generated diffs instead of hand-editing models.
- `src/LandStack-Spa`: Angular 13 with strict TypeScript, RxJS services and SCSS.
  Follow its `.editorconfig`. `todo-rest` uses HTTP and reloads after mutations;
  `todo-realtime` maintains a list from SignalR messages. Both use `BusyService`.
- Keep REST routes, hub method names and notification names coordinated with
  both SPA services. `NotificationHandler` broadcasts camel-case event names
  (`itemCreated`, `itemUpdated`, `itemDeleted`); deletion carries an ID, creation
  and update carry an item. Trace serialization before changing payloads.
- `docker-compose.yml` and `src/.docker` own packaging. Nginx serves the SPA
  with a client-route fallback; it does not proxy API or hub traffic.

## Setup, builds and checks

Run each command from the directory shown. Use `rtk proxy` for tools not covered
by an RTK filter. There is no root solution, root `package.json` or CI workflow.

| Directory | Command | Purpose |
| --- | --- | --- |
| `src/LandStack.Api` | `rtk proxy dotnet build -c Release` | Restore, generate SPA models and compile API. |
| `src/LandStack-Spa` | `rtk proxy npm ci --no-audit --no-fund` | Install dependencies from the committed lockfile. |
| `src/LandStack-Spa` | `rtk proxy npm run build -- --configuration production` | Compile production SPA and check configured size budgets. |
| Repository root | `rtk proxy docker compose config --quiet` | Validate Compose configuration without starting containers. |
| Repository root | `rtk git diff --check` | Check patch whitespace. |

All commands above passed during initialization on 2026-11-09 with Windows,
.NET SDK 10.0.403 (the .NET 6 runtime is also installed), Node 26.7.0, npm 12.0.2
and Docker CLI 29.9.2. This records an observed build, not a supported runtime
matrix. No SDK version is pinned. npm reported blocked dependency install
scripts; the production build nevertheless passed with the default policy.
Keep the lockfile and do not silently upgrade dependencies to repair tooling.

Existing check limitations:

- No backend test project or frontend `*.spec.ts` files are tracked.
  A successful build is compilation evidence, not behavioral test coverage.
- `npm test -- --watch=false` fails with missing `karma`; the referenced
  `karma.conf.js` is also absent.
- `npm run lint` fails because `@angular-devkit/build-angular:tslint` is absent.
- E2E execution was not attempted: `e2e/protractor.conf.js` and
  `e2e/tsconfig.json` are absent. `npm run e2e -- --help` only verifies CLI help.
- The API build reports NETSDK1138 for `net6.0` and NU1904 for LiteDB 5.0.11
  (GHSA-3x49-g6rc-c284). Compose reports an obsolete `version` field.
  Track remediation in separately scoped work; retain these findings in handoffs.

For API/contract changes, build the API first and review regenerated models,
then build the SPA. For frontend changes, build the SPA. For packaging changes,
validate Compose and verify the affected image separately. Report unavailable
checks explicitly; do not claim a test gate passed from build output.

## Running and manual verification

The existing launch commands are `rtk proxy dotnet run` from the API directory
and `rtk proxy npm start -- --port 8080` from the SPA directory. They were not
smoke-tested during initialization. The API launch profile uses port 5000;
`Program.cs` allows browser origin `http://localhost:8080`, while plain Angular
`npm start` defaults to 4200 and will not match that CORS origin. Development
SPA URLs point to port 5000. Production URLs point directly to localhost:8181;
Compose publishes SPA 8080 and API 8181. Verify environment URLs and CORS together.

For behavior changes, exercise list, create, toggle and delete in both the REST
view (`/`) and realtime view (`/realtime`); use two browser clients when checking
broadcasts. Record actual results and use an isolated database. Container image
builds, container startup and browser behavior were not verified at initialization.

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
