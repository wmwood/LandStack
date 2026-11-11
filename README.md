# The LAND Stack

LAND combines LiteDB, Angular, Nginx and .NET in a to-do application. The SPA has
REST (`/`) and SignalR (`/realtime`) views backed by the same .NET 11 API
operations through a local typed dispatcher. The backend retains LiteDB data
and existing REST/hub contracts alongside the Angular 22 frontend.

## Supported frontend setup

Use Node **24.22.2** from [Node.js](https://nodejs.org/en/download) and npm
**12.0.2**. The repository `.node-version`, SPA `packageManager`, Docker build and
GitHub Actions workflow agree on these versions. Selecting a Node version does
not select npm automatically:

```powershell
rtk proxy node --version
rtk proxy npm install --global npm@12.0.2
rtk proxy npm --version
```

Use the committed lockfile. From `src/LandStack-Spa`:

```powershell
rtk proxy npm ci --no-audit --no-fund
rtk proxy npm start
```

Open `http://localhost:8080`. This is the default development port and matches
the API's allowed browser origin. The SPA uses Angular 22.2.5, standalone
components, the application builder, SCSS and RxJS. Application change detection
remains Zone.js based.

## Backend setup

Use the stable .NET SDK selected by `global.json`, which allows servicing patches
within its feature band and selects the native Microsoft Testing Platform runner.
From `src/LandStack.Api`, run `rtk proxy dotnet run` for Development on port 5000.
The default data file is that content root's `App_Data/database.db`; use the
disposable setup below when creating or deleting verification items.

## Endpoints and behavior

- `/todoItem`: list and create; `/todoItem/toggleComplete/{id}`: toggle;
  `/todoItem/{id}`: delete. Successful routes, methods and item fields are retained.
- `/todohub`: `GetAllItems`, `Create`, `ToggleComplete`, `Delete`, with existing
  `itemCreated`, `itemUpdated`, `itemDeleted` event names and payloads.
- `/openapi/v1.json`: built-in OpenAPI document, available only in Development.
- `/health`: minimal liveness returning `Healthy`. It does not open or validate
  the database. Verify `/todoItem` separately when checking storage readiness.

Both transports reject null/empty/whitespace descriptions and preserve valid
text as supplied. A missing toggle returns REST 404 or a safe hub error; an
absent delete succeeds without an event. REST failures use Problem Details;
unexpected HTTP/hub failures expose generic messages while server logs retain
diagnostics. New timestamps use TimeProvider UTC; existing stored instants are
preserved. A failed broadcast after a committed write does not undo that write.

## Database configuration

The default is `<API content root>/App_Data/database.db`, collection `ToDoItem`.
Set `LiteDb:DataDirectory` in configuration, or environment variable
`LiteDb__DataDirectory`, to use another directory. Relative paths resolve under
the content root. Use a fresh external directory for manual verification and
never point tests at application data. Backend tests create disposable copies.

C# DTOs remain the source of tracked TypeScript contracts. Build the API before
the SPA; see [contract generation](tools/contract-generation/README.md) when
changing DTO nullability or API-only build behavior.

## Frontend checks

Run these from `src/LandStack-Spa`:

```powershell
rtk proxy npm run lint
rtk proxy npm test -- --watch=false
rtk proxy npm run build -- --configuration production
```

`npm test` starts development watch mode; press `q` to exit. The one-shot command
runs a nonempty Vitest suite and exits nonzero for failed assertions. Tests use
jsdom, mocked HTTP and a fake SignalR connection, so they need no running API,
production database or manually installed browser. Production output is
`dist/LandStack-Spa/browser`, including `index.html`. Strict TypeScript/template
checks and the configured size budgets remain enabled.

ESLint checks handwritten TypeScript and Angular templates. Generated DTOs under
`src/app/_models` are excluded from handwritten-code lint rules; their C# sources
and API build generation remain authoritative. Karma/Jasmine and TSLint were
replaced. The incomplete Protractor setup and `e2e` command were retired; there is
no automated browser end-to-end suite.

### Adding tests

Place a `*.spec.ts` beside the component or service. Import assertions and hooks
from `vitest`. For HTTP services, register `provideHttpClient()` before
`provideHttpClientTesting()`, use `HttpTestingController.expectOne()` to assert
URLs/methods/payloads, and `flush()` the response. Always verify outstanding
requests and unsubscribe. For example, beside `todo-rest.service.ts`:

```typescript
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { expect, it } from 'vitest';
import { environment } from '../../environments/environment';
import { TodoRestService } from './todo-rest.service';

it('loads an empty list without a real server', () => {
  TestBed.configureTestingModule({
    providers: [provideHttpClient(), provideHttpClientTesting()],
  });
  const http = TestBed.inject(HttpTestingController);
  const service = TestBed.inject(TodoRestService);
  let count = -1;
  const subscription = service.todos$.subscribe(items => count = items.length);
  const request = http.expectOne(`${environment.apiUrl}/todoItem`);
  expect(request.request.method).toBe('GET');
  request.flush([]);
  expect(count).toBe(0);
  subscription.unsubscribe();
  http.verify();
});
```

For standalone components, import the component into `TestBed`, provide
`provideZonelessChangeDetection()` and replace services with `useValue` mocks.
Drive input/submit/click events through the rendered DOM and await
`fixture.whenStable()` before checking rendering. The test build intentionally
omits Zone.js; this does not change the application's Zone.js runtime. See the
[REST component tests](src/LandStack-Spa/src/app/todo-rest/todo-rest.component.spec.ts)
and [routing tests](src/LandStack-Spa/src/app/app-routing.spec.ts) for working examples.

For realtime tests, replace `TODO_HUB_CONNECTION_FACTORY` with a fake rather than
opening a socket; see the
[service tests](src/LandStack-Spa/src/app/todo-realtime/todo-realtime.service.spec.ts).
Use Vitest fake timers for busy delays/retries, settle controlled promises,
restore real timers and destroy fixtures or stop connections during teardown.
Assert rejection and cancellation behavior as well as successful operations.

## CI and containers

[Frontend CI](.github/workflows/frontend.yml) runs on pushes and pull requests.
It selects `.node-version`, installs pinned npm, caches from the SPA lockfile,
and runs clean install, lint, one-shot tests and a production build on Ubuntu.
It needs no backend or secrets. Report hosted workflow execution only after
observing an actual run; local checks do not establish hosted results.

From the repository root:

```powershell
rtk proxy docker compose config --quiet
rtk proxy docker build -f src/.docker/_spa.dockerfile -t landstack-spa:local src
rtk proxy docker compose build --pull api
rtk proxy docker compose up api
```

The SPA Docker build uses Node 24.22.2, npm 12.0.2 and `npm ci`, with no OpenSSL
legacy-provider flag. It copies only source/configuration after installation so
host `node_modules` cannot overwrite Linux dependencies. The build's output
argument produces `/app/dist/out/browser`; those assets become Nginx's document
root. Nginx falls back to `index.html` for `/realtime` and other client routes.
It does not proxy API or hub traffic. Production SPA URLs point directly to
`http://localhost:8181`; Compose publishes SPA 8080 and API 8181.

The API image uses stable .NET 11 SDK/runtime patch tags, runs as non-root UID
1654 and listens on internal 8080 with host port 8181. The named `appdata` volume
mounts at `/app/App_Data`; a new empty volume inherits the image directory's
ownership. Existing volumes are never automatically chowned at startup.
Use `rtk proxy docker compose up --build` to start both services. Update API
Dockerfile tags and `global.json` deliberately for servicing releases, rerun
checks and record resolved image digests; `--pull` fetches the selected tags.

## Disposable manual verification

Run this only from a managed checkout when using the feature workflow. Record
its exact commit and use a new external directory for each run. Set an explicit external data directory and content root to isolate
`App_Data/database.db` without changing application code or production data.

From `src/LandStack.Api`, build with `rtk proxy dotnet build -c Release`. Review
any generated DTO diff. Then, from the repository root in PowerShell:

```powershell
$repoRoot = (Get-Location).Path
$smokeRoot = Join-Path $env:TEMP ('landstack-smoke-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $smokeRoot
$apiDll = Join-Path $repoRoot 'src/LandStack.Api/bin/Release/net11.0/LandStack.Api.dll'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:LiteDb__DataDirectory = Join-Path $smokeRoot 'App_Data'
rtk proxy dotnet $apiDll --contentRoot $smokeRoot --urls http://localhost:5000
```

Keep that owned process running. Its disposable database is exactly
`$smokeRoot/App_Data/database.db`. Start the SPA with `npm start` in another
terminal and open two browser clients at `http://localhost:8080`. Record URLs,
API process identity, database path, commit and observations outside the
checkout. In a managed workflow, register verification directories/process
ownership and containers with the orchestrator before dependent use.

1. In `/`, load the list, reject an empty description, create a uniquely named
   item, observe form reset/list reload, toggle twice, delete and refresh.
2. In `/realtime`, check connection state and repeat create/toggle/delete and form
   validation. Mutate items from each client and confirm creation/update/deletion
   reaches the other realtime client. Include a REST mutation observed by a
   realtime client.
3. Check shell links, route titles and an unknown-route fallback. With browser
   network throttling long enough to exceed 250 ms, observe busy indication,
   focus blur and restoration. Unit tests separately cover deterministic timing,
   concurrency and rejected operations.
4. Stop only the owned development servers. For production verification, run the
   built SPA image on localhost:8080 and launch the same isolated API command with
   `--urls http://localhost:8181`. Repeat REST/realtime actions and two-client
   broadcasts. Check index, JS/CSS asset MIME types and direct `/realtime` loading.
   Origin `http://localhost:8080` remains required by API CORS.

Keep the disposable data and evidence until the owning workflow authorizes
cleanup. Never substitute a persistent Compose volume or an existing application
database to make these checks pass.

## Upgrade, backup and rollback

Before upgrading an existing deployment:

1. Record the current API container, image ID, internal port and exact named
   volume using `docker compose ps -a api` and `docker inspect <api-container>`.
   Check the mount destination is `/app/App_Data` and identify its actual source;
   Compose project names can change the physical volume name.
2. Stop the API with `rtk proxy docker compose stop api`. Copy the **entire**
   data directory, including any log files, to a new external backup directory:
   `rtk proxy docker cp <api-container>:/app/App_Data/. <absolute-new-backup-directory>`.
   Keep that consistent stopped-data backup and the previous image.
3. Inspect existing data ownership before starting the non-root image. With the
   API stopped, an owner may explicitly repair the verified volume using a
   uniquely named helper and the runtime UID reported by
   `docker image inspect landstack-api --format '{{.Config.User}}'`. For the
   current image, the narrowly scoped helper command is:

   ```text
   rtk proxy docker run --name <unique-permissions-helper> --user 0:0 --entrypoint chown --mount type=volume,src=<verified-appdata-volume>,dst=/data landstack-api -R 1654:1654 /data
   ```

   Replace placeholders with inspected identities. This changes only ownership
   in that selected volume; retain existing file modes. It is an explicit owner
   operation, not an automatic application migration. Host bind mounts may need
   host-specific ownership handling instead of the named-volume procedure.
4. Start the upgraded API, verify `/health`, then list/create/toggle/delete via
   `/todoItem` and check realtime events. Restart and confirm persistence before
   declaring the upgrade successful. Keep backups and previous images.

For rollback, stop the upgraded API and restore the pre-upgrade directory into
a **separate** volume, then start the recorded previous image with that restored
copy and its recorded internal port. New-engine writes may prevent an older
engine from reading current files; use the pre-upgrade backup in that case.
Verify records and mutations before switching traffic back. Keep the current
volume intact for investigation, and run only one writer against a data file.


## Backend verification

Build from `src/LandStack.Api` with `rtk proxy dotnet build -c Release`, then run
from the repository root:

```powershell
rtk proxy dotnet test --project tests/LandStack.Api.Tests/LandStack.Api.Tests.csproj -c Release
rtk proxy dotnet list src/LandStack.Api/LandStack.Api.csproj package --include-transitive --vulnerable
$compatibilityOutput = Join-Path $env:TEMP 'LandStack-compatibility'
rtk proxy node tests/compatibility/signalr-client.cjs --output-root $compatibilityOutput
rtk proxy docker compose config --quiet
rtk git diff --check
```

Run the real-client command after installing the SPA lockfile with its pinned
Node/npm toolchain. It uses the actual locked SignalR package, two WebSocket
clients and isolated API/data processes; see
[compatibility verification](tests/compatibility/README.md) for evidence and
lifecycle details. Backend tests cover dispatcher behavior, copied old databases,
HTTP/hub errors, UTC/serialization and operational endpoints. Review advisory
output as well as command exit codes. Frontend lint/Vitest/build checks above
remain required; mocked tests do not replace real-client/browser verification.
Packaging also requires an actual affected-image build/start and data persistence
checks. Independent feature validation and quality reviews bind exact commits.

Initialization's .NET 6 NETSDK1138, LiteDB 5.0.11 NU1904
(GHSA-3x49-g6rc-c284) and obsolete Compose version findings are historical;
the .NET 11/LiteDB 5.0.21 backend and updated Compose resolve those shipping
findings. Excluded manual legacy fixture generators retain old-version
provenance only. npm 12 may report blocked install scripts; keep the default
policy and investigate actual failures rather than silently weakening it.
