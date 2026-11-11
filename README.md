# The LAND Stack

LAND combines LiteDB, Angular, Nginx and .NET in a to-do demo. The backend
uses .NET 11, a small local dispatcher shared by REST and SignalR, and LiteDB.
The existing Angular SPA and its dependencies remain unchanged.

## Local development

Install a stable SDK matching `global.json`; it permits servicing patches within
the selected SDK feature band and selects the native Microsoft Testing Platform
runner. Use the SPA's committed npm lockfile.

From `src/LandStack.Api`:

```text
rtk proxy dotnet run
```

The launch profile uses `http://localhost:5000` and Development. From
`src/LandStack-Spa`, install dependencies and start the SPA on the allowed origin:

```text
rtk proxy npm ci --no-audit --no-fund
rtk proxy npm start -- --port 8080
```

Open `http://localhost:8080` for REST or `/realtime` for SignalR. The API allows
that exact browser origin with credentials. Plain `npm start` defaults to 4200,
which is a different origin. Development SPA URLs use API port 5000.

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

## Containers

From the repository root:

```text
rtk proxy docker compose config --quiet
rtk proxy docker compose build --pull api
rtk proxy docker compose up api
```

The API image builds from API sources alone, runs as non-root UID 1654, listens
on unprivileged port 8080 and publishes `http://localhost:8181`. The named
`appdata` volume mounts at `/app/App_Data`. A new empty volume inherits the
image directory's owner and supports writes without a privileged entrypoint.
The image never changes ownership of an existing volume at startup.

Compose also defines the existing SPA image on port 8080. To start both services,
use `rtk proxy docker compose up --build`. Production SPA URLs point directly to
API port 8181; Nginx serves SPA routes and does not proxy the API or hub.
The SPA/Nginx packaging is unchanged by this backend upgrade.

The API Dockerfile pins tested stable SDK/runtime patch tags. Update those tags
and `global.json` deliberately for .NET servicing releases, rerun checks and
rebuild with `--pull`. Record the resolved image digests for release evidence;
`--pull` fetches the selected tags, so pinned version tags still need updates.

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

## Build and verification

From `src/LandStack.Api` run `rtk proxy dotnet build -c Release`. From the
repository root run:

```text
rtk proxy dotnet test --project tests/LandStack.Api.Tests/LandStack.Api.Tests.csproj -c Release
rtk proxy dotnet list src/LandStack.Api/LandStack.Api.csproj package --include-transitive --vulnerable
rtk proxy docker compose config --quiet
rtk git diff --check
```

After building the API and installing SPA dependencies, run the actual locked
JavaScript client compatibility check from the repository root:

```powershell
$compatibilityOutput = Join-Path $env:TEMP 'LandStack-compatibility'
rtk proxy node tests/compatibility/signalr-client.cjs --output-root $compatibilityOutput
```

It uses two real WebSocket clients and isolated API processes/data; see
[compatibility verification](tests/compatibility/README.md) for prerequisites,
evidence and lifecycle details.

Review audit findings as well as exit codes. From `src/LandStack-Spa`, run
`rtk proxy npm run build -- --configuration production` after installing the
lockfile. Backend tests cover dispatcher behavior, isolated persistence and old
fixtures, HTTP/hub failures, time/serialization and operational endpoints.
Packaging checks also require a real API image build/start, non-root writable
new volume, copied old data, ownership handling and restart persistence.
Browser verification exercises both views and two clients for broadcasts.

The existing frontend test/lint/e2e setup is incomplete; see `AGENTS.md` for the
known missing files/builders. A successful SPA build is compilation and budget
evidence, not frontend behavioral test coverage.
