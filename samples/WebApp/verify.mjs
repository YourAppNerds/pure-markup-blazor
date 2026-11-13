// Usage: node verify.mjs [baseUrl]   (server must already be running: dotnet run --project WebApp)
import { chromium } from 'playwright';

const base = process.argv[2] ?? 'http://127.0.0.1:5191';
const problems = [];
const browser = await chromium.launch();
const page = await browser.newPage();
page.on('console', m => { if (m.type() === 'error') problems.push(`console: ${m.text()}`); });
page.on('pageerror', e => problems.push(`pageerror: ${e.message}`));
page.on('response', r => { if (r.status() >= 400) problems.push(`${r.status()} ${r.url()}`); });
const loaded = new Set();
page.on('response', r => loaded.add(new URL(r.url()).pathname));

function check(cond, msg) { if (!cond) throw new Error(msg); console.log(`ok  ${msg}`); }

// Static SSR + form post (EditForm, InputText bound with .Bind(() => Model!.Name))
await page.goto(base + '/');
check((await page.textContent('#renderer')).includes('Static'), 'home renders statically');
check((await page.getAttribute('#name', 'name')) === 'Model.Name', 'SSR input name comes from the Bind expression');
await page.fill('#name', 'Kyle');
await page.click('#submit');
await page.waitForSelector('#greeting');
check((await page.textContent('#greeting')) === 'Hello, Kyle!', 'SSR form post binds [SupplyParameterFromForm]');

async function counter(path, renderer) {
  await page.goto(base + path);
  await page.waitForFunction(r => document.querySelector('#renderer')?.textContent.includes(`Renderer: ${r}; interactive: True`), renderer, { timeout: 30000 });
  check(true, `${path} interactive on ${renderer}`);
  await page.click('#inc');
  await page.click('#inc');
  await page.waitForFunction(() => document.querySelector('#count').textContent === 'Current count: 2');
  check(true, `${path} counter increments`);
}
await counter('/counter-server', 'Server');
await counter('/counter-wasm', 'WebAssembly');
// Auto: the WASM runtime is cached by now, so Auto should pick WebAssembly; accept either.
await page.goto(base + '/counter-auto');
await page.waitForFunction(() => /interactive: True/.test(document.querySelector('#renderer')?.textContent), null, { timeout: 30000 });
const auto = await page.textContent('#renderer');
await page.click('#inc');
await page.waitForFunction(() => document.querySelector('#count').textContent === 'Current count: 1');
check(true, `/counter-auto increments (${auto})`);

// 50-row ForEach with keyed rows and an InputText per row bound with .Bind(() => row.Name)
await page.goto(base + '/list');
await page.waitForFunction(() => document.querySelector('#renderer')?.textContent.includes('Renderer: Server; interactive: True'), null, { timeout: 30000 });
check((await page.locator('#rows li').count()) === 50, '/list renders 50 rows');
check((await page.inputValue('#name-2')) === 'Item 2', 'row input shows the bound value');
await page.fill('#name-2', 'Zed');
await page.press('#name-2', 'Tab'); // change event
await page.waitForFunction(() => document.querySelector('#summary').textContent === 'Item 1, Zed, Item 3');
check(true, 'editing a row input updates the model and re-renders');
check((await page.inputValue('#name-50')) === 'Item 50', 'other rows keep their values');

check([...loaded].some(p => /^\/_framework\/blazor\.web(\.[a-z0-9]+)?\.js$/.test(p)), 'blazor.web.js loaded');
check([...loaded].some(p => /^\/_framework\/dotnet(\.[a-z0-9]+)?\.js$/.test(p)), 'WASM runtime dotnet.js loaded');
await browser.close();
if (problems.length) { console.error('PROBLEMS:\n' + problems.join('\n')); process.exit(1); }
console.log('ALL OK (no 4xx/5xx, no console errors)');
