# LandStack SPA

The frontend uses Angular 22.2.5, standalone components, SCSS and RxJS. Use Node
24.22.2 and npm 12.0.2, matching the root `.node-version` and the package manifest.

From this directory:

```powershell
rtk proxy npm ci --no-audit --no-fund
rtk proxy npm start
```

Open `http://localhost:8080`. Run `npm run lint`, `npm test` for watch mode,
`npm test -- --watch=false` for one-shot Vitest tests, and
`npm run build -- --configuration production` for assets in
`dist/LandStack-Spa/browser` (prefix shell invocations with `rtk proxy`).

The [repository README](../../README.md) owns full setup, mocked component/service
test examples, CI/container details and disposable API/two-client verification.
Karma/Jasmine and TSLint have been replaced; the incomplete Protractor setup was
retired and there is no automated end-to-end command.
