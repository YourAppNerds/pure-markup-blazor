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
check(/FormCss_Field-[a-z0-9]{6}/.test(await page.getAttribute('#name', 'class') ?? ''), 'InputText preset (AppPresets.Field) adds its rule class');
check((await page.getAttribute('#name', 'autocomplete')) === 'off', 'InputText preset sets a captured attribute');
check((await page.getAttribute('#submit', 'type')) === 'submit' && /(^| )btn( |$)/.test(await page.getAttribute('#submit', 'class') ?? ''),
  '.With chain (AppPresets.SubmitButton): derived type wins, base classes kept');
check((await page.$eval('#name', e => getComputedStyle(e).borderTopStyle)) === 'solid', 'preset rule CSS applies to the input');
await page.fill('#name', 'Kyle');
await page.click('#submit');
await page.waitForSelector('#greeting');
check((await page.textContent('#greeting')) === 'Hello, Kyle!', 'SSR form post binds [SupplyParameterFromForm]');

// Preset and local layers (CascadePage): computed styles decide, since layer order beats specificity and source order.
await page.goto(base + '/cascade');
const color = sel => page.$eval(sel, e => getComputedStyle(e).color);
const [red, blue, green, orange, purple] = ['rgb(255, 0, 0)', 'rgb(0, 0, 255)', 'rgb(0, 128, 0)', 'rgb(255, 165, 0)', 'rgb(128, 0, 128)'];
check((await page.getAttribute('#preset-vs-local', 'class')).split(' ').some(c => /^CascadeCss_Shared-[a-z0-9]{6}-p$/.test(c)), 'a rule applied in a preset writes its preset-origin class ({name}-p)');
check((await color('#shared-local')) === red, 'the shared rule applies locally (red)');
check((await color('#preset-vs-local')) === blue, 'local base beats preset base, though the shared rule is declared later and also used locally');
check((await color('#preset-hover')) === blue, 'local base value before hover');
await page.hover('#preset-hover');
check((await color('#preset-hover')) === orange, 'preset :hover (pm.preset-state) beats local base value');
check((await color('#local-hover')) === red, 'preset base value before hover');
await page.hover('#local-hover');
check((await color('#local-hover')) === green, 'local :hover (pm.local-state) beats preset :hover');
await page.mouse.move(0, 0);
check((await color('#derived')) === purple, '.With derived rule beats its base rule (base written first in pm.preset)');
check((await color('#inline')) === 'rgb(1, 2, 3)', 'inline .Style beats preset and local rules');

// Theme: tokens follow prefers-color-scheme, the pm-dark / pm-light classes override it, with no re-render.
const surface = sel => page.$eval(sel, e => getComputedStyle(e).backgroundColor);
const [light, dark] = ['rgb(255, 255, 255)', 'rgb(27, 27, 31)'];
check((await surface('body')) === light, 'light token value by default');
check((await surface('#dark-panel')) === dark, '.Theme(ThemeMode.Dark) pins a dark subtree in a light page');
await page.evaluate(() => document.documentElement.classList.add('pm-dark'));
check((await surface('body')) === dark, 'pm-dark on <html> flips the token');
await page.evaluate(() => document.documentElement.classList.remove('pm-dark'));
await page.emulateMedia({ colorScheme: 'dark' });
check((await surface('body')) === dark, 'prefers-color-scheme: dark flips the token without a class');
await page.evaluate(() => document.documentElement.classList.add('pm-light'));
check((await surface('body')) === light, 'pm-light on <html> overrides prefers-color-scheme: dark');
await page.emulateMedia({ colorScheme: 'light' });

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

// QuickGrid through consumer-time typed methods ([assembly: GenerateMarkup(typeof(QuickGrid<>), WholeAssembly = true)])
await page.goto(base + '/grid');
await page.waitForFunction(() => document.querySelector('#renderer')?.textContent.includes('Renderer: Server; interactive: True'), null, { timeout: 30000 });
const names = () => page.$$eval('table.planets tbody tr td:first-child', tds => tds.map(td => td.textContent.trim()).join(','));
check((await names()) === 'Mars,Earth,Jupiter', '/grid renders QuickGrid rows');
check((await page.$$eval('table.planets tbody tr td:last-child', tds => tds.map(td => td.textContent).join(','))) === 'no,no,yes', 'TemplateColumn ChildContent renders');
await page.click('table.planets th:nth-child(2) button.col-title');
await page.waitForFunction(() => document.querySelector('table.planets tbody tr td:first-child')?.textContent.trim() === 'Earth');
check((await names()) === 'Earth,Mars,Jupiter', 'sorting by Moons re-renders the grid');
// QuickGrid's scoped CSS (in the SDK bundle WebApp.styles.css, beside the generated WebApp.pm.css) styles the grid ...
const colTitle = await page.$eval('table.planets th button.col-title', e => { const s = getComputedStyle(e); return `${s.display} ${s.borderTopStyle}`; });
check(colTitle === 'flex none', `QuickGrid scoped CSS applies to the sort button (${colTitle})`);
// ... and a generated rule still styles the same page.
check((await page.$eval('nav', e => getComputedStyle(e).display)) === 'flex', 'generated rule (LayoutCss.Nav) applies on /grid');

// CSS in C#: build-generated, fingerprinted stylesheets linked by StyleLink(); no .css files in the app.
const prerendered = await (await page.request.get(base + '/counter-wasm')).text();
const prerenderClass = /<button id="inc" class="([^"]+)"/.exec(prerendered)?.[1];
check(/CounterCss_Button-[a-z0-9]{6}/.test(prerenderClass ?? ''), `prerendered button has a generated class (${prerenderClass})`);
await page.goto(base + '/counter-wasm');
await page.waitForFunction(() => document.querySelector('#renderer')?.textContent.includes('Renderer: WebAssembly; interactive: True'), null, { timeout: 30000 });
const links = await page.$$eval('link[rel=stylesheet]', ls => ls.map(l => l.getAttribute('href')));
check(links.length === 3 && /^WebApp\.[a-z0-9]+\.styles\.css$/.test(links[0]), `StyleLink links the SDK scoped CSS bundle first (${links[0]})`);
const hrefs = links.slice(1);
check(hrefs.every(h => /^WebApp(\.Client)?\.[a-z0-9]+\.pm\.css$/.test(h)), `StyleLink writes fingerprinted links: ${hrefs.join(', ')}`);
for (const h of hrefs) {
  const r = await page.request.get(base + '/' + h);
  const body = await r.text();
  check(r.status() === 200 && body.includes('@layer reset, vendor, pm.base'), `${h} is 200 and non-empty (${body.length} chars)`);
}
check((await page.getAttribute('#inc', 'class')) === prerenderClass, 'button class identical after WebAssembly took over');
const bg = () => page.$eval('#inc', e => getComputedStyle(e).backgroundColor);
const before = await bg();
await page.hover('#inc');
// Waits out the 150ms transition.
await page.waitForFunction(() => getComputedStyle(document.querySelector('#inc')).backgroundColor === 'rgb(29, 78, 216)', null, { timeout: 5000 });
const after = await bg();
check(before === 'rgb(37, 99, 235)' && after === 'rgb(29, 78, 216)', `preset rule's hover from pm.preset-state applies (${before} -> ${after})`);
await page.mouse.move(0, 0);
await page.click('#inc');
await page.waitForFunction(() => document.querySelector('#bar').getAttribute('style')?.includes(': 10%'));
check((await page.$eval('#bar', e => getComputedStyle(e).width)) !== '0px', '.Set(var, value) drives the bar width');
check((await page.$eval('nav', e => getComputedStyle(e).display)) === 'flex', 'server-assembly rule (LayoutCss.Nav) applies');

check([...loaded].some(p => /^\/_framework\/blazor\.web(\.[a-z0-9]+)?\.js$/.test(p)), 'blazor.web.js loaded');
check([...loaded].some(p => /^\/_framework\/dotnet(\.[a-z0-9]+)?\.js$/.test(p)), 'WASM runtime dotnet.js loaded');
await browser.close();
if (problems.length) { console.error('PROBLEMS:\n' + problems.join('\n')); process.exit(1); }
console.log('ALL OK (no 4xx/5xx, no console errors)');
