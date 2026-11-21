// Verifies the dotnet new templates end to end (a passing build proves nothing about a Blazor Web App):
//   1. packs PureMarkup.Blazor and PureMarkup.Blazor.Templates to artifacts/packages and installs the templates into a
//      throwaway hive (--debug:custom-hive), so the user's real template list is untouched;
//   2. instantiates every --interactivity mode, with and without --sample-content, builds and runs each, and checks
//      /, /_framework/blazor.web.js and every linked .pm.css (200), a 404 page, and that the output has no .razor/.html;
//      interactive modes with sample content: Playwright clicks Counter and waits for the count to change;
//      --all-interactive cases: the render mode sits on Routes/HeadOutlet in App.cs, no page has its own, and with
//      WebAssembly/Auto the routes, layout and pages are in the .Client project;
//   3. adds pages with the item template (puremarkup-blazor-page) and checks they compile and route;
//   4. publish cases: dotnet publish -c Release (trimmed WebAssembly), run the output from the publish folder with
//      ASPNETCORE_ENVIRONMENT=Production, check blazor.web.js has its real content, and click Counter again
//      (Auto: a second load that runs on WebAssembly).
// Usage: node scripts/verify-template.mjs [--scratch <dir>] [--only <case>,...] [--no-pack]
// Needs the .NET 10 SDK and Playwright (cd samples/WebApp && npm ci && npx playwright install chromium).
import { spawn, spawnSync } from 'node:child_process';
import { createRequire } from 'node:module';
import fs from 'node:fs';
import net from 'node:net';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const args = process.argv.slice(2);
const arg = name => { const i = args.indexOf(name); return i >= 0 ? args[i + 1] : undefined; };
const scratch = path.resolve(arg('--scratch') ?? path.join(os.tmpdir(), 'pmblazor-template-verify'));
const only = arg('--only')?.split(',');
const feed = path.join(repo, 'artifacts', 'packages');
const hive = path.join(scratch, 'hive');

const cases = [
  { name: 'None', interactivity: 'None' },
  { name: 'Server', interactivity: 'Server', pages: true },
  { name: 'Wasm', interactivity: 'WebAssembly' },
  { name: 'Auto', interactivity: 'Auto' },
  { name: 'NoneSample', interactivity: 'None', sample: true },
  { name: 'ServerSample', interactivity: 'Server', sample: true, publish: true },
  { name: 'WasmSample', interactivity: 'WebAssembly', sample: true, publish: true },
  { name: 'AutoSample', interactivity: 'Auto', sample: true, publish: true },
  { name: 'AutoGlobal', interactivity: 'Auto', global: true },
  { name: 'ServerGlobalSample', interactivity: 'Server', sample: true, global: true },
  { name: 'WasmGlobalSample', interactivity: 'WebAssembly', sample: true, global: true, publish: true },
  { name: 'AutoGlobalSample', interactivity: 'Auto', sample: true, global: true },
].filter(c => !only || only.includes(c.name));

const env = { ...process.env, NUGET_PACKAGES: path.join(scratch, 'nuget-packages'), DOTNET_CLI_TELEMETRY_OPTOUT: '1', DOTNET_NOLOGO: '1' };
const failures = [];
const children = new Set();

function run(cmd, cmdArgs, cwd = repo) {
  const r = spawnSync(cmd, cmdArgs, { cwd, env, encoding: 'utf8', shell: false });
  if (r.status !== 0) {
    const out = (r.stdout ?? '') + (r.stderr ?? '');
    const errors = [...new Set(out.split(/\r?\n/).filter(l => /\berror\b/i.test(l)).map(l => l.replace(/ \[[^\]]+\]$/, '').replace(scratch, '.')))];
    throw new Error(`${cmd} ${cmdArgs.join(' ')} exited ${r.status}\n${(errors.length ? errors.join('\n') : out).slice(-6000)}`);
  }
  return r.stdout;
}
function check(cond, msg) { if (!cond) throw new Error(msg); console.log(`  ok  ${msg}`); }
const freePort = () => new Promise(res => { const s = net.createServer(); s.listen(0, '127.0.0.1', () => { const p = s.address().port; s.close(() => res(p)); }); });

function filesUnder(dir, out = []) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    if (e.isDirectory()) { if (!['bin', 'obj'].includes(e.name)) filesUnder(path.join(dir, e.name), out); }
    else out.push(path.join(dir, e.name));
  }
  return out;
}

async function startApp(projectDir, name, { dll = path.join(projectDir, 'bin', 'Debug', 'net10.0', `${name}.dll`), environment = 'Development' } = {}) {
  const port = await freePort();
  const url = `http://127.0.0.1:${port}`;
  const child = spawn('dotnet', [dll, '--urls', url], {
    cwd: projectDir, env: { ...env, ASPNETCORE_ENVIRONMENT: environment }, stdio: ['ignore', 'pipe', 'pipe'],
  });
  children.add(child);
  let log = '';
  child.stdout.on('data', d => { log += d; });
  child.stderr.on('data', d => { log += d; });
  child.on('exit', () => children.delete(child));
  for (let i = 0; i < 120; i++) {
    if (child.exitCode !== null) throw new Error(`${name} exited ${child.exitCode}\n${log}`);
    try { await fetch(url + '/'); return { url, stop: () => stop(child), log: () => log }; } catch { await new Promise(r => setTimeout(r, 250)); }
  }
  stop(child);
  throw new Error(`${name} did not start\n${log}`);
}
function stop(child) {
  if (child.exitCode !== null) return;
  if (process.platform === 'win32') spawnSync('taskkill', ['/pid', String(child.pid), '/T', '/F']);
  else child.kill('SIGKILL');
  children.delete(child);
}
process.on('exit', () => { for (const c of children) stop(c); });

async function get(url) {
  const r = await fetch(url);
  return { status: r.status, text: await r.text() };
}

// Clicks the Counter button until the count changes (an interactive render mode may take a while to start).
async function clickCounter(page) {
  const status = page.locator('[role=status]');
  let count = 'Current count: 0';
  for (let i = 0; i < 60 && count === 'Current count: 0'; i++) {
    await page.click('button');
    await page.waitForTimeout(500);
    count = await status.textContent();
  }
  return count;
}

function watchProblems(page) {
  const problems = [];
  page.on('pageerror', e => problems.push(`pageerror: ${e.message}`));
  page.on('console', m => { if (m.type() === 'error') problems.push(`console: ${m.text()}`); });
  return problems;
}

// 1. Pack and install into a throwaway hive.
fs.rmSync(scratch, { recursive: true, force: true });
fs.mkdirSync(scratch, { recursive: true });
fs.writeFileSync(path.join(scratch, 'nuget.config'), `<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="puremarkup-local" value="${feed}" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
`);
if (!args.includes('--no-pack')) {
  console.log('pack');
  run('dotnet', ['pack', 'src/PureMarkup.Blazor', '-c', 'Release']);
  run('dotnet', ['pack', 'templates/PureMarkup.Blazor.Templates', '-c', 'Release']);
}
const templatePkg = fs.readdirSync(feed).filter(f => /^PureMarkup\.Blazor\.Templates\..*\.nupkg$/.test(f)).sort().at(-1);
console.log(`install ${templatePkg} (custom hive)`);
run('dotnet', ['new', 'install', path.join(feed, templatePkg), '--debug:custom-hive', hive]);

const require = createRequire(path.join(repo, 'samples', 'WebApp', 'package.json'));
const { chromium } = require('playwright');
const browser = await chromium.launch();

try {
  for (const c of cases) {
    console.log(`\n${c.name}: --interactivity ${c.interactivity}${c.sample ? ' --sample-content' : ''}${c.global ? ' --all-interactive' : ''}`);
    try {
      await verifyCase(c);
    } catch (e) {
      failures.push(c.name);
      console.log(`  FAIL ${e.message}`);
    }
  }
} finally {
  await browser.close();
  for (const c of children) stop(c);
}

async function verifyCase(c) {
  const name = `Pm${c.name}`;
  const out = path.join(scratch, name);
  const newArgs = ['new', 'puremarkup-blazor', '-o', out, '-n', name, '--interactivity', c.interactivity, '--no-restore', '--debug:custom-hive', hive];
  if (c.sample) newArgs.push('--sample-content');
  if (c.global) newArgs.push('--all-interactive');
  run('dotnet', newArgs, scratch);

  const hasClient = c.interactivity === 'WebAssembly' || c.interactivity === 'Auto';
  const serverDir = hasClient ? path.join(out, name) : out;
  const files = filesUnder(out).map(f => path.relative(out, f));
  check(!files.some(f => /\.(razor|html?)$/i.test(f)), `no .razor/.html files (${files.length} files)`);
  check(fs.existsSync(path.join(out, `${name}.Client`)) === hasClient, `.Client project ${hasClient ? 'present' : 'absent'}`);
  const counter = files.find(f => /Counter\.cs$/.test(f));
  check(!!counter === !!(c.sample && c.interactivity !== 'None'), `Counter page ${counter ?? 'absent'}`);
  const read = f => fs.readFileSync(path.join(out, f), 'utf8');
  const inClient = f => f.startsWith(`${name}.Client${path.sep}`);
  if (counter) {
    check(inClient(counter) === hasClient, `Counter in the ${hasClient ? '.Client' : 'server'} project`);
    const mode = { Server: 'InteractiveServer', WebAssembly: 'InteractiveWebAssembly', Auto: 'InteractiveAuto' }[c.interactivity];
    const attrs = read(counter).match(/\[Interactive\w+\]/g) ?? [];
    check(c.global ? attrs.length === 0 : attrs.join() === `[${mode}]`, `Counter render mode: ${attrs.join() || 'none (global)'}`);
  }
  if (c.global) {
    const app = files.find(f => path.basename(f) === 'App.cs');
    check(!inClient(app) && (read(app).match(/\.RenderMode\(Mode\)/g) ?? []).length === 2, 'App.cs: render mode on Routes and HeadOutlet');
    check(!files.some(f => f.endsWith('.cs') && /\[Interactive\w+\]/.test(read(f))), 'no page-level render-mode attribute');
    for (const f of ['Routes.cs', 'MainLayout.cs', 'NavMenu.cs', 'Home.cs', 'NotFound.cs']) {
      const at = files.filter(x => path.basename(x) === f);
      check(at.length === 1 && inClient(at[0]) === hasClient, `${f} in the ${hasClient ? '.Client' : 'server'} project`);
    }
    check(!inClient(files.find(f => path.basename(f) === 'Error.cs')), 'Error.cs stays in the server project');
  }

  // 3. Item template: one page with an explicit namespace and render mode, one from the project's root namespace.
  if (c.pages) {
    run('dotnet', ['restore', serverDir], scratch); // the item template reads RootNamespace from the restored project
    run('dotnet', ['new', 'puremarkup-blazor-page', '-n', 'Orders', '-o', 'Components/Pages', '--namespace', `${name}.Components.Pages`,
      '--interactivity', 'Server', '--debug:custom-hive', hive], serverDir);
    run('dotnet', ['new', 'puremarkup-blazor-page', '-n', 'Reports', '--route', '/reports/all', '--debug:custom-hive', hive], serverDir);
    const orders = fs.readFileSync(path.join(serverDir, 'Components', 'Pages', 'Orders.cs'), 'utf8');
    const reports = fs.readFileSync(path.join(serverDir, 'Reports.cs'), 'utf8');
    check(orders.includes(`namespace ${name}.Components.Pages;`) && orders.includes('[Route("/orders")]') && orders.includes('[InteractiveServer]'),
      'item template: --namespace, default route, --interactivity');
    check(reports.includes(`namespace ${name};`) && reports.includes('[Route("/reports/all")]') && !reports.includes('[Interactive'),
      'item template: root namespace from the project, --route');
  }

  run('dotnet', ['build', serverDir, '-nologo', '-v', 'q'], scratch);
  check(true, 'build');

  const app = await startApp(serverDir, name);
  try {
    const home = await get(app.url + '/');
    check(home.status === 200 && home.text.includes('Hello, world!'), 'GET / 200');
    check((await get(app.url + '/_framework/blazor.web.js')).status === 200, 'GET /_framework/blazor.web.js 200');
    const fingerprinted = home.text.match(/src="(_framework\/blazor\.web\.[^"]+\.js)"/)?.[1];
    check(!!fingerprinted && (await get(`${app.url}/${fingerprinted}`)).status === 200, `GET ${fingerprinted} 200 (script tag)`);
    const css = [...home.text.matchAll(/<link[^>]+href="([^"]+\.pm\.css)"/g)].map(m => m[1]);
    check(css.length === (hasClient ? 2 : 1), `${css.length} .pm.css link(s): ${css.join(', ')}`);
    for (const href of css) {
      const r = await get(new URL(href, app.url + '/').href);
      check(r.status === 200 && r.text.includes('@layer'), `GET ${href} 200`);
    }
    const missing = await get(app.url + '/no-such-page');
    check(missing.status === 404 && missing.text.includes('Not Found'), 'unknown URL: 404 with the NotFound page');

    const page = await browser.newPage();
    const problems = watchProblems(page);
    try {
      await page.goto(app.url + '/');
      const bg = await page.$eval('body', e => getComputedStyle(e).backgroundColor);
      check(bg === 'rgb(255, 255, 255)', `generated CSS applies (body background ${bg})`);
      const sidebar = await page.$eval('main', e => getComputedStyle(e.parentElement.firstElementChild).backgroundImage);
      check(sidebar.includes('linear-gradient'), 'layout CSS applies (sidebar gradient)');

      if (c.sample) {
        await page.goto(app.url + '/weather');
        await page.waitForSelector('table td', { timeout: 10000 });
        check(true, '/weather streams its table');
      }
      if (c.sample && c.interactivity !== 'None') {
        await page.goto(app.url + '/counter');
        const status = page.locator('[role=status]');
        check((await status.textContent()) === 'Current count: 0', '/counter prerenders');
        const count = await clickCounter(page);
        check(count !== 'Current count: 0', `Counter click increments (${count}) under ${c.interactivity}`);
        await page.mouse.move(0, 0); // off the button: its :hover color differs
        const btn = await page.$eval('button', e => getComputedStyle(e).backgroundColor);
        check(btn === 'rgb(27, 110, 194)', `preset button style (${btn})`);
      }
      if (c.pages) {
        await page.goto(app.url + '/orders');
        check((await page.textContent('h1')) === 'Orders', 'item template page /orders routes');
        await page.goto(app.url + '/reports/all');
        check((await page.textContent('h1')) === 'Reports', 'item template page /reports/all routes');
      }
      check(problems.length === 0, `no browser errors${problems.length ? ': ' + problems.join('; ') : ''}`);
    } finally {
      await page.close();
    }
  } finally {
    app.stop();
  }

  if (c.publish) await verifyPublish(c, name, out, serverDir);
}

// 4. A real Release publish, run from the publish folder in Production (no launchSettings, no dev static-asset fallback).
async function verifyPublish(c, name, out, serverDir) {
  const pub = path.join(out, 'publish');
  run('dotnet', ['publish', serverDir, '-c', 'Release', '-o', pub, '-nologo', '-v', 'q'], scratch);
  check(true, 'publish -c Release');
  const app = await startApp(pub, name, { dll: path.join(pub, `${name}.dll`), environment: 'Production' });
  try {
    const home = await get(app.url + '/');
    check(home.status === 200, 'Production: GET / 200');
    const fingerprinted = home.text.match(/src="(_framework\/blazor\.web\.[^"]+\.js)"/)?.[1];
    for (const u of ['_framework/blazor.web.js', fingerprinted]) {
      const r = await get(`${app.url}/${u}`);
      check(r.status === 200 && r.text.length > 100000 && r.text.includes('Blazor'), `Production: GET ${u} 200, ${r.text.length} chars of blazor.web.js`);
    }
    const ctx = await browser.newContext();
    const page = await ctx.newPage();
    const problems = watchProblems(page);
    try {
      await page.goto(app.url + '/counter');
      const count = await clickCounter(page);
      check(count !== 'Current count: 0', `Production: Counter click increments (${count}) under ${c.interactivity}`);
      if (c.interactivity === 'Auto') {
        // The first visit ran on Server while the WebAssembly runtime downloaded; a new load runs on WebAssembly.
        await page.waitForLoadState('networkidle');
        await page.waitForTimeout(2000);
        const sockets = [];
        page.on('websocket', ws => sockets.push(ws.url()));
        await page.goto(app.url + '/counter');
        const again = await clickCounter(page);
        check(again !== 'Current count: 0' && !sockets.some(u => u.includes('_blazor')),
          `Production: Auto second load increments (${again}) on WebAssembly (no circuit)`);
      }
      check(problems.length === 0, `Production: no browser errors${problems.length ? ': ' + problems.join('; ') : ''}`);
    } finally {
      await ctx.close();
    }
  } finally {
    app.stop();
  }
}

console.log(failures.length ? `\nFAILED: ${failures.join(', ')}` : `\nall ${cases.length} cases passed`);
process.exit(failures.length ? 1 : 0);
