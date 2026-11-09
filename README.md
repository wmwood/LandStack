# The LAND Stack

LAND combines LiteDB, Angular, Nginx and .NET in a to-do application. The SPA has
REST (`/`) and SignalR (`/realtime`) views backed by the same API operations.

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

The API targets .NET 6 and needs its runtime plus a compatible SDK. From
`src/LandStack.Api`, run `rtk proxy dotnet run` for ordinary development on port
5000. This uses that checkout's `App_Data/database.db`; use the disposable setup
below for verification that creates or deletes items.

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
no automated end-to-end suite.

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
It needs no backend or secrets. Local execution of these commands and workflow
inspection are recorded; a hosted run remains unverified until an authorized
push is observed.

From the repository root:

```powershell
rtk proxy docker compose config --quiet
rtk proxy docker build -f src/.docker/_spa.dockerfile -t landstack-spa:local src
```

The SPA Docker build uses Node 24.22.2, npm 12.0.2 and `npm ci`, with no OpenSSL
legacy-provider flag. It copies only source/configuration after installation so
host `node_modules` cannot overwrite Linux dependencies. The build's output
argument produces `/app/dist/out/browser`; those assets become Nginx's document
root. Nginx falls back to `index.html` for `/realtime` and other client routes.
It does not proxy API or hub traffic. Production SPA URLs point directly to
`http://localhost:8181`; Compose publishes SPA 8080 and API 8181.

## Disposable manual verification

Run this only from a managed checkout when using the feature workflow. Record
its exact commit and use a new external directory for each run. The API sets its
LiteDB directory from `ContentRootPath`, so an explicit content root isolates
`App_Data/database.db` without changing application code or production data.

From `src/LandStack.Api`, build with `rtk proxy dotnet build -c Release`. Review
any generated DTO diff. Then, from the repository root in PowerShell:

```powershell
$repoRoot = (Get-Location).Path
$smokeRoot = Join-Path $env:TEMP ('landstack-smoke-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $smokeRoot
$apiDll = Join-Path $repoRoot 'src/LandStack.Api/bin/Release/net6.0/LandStack.Api.dll'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
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

## Verification handoff

The upgrade's external feature workspace contains `state.json`, immutable
specifications and `reports/`. Task reports record sequential official Angular
14–22 migrations, clean installation, 49 mocked regression tests, production
builds, meaningful lint/failure probes, and separately built SPA images with HTTP
asset/deep-link checks. Independent task reviews repeat relevant checks. Evidence files include
`01-foundation-attempt1.json`, `02-rest-busy-attempt1.json`,
`03-realtime-routing-attempt1.json`, `04-lint-ci-attempt1.json`,
`05-packaging-attempt1.json` and `05-http-serving.json` under that external
`reports/` directory.

At this documentation task, final exact-commit browser checks against the real
API, two-client broadcast acceptance and feature-wide validation/quality gates
remain pending. Mocked tests and HTTP serving checks alone do not certify those
requirements. Preserve each validator report beside the other external evidence.

Known separately scoped findings remain: .NET 6 is out of support (`NETSDK1138`),
LiteDB 5.0.11 reports `NU1904` for GHSA-3x49-g6rc-c284, and Compose warns about its
obsolete `version` field. npm 12 can report blocked dependency install scripts;
the observed local and image builds passed with the default policy. A successful
API build is compilation evidence; no backend test project is tracked.
