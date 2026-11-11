'use strict';

const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const fs = require('node:fs');
const http = require('node:http');
const path = require('node:path');
const { spawn, execFileSync } = require('node:child_process');

const repo = path.resolve(__dirname, '../..');
const spa = path.join(repo, 'src/LandStack-Spa');
const options = {};
for (let i = 2; i < process.argv.length; i += 2) {
  assert(['--output-root', '--dotnet'].includes(process.argv[i]), `Unknown option ${process.argv[i]}`);
  assert(process.argv[i + 1], `Missing value for ${process.argv[i]}`);
  options[process.argv[i]] = process.argv[i + 1];
}
assert(options['--output-root'] && path.isAbsolute(options['--output-root']), '--output-root must be an absolute external directory');
const outputRoot = path.resolve(options['--output-root']);
const git = (...args) => execFileSync('git', ['-C', repo, ...args], { encoding: 'utf8' }).trim();
for (const entry of git('worktree', 'list', '--porcelain').split('\n').filter(line => line.startsWith('worktree '))) {
  const relative = path.relative(path.resolve(entry.slice(9)), outputRoot);
  assert(relative && (path.isAbsolute(relative) || relative === '..' || relative.startsWith(`..${path.sep}`)), 'Output must be outside every worktree');
}
if (fs.existsSync(outputRoot)) assert(!fs.lstatSync(outputRoot).isSymbolicLink(), 'Output root must not be a link');
const lockPath = path.join(spa, 'package-lock.json');
const lock = JSON.parse(fs.readFileSync(lockPath, 'utf8'));
const clientPackagePath = path.join(spa, 'node_modules/@microsoft/signalr/package.json');
const clientPackage = JSON.parse(fs.readFileSync(clientPackagePath, 'utf8'));
const lockedVersion = lock.packages?.['node_modules/@microsoft/signalr']?.version ?? lock.dependencies?.['@microsoft/signalr']?.version;
assert.equal(clientPackage.version, lockedVersion, 'Installed SPA SignalR version must match committed lockfile');
// SignalR 5 predates Node's global fetch and assumes its presence means browser self exists.
// Select its shipped Node HTTP/cookie/abort polyfills only within this test process.
globalThis.fetch = undefined;
const signalr = require(require.resolve('@microsoft/signalr', { paths: [spa] }));
const dotnet = options['--dotnet'] ?? 'dotnet';
const apiDirectory = path.join(repo, 'src/LandStack.Api');
const apiDll = path.join(apiDirectory, 'bin/Release/net11.0/LandStack.Api.dll');
assert(fs.existsSync(apiDll), 'Build the Release API first');
const sourceHash = crypto.createHash('sha256');
for (const file of git('ls-files', '--', 'src/LandStack.Api').split('\n').sort()) {
  sourceHash.update(file).update('\0').update(fs.readFileSync(path.join(repo, file))).update('\0');
}
fs.mkdirSync(outputRoot, { recursive: true });
const run = fs.mkdtempSync(path.join(outputRoot, 'signalr-client-'));
const dataDirectory = path.join(run, 'data');
fs.mkdirSync(dataDirectory);
const report = {
  passed: false, sourceHead: git('rev-parse', 'HEAD'), headTree: git('rev-parse', 'HEAD^{tree}'),
  worktreeStatus: git('status', '--porcelain=v1'),
  stagedPaths: git('diff', '--cached', '--name-only').split('\n').filter(Boolean),
  stagedDiffSha256: crypto.createHash('sha256').update(git('diff', '--cached', '--binary')).digest('hex'),
  runnerSha256: crypto.createHash('sha256').update(fs.readFileSync(__filename)).digest('hex'),
  apiDllSha256: crypto.createHash('sha256').update(fs.readFileSync(apiDll)).digest('hex'),
  apiSourceSha256: sourceHash.digest('hex'), httpEnvironment: 'SignalR 5 shipped Node polyfills; test-process global fetch disabled',
  clientVersion: clientPackage.version, lockedVersion, clientPackagePath,
  lockSha256: crypto.createHash('sha256').update(fs.readFileSync(lockPath)).digest('hex'),
  node: process.version, runDirectory: run, dataDirectory,
  processes: [], checks: [], startedUtc: new Date().toISOString()
};
let server;
let clients = [];
process.once('exit', () => {
  if (server && server.child.exitCode === null && server.child.signalCode === null) server.child.kill('SIGKILL');
});

async function bounded(promise, description, milliseconds = 10000) {
  let timer;
  try {
    return await Promise.race([promise, new Promise((_, reject) => {
      timer = setTimeout(() => reject(new Error(`Timed out: ${description}`)), milliseconds);
    })]);
  } finally { clearTimeout(timer); }
}
const sleep = milliseconds => new Promise(resolve => setTimeout(resolve, milliseconds));
function checked(name, details = {}) { report.checks.push({ name, passed: true, ...details }); }

async function startApi() {
  const logPath = path.join(run, `api-${report.processes.length + 1}.log`);
  const log = fs.createWriteStream(logPath, { flags: 'wx' });
  const env = { ...process.env, ASPNETCORE_ENVIRONMENT: 'Production', DOTNET_ENVIRONMENT: 'Production', LiteDb__DataDirectory: dataDirectory };
  if (path.isAbsolute(dotnet)) env.DOTNET_ROOT = path.dirname(dotnet);
  const child = spawn(dotnet, [apiDll, '--urls', 'http://127.0.0.1:0'], { cwd: apiDirectory, env, windowsHide: true });
  const evidence = { pid: child.pid, startedUtc: new Date().toISOString(), executable: dotnet, dll: apiDll, logPath, stopped: false };
  report.processes.push(evidence);
  const closed = new Promise(resolve => child.once('close', (code, signal) => resolve({ code, signal })));
  let output = '';
  const ready = new Promise((resolve, reject) => {
    child.once('error', reject);
    child.once('exit', (code, signal) => reject(new Error(`API exited before listening: ${code}/${signal}`)));
    child.stdout.on('data', chunk => {
      log.write(chunk);
      output += chunk.toString();
      const address = output.match(/Now listening on: (http:\/\/127\.0\.0\.1:\d+)/);
      if (address) resolve(address[1]);
    });
    child.stderr.on('data', chunk => log.write(chunk));
  });
  server = { child, closed, log, evidence };
  console.log(JSON.stringify({ event: 'api-started', pid: child.pid, runDirectory: run }));
  server.url = await bounded(ready, 'API startup', 15000);
  evidence.url = server.url;
}

async function stopApi() {
  if (!server) return;
  const current = server;
  if (current.child.exitCode === null && current.child.signalCode === null) current.child.kill('SIGTERM');
  try {
    current.evidence.exit = await bounded(current.closed, 'owned API process shutdown', 5000);
  } catch {
    current.child.kill('SIGKILL');
    current.evidence.forcedStop = true;
    current.evidence.exit = await bounded(current.closed, 'forced owned API process shutdown', 5000);
  }
  current.evidence.stopped = true;
  current.evidence.stopRequest = process.platform === 'win32' ? 'SIGTERM (Windows terminates the owned process)' : 'SIGTERM';
  await new Promise(resolve => current.log.end(resolve));
  server = undefined;
}

function request(method, endpoint, payload) {
  return new Promise((resolve, reject) => {
    const body = payload === undefined ? undefined : JSON.stringify(payload);
    const headers = { Origin: 'http://localhost:8080' };
    if (body !== undefined) { headers['Content-Type'] = 'application/json'; headers['Content-Length'] = Buffer.byteLength(body); }
    const req = http.request(new URL(endpoint, server.url), { method, headers }, response => {
      const chunks = [];
      response.on('data', chunk => chunks.push(chunk));
      response.on('error', reject);
      response.on('end', () => {
        const text = Buffer.concat(chunks).toString('utf8');
        resolve({ status: response.statusCode, headers: response.headers, text, json: () => JSON.parse(text) });
      });
    });
    req.setTimeout(5000, () => req.destroy(new Error(`HTTP timeout: ${method} ${endpoint}`)));
    req.on('error', reject);
    req.end(body);
  });
}

async function connectClients() {
  clients = [0, 1].map(index => {
    const client = { index, events: [], warnings: [] };
    client.connection = new signalr.HubConnectionBuilder()
      .withUrl(`${server.url}/todohub`, { transport: signalr.HttpTransportType.WebSockets, headers: { Origin: 'http://localhost:8080' } })
      .configureLogging({ log: (level, message) => { if (level >= signalr.LogLevel.Warning) client.warnings.push({ level, message }); } })
      .build();
    for (const name of ['itemCreated', 'itemUpdated', 'itemDeleted'])
      client.connection.on(name, (...args) => client.events.push({ name, args }));
    return client;
  });
  await bounded(Promise.all(clients.map(c => c.connection.start())), 'two actual SPA-package WebSocket clients');
  const ids = clients.map(c => c.connection.connectionId);
  assert(ids.every(Boolean));
  assert.equal(new Set(ids).size, 2);
  (report.connections ??= []).push({ serverPid: server.child.pid, url: server.url, connectionIds: ids });
}
async function disconnectClients() {
  const closing = clients;
  await bounded(Promise.all(closing.map(c => c.connection.stop())), 'owned clients shutdown');
  for (const client of closing) assert.deepEqual(client.warnings, [], 'Unexpected client warning/error');
  (report.clientLogs ??= []).push(...closing.map(c => ({ index: c.index, warnings: c.warnings })));
  clients = [];
}
async function expectEvent(count, name, assertPayload) {
  await bounded((async () => {
    while (clients.some(client => client.events.length < count)) await sleep(10);
  })(), `both clients receive ${name}`);
  for (const client of clients) {
    assert.equal(client.events.length, count, 'No duplicate or extra events');
    const event = client.events[count - 1];
    assert.equal(event.name, name);
    assert.equal(event.args.length, 1);
    assertPayload(event.args[0]);
  }
}
async function assertNoEvents(count) {
  // Invocation completions provide a message-order barrier after each invalid/no-op operation.
  await bounded(Promise.all(clients.map(c => c.connection.invoke('GetAllItems'))), 'post-operation hub barriers');
  for (const client of clients) assert.equal(client.events.length, count, 'Invalid/no-op operation broadcast an event');
}
function assertItem(item, description, completed) {
  assert.deepEqual(Object.keys(item).sort(), ['createdDate', 'description', 'isCompleted', 'toDoItemId']);
  assert.match(item.toDoItemId, /^[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$/);
  assert.equal(item.description, description);
  assert.equal(item.isCompleted, completed);
  assert.equal(typeof item.createdDate, 'string');
  assert(Number.isFinite(Date.parse(item.createdDate)));
}

(async () => {
  try {
    await startApi();
    const health = await request('GET', '/health');
    assert.equal(health.status, 200);
    assert.equal(health.text, 'Healthy');
    assert.deepEqual((await request('GET', '/todoItem')).json(), []);
    assert.equal((await request('GET', '/openapi/v1.json')).status, 404);
    await connectClients();
    checked('Actual locked SPA SignalR package negotiates two real WebSocket connections', { url: server.url, transport: 'WebSockets' });
    let id;
    await clients[0].connection.send('Create', { description: '  Hub create ✓  ' });
    await expectEvent(1, 'itemCreated', item => { assertItem(item, '  Hub create ✓  ', false); id = item.toDoItemId; });
    assert.equal((await clients[1].connection.invoke('GetAllItems'))[0].toDoItemId, id);
    assert.equal((await request('GET', '/todoItem')).json()[0].toDoItemId, id);
    await clients[1].connection.invoke('ToggleComplete', id);
    await expectEvent(2, 'itemUpdated', item => { assertItem(item, '  Hub create ✓  ', true); assert.equal(item.toDoItemId, id); });
    await clients[0].connection.send('Delete', id);
    await expectEvent(3, 'itemDeleted', deleted => assert.equal(deleted, id));
    checked('Hub send/invoke CRUD broadcasts exact existing item/ID payloads to both clients');

    const created = await request('POST', '/todoItem', { description: '  REST broadcast ✓  ' });
    assert.equal(created.status, 201);
    const restItem = created.json();
    assertItem(restItem, '  REST broadcast ✓  ', false);
    assert.equal(created.headers.location, `${server.url}/todoItem/${restItem.toDoItemId}`);
    await expectEvent(4, 'itemCreated', item => assert.deepEqual(item, restItem));
    assert.equal((await request('POST', `/todoItem/toggleComplete/${restItem.toDoItemId}`)).status, 200);
    await expectEvent(5, 'itemUpdated', item => assertItem(item, restItem.description, true));
    assert.equal((await request('DELETE', `/todoItem/${restItem.toDoItemId}`)).status, 200);
    await expectEvent(6, 'itemDeleted', deleted => assert.equal(deleted, restItem.toDoItemId));
    checked('REST CRUD broadcasts to both old JavaScript clients with preserved status/body/Location');

    for (const payload of [null, {}, { description: null }, { description: '' }, { description: ' \t\r\n' }]) {
      await assert.rejects(bounded(clients[0].connection.invoke('Create', payload), 'invalid hub create'), /Description must contain non-whitespace text/);
      const invalid = await request('POST', '/todoItem', payload);
      assert.equal(invalid.status, 400);
      assert.match(invalid.headers['content-type'], /^application\/problem\+json/);
      assert.equal(invalid.json().status, 400);
      await assertNoEvents(6);
    }
    const missing = crypto.randomUUID();
    await assert.rejects(clients[0].connection.invoke('ToggleComplete', missing), /The to-do item was not found/);
    assert.equal((await request('POST', `/todoItem/toggleComplete/${missing}`)).status, 404);
    await clients[1].connection.invoke('Delete', missing);
    assert.equal((await request('DELETE', `/todoItem/${missing}`)).status, 200);
    await assert.rejects(clients[0].connection.invoke('ToggleComplete', 'not-a-guid'), error => !/stack|JsonException|System\./i.test(error.message));
    assert.equal((await request('POST', '/todoItem/toggleComplete/not-a-guid')).status, 400);
    await assertNoEvents(6);
    assert.deepEqual((await request('GET', '/todoItem')).json(), []);
    checked('Invalid/missing/null/blank inputs and absent deletes produce safe errors/no false events or writes');

    const persistent = (await request('POST', '/todoItem', { description: 'Persist across process restart' })).json();
    await expectEvent(7, 'itemCreated', item => assert.equal(item.toDoItemId, persistent.toDoItemId));
    await clients[1].connection.invoke('ToggleComplete', persistent.toDoItemId);
    await expectEvent(8, 'itemUpdated', item => assert.equal(item.isCompleted, true));
    const beforeRestart = (await request('GET', '/todoItem')).json();
    await disconnectClients();
    await stopApi();
    await startApi();
    await connectClients();
    const restored = (await request('GET', '/todoItem')).json();
    assert.deepEqual(restored, beforeRestart);
    assertItem(restored[0], persistent.description, true);
    for (const client of clients) {
      const realtime = await client.connection.invoke('GetAllItems');
      assert.equal(realtime[0].toDoItemId, persistent.toDoItemId);
      assert.equal(realtime[0].isCompleted, true);
      assert.equal(Date.parse(realtime[0].createdDate), Date.parse(restored[0].createdDate));
    }
    assert.equal((await request('DELETE', `/todoItem/${persistent.toDoItemId}`)).status, 200);
    await expectEvent(1, 'itemDeleted', deleted => assert.equal(deleted, persistent.toDoItemId));
    await clients[0].connection.invoke('Delete', persistent.toDoItemId);
    await assertNoEvents(1);
    assert.deepEqual((await request('GET', '/todoItem')).json(), []);
    checked('Real Kestrel process restart preserves IDs/text/completion/date meaning and clients reconnect to stored state');
    await disconnectClients();
    await stopApi();
    report.passed = true;
  } catch (error) {
    report.error = { message: error.message, stack: error.stack };
    process.exitCode = 1;
  } finally {
    try { await disconnectClients(); } catch (error) { report.cleanupError = error.message; process.exitCode = 1; }
    try { await stopApi(); } catch (error) { report.stopError = error.message; process.exitCode = 1; }
    if (process.exitCode) report.passed = false;
    report.finishedUtc = new Date().toISOString();
    const reportPath = path.join(run, 'result.json');
    fs.writeFileSync(reportPath, JSON.stringify(report, null, 2) + '\n', { flag: 'wx' });
    console.log(JSON.stringify({ passed: report.passed, clientVersion: report.clientVersion, checks: report.checks.length, report: reportPath }));
    if (!report.passed) console.error(report.error ?? report.cleanupError ?? report.stopError);
  }
})();
