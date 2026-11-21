// WebAssembly render benchmark: Table 1,000 rows, Razor vs PureMarkup, in headless Chromium.
//   1. dotnet publish benchmarks/WasmBench -c Release (trimmed; --aot adds RunAOTCompilation, needs the wasm-tools workload);
//   2. serves the published wwwroot from a small static server (no compression, application/wasm);
//   3. opens the page with Playwright and prints the JSON the page writes to #results, plus a Markdown table.
// Usage: node benchmarks/wasm-bench.mjs [--aot] [--no-publish] [--rounds 7] [--warmup 10] [--iterations 30]
// Needs the .NET 10 SDK and Playwright (cd samples/WebApp && npm ci && npx playwright install chromium).
import { spawnSync } from 'node:child_process';
import { createRequire } from 'node:module';
import fs from 'node:fs';
import http from 'node:http';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const args = process.argv.slice(2);
const arg = (name, def) => { const i = args.indexOf(name); return i >= 0 ? args[i + 1] : def; };
const aot = args.includes('--aot');
const out = path.join(os.tmpdir(), aot ? 'pmblazor-wasm-bench-aot' : 'pmblazor-wasm-bench');

if (!args.includes('--no-publish')) {
  console.log(`publish -c Release${aot ? ' (AOT)' : ''} -> ${out}`);
  fs.rmSync(out, { recursive: true, force: true });
  const r = spawnSync('dotnet', ['publish', path.join(repo, 'benchmarks', 'WasmBench'), '-c', 'Release', '-o', out, '-nologo', '-v', 'q',
    ...(aot ? ['-p:RunAOTCompilation=true'] : [])], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] });
  if (r.status !== 0) { console.error(r.stdout + r.stderr); process.exit(1); }
}

const root = path.join(out, 'wwwroot');
const types = { '.html': 'text/html', '.js': 'text/javascript', '.mjs': 'text/javascript', '.wasm': 'application/wasm', '.json': 'application/json', '.dat': 'application/octet-stream' };
const server = http.createServer((req, res) => {
  const p = decodeURIComponent(new URL(req.url, 'http://x').pathname);
  let file = path.join(root, p === '/' ? 'index.html' : p);
  if (!file.startsWith(root) || !fs.existsSync(file) || fs.statSync(file).isDirectory()) file = path.join(root, 'index.html');
  res.writeHead(200, { 'content-type': types[path.extname(file)] ?? 'application/octet-stream', 'cache-control': 'no-store' });
  fs.createReadStream(file).pipe(res);
});
await new Promise(r => server.listen(0, '127.0.0.1', r));
const url = `http://127.0.0.1:${server.address().port}/?rounds=${arg('--rounds', 7)}&warmup=${arg('--warmup', 10)}&iterations=${arg('--iterations', 30)}`;

const require = createRequire(path.join(repo, 'samples', 'WebApp', 'package.json'));
const { chromium } = require('playwright');
const browser = await chromium.launch();
try {
  const page = await browser.newPage();
  page.on('pageerror', e => console.error('pageerror:', e.message));
  await page.goto(url);
  const handle = await page.waitForFunction(() => {
    const t = document.querySelector('#results')?.textContent ?? '';
    return t.startsWith('{') || t.startsWith('error') ? t : null;
  }, null, { timeout: 600000, polling: 500 });
  const text = await handle.jsonValue();
  if (!text.startsWith('{')) throw new Error(text);
  const r = JSON.parse(text);
  r.browser = `Chromium ${browser.version()}`;
  r.mode = aot ? 'AOT' : 'interpreter';
  console.log(JSON.stringify(r, null, 2));
  const ms = us => (us / 1000).toFixed(2) + ' ms';
  const row = (label, m) => `| ${label} | ${ms(m.Razor)} | ${ms(m.Markup)} | ${(m.Markup / m.Razor).toFixed(2)}x |`;
  console.log(`\n| Table 1,000 rows (${r.mode}) | Razor | Markup | Ratio |\n|---|--:|--:|--:|`);
  console.log(row('Re-render (build + diff + DOM)', r.rerenderUs));
  console.log(row('BuildRenderTree only', r.buildRenderTreeUs));
  console.log(row('First render (+ DOM)', r.firstRenderUs));
} finally {
  await browser.close();
  server.close();
}
