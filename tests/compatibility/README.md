# Actual SPA SignalR compatibility check

The runner loads the installed `@microsoft/signalr` package from the existing SPA
and verifies its version against the committed lockfile. It adds no npm packages.
Build the Release API and run the SPA's `npm ci --no-audit --no-fund` first.
From the repository root in PowerShell:

```powershell
$compatibilityOutput = Join-Path $env:TEMP 'LandStack-compatibility'
rtk proxy node tests/compatibility/signalr-client.cjs --output-root $compatibilityOutput
```

An isolated SDK/runtime executable can be selected with
`--dotnet C:\path\to\dotnet.exe`. Otherwise the runner uses `dotnet` on PATH.
It starts only its own built API DLL on an ephemeral loopback port, with fresh
external data, and opens two real WebSocket clients. Checks cover existing hub
`send`/`invoke` and REST CRUD broadcasts, exact payloads/statuses/Location,
invalid-input and missing-item/no-op behavior, and persisted state after stopping
and restarting its API process. Clients fetch the initial list after restart.
Client warning/error logs must be empty. The test removes its synthetic records
through API calls but retains the disposable database and evidence files.

SignalR 5 predates Node's native global `fetch` and assumes browser `self` when
it sees that global. The runner disables global fetch only within its own Node
process so the unchanged package uses its already-locked Node HTTP/cookie/abort
polyfills. REST probes use Node's native HTTP module. No application/browser
code, package source, lockfile or production transport configuration is changed.

Each run creates a unique directory under the absolute external `--output-root`
and refuses output within any repository worktree. `result.json` records the
package version/lock hash, source HEAD/tree/status/staged context, runner/API
DLL/source hashes, process IDs, connection IDs, assertions and failures.
API logs and synthetic data remain beside it. Partial failures preserve evidence;
the runner stops only the clients and API processes it created. Windows SIGTERM
terminates that owned process, so its restart check also exercises persisted
writes across termination; normal graceful container restart is checked separately.

The runner supports prepared staged integration trees and does not assume HEAD
alone identifies uncommitted code. Independent review and final feature gates
must bind its evidence to their exact commit/source context. This executable
compatibility check complements actual browser CRUD/two-client checks and
container non-root/volume verification; it does not certify those checks itself.
