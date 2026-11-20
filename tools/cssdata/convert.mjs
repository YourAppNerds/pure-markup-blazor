// Builds src/PureMarkup.Blazor/Css/css.data from w3c/webref's @webref/css package (MIT), css.json passed as argv[2].
// Usage: npm pack @webref/css && tar xzf webref-css-*.tgz
//        node convert.mjs package/css.json package/package.json > ../../src/PureMarkup.Blazor/Css/css.data
// Names that would clash with HTML element methods or enums (read from Html/html.data) get a "Keyword" suffix.
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

const css = JSON.parse(readFileSync(process.argv[2], 'utf8'));
const version = process.argv[3] ? JSON.parse(readFileSync(process.argv[3], 'utf8')).version : '?';
const html = readFileSync(fileURLToPath(new URL('../../src/PureMarkup.Blazor/Html/html.data', import.meta.url)), 'utf8');

const pascal = s => s.replace(/^-+/, '').split('-').filter(Boolean).map(w => w[0].toUpperCase() + w.slice(1)).join('');

// Type names users see next to these: HTML element methods (via `using static H`), HTML enums, and the styling types.
const reserved = new Set([
  ...[...html.matchAll(/^element (\S+)/gm)].map(m => pascal(m[1])),
  ...[...html.matchAll(/^enum (\S+)/gm)].map(m => m[1]),
  'Text', 'Markup', 'Fragment', 'Content', 'If', 'ForEach', 'Comp', 'Static', 'El', 'Doctype', 'Node', 'Element',
  'Css', 'Rule', 'Var', 'Token', 'Length', 'Color', 'Time', 'Angle', 'CssValue', 'CssUnits', 'Media', 'State', 'Layer',
]);

const props = new Map(css.properties.filter(p => !p.name.startsWith('-') && !p.legacyAliasOf && p.syntax).map(p => [p.name, p]));
const types = new Map(css.types.filter(t => t.syntax).map(t => [t.name, t]));

// Top-level `a | b | c` alternatives (single bar, outside brackets and functions).
function alternatives(syntax) {
  const out = []; let depth = 0, cur = '';
  for (let i = 0; i < syntax.length; i++) {
    const c = syntax[i];
    if ('[(<'.includes(c)) depth++;
    if ('])>'.includes(c)) depth--;
    if (c === '|' && depth === 0 && syntax[i + 1] !== '|' && syntax[i - 1] !== '|') { out.push(cur.trim()); cur = ''; continue; }
    cur += c;
  }
  out.push(cur.trim());
  return out;
}

function balanced(s) {
  let d = 0;
  for (const c of s) { if (c === '[') d++; if (c === ']' && --d < 0) return false; }
  return d === 0;
}
// Splits on a top-level separator such as `||`.
function splitTop(s, sep) {
  const out = []; let depth = 0, cur = '';
  for (let i = 0; i < s.length; i++) {
    const c = s[i];
    if ('[(<'.includes(c)) depth++;
    if ('])>'.includes(c)) depth--;
    if (depth === 0 && s.startsWith(sep, i)) { out.push(cur.trim()); cur = ''; i += sep.length - 1; continue; }
    cur += c;
  }
  out.push(cur.trim());
  return out;
}

const valueKinds = [
  ['length', /^<(length|length-percentage|percentage)( \[[^\]]*\])?>$/],
  ['color', /^<color>$/],
  ['time', /^<time( \[[^\]]*\])?>$/],
  ['angle', /^<angle( \[[^\]]*\])?>$/],
  ['number', /^<(number|integer|opacity-value|alpha-value)( \[[^\]]*\])?>$/],
];

// Keywords and simple value kinds reachable through whole alternatives; anything else stays CssValue-only.
function classify(syntax, seen = new Set()) {
  const kw = [], kinds = new Set();
  const merge = sub => { kw.push(...sub.kw); sub.kinds.forEach(k => kinds.add(k)); };
  for (let alt of alternatives(syntax)) {
    // A single value of a repeated or optional term is valid on its own: drop the multiplier.
    alt = alt.replace(/(\{\d+(,\d*)?\}|[#+?])$/, '').trim();
    const inner = /^\[\s*(.*)\s*\]$/.exec(alt);
    if (inner && balanced(inner[1])) { merge(classify(inner[1], seen)); continue; }
    const parts = splitTop(alt, '||');
    if (parts.length > 1) { for (const part of parts) merge(classify(part.replace(/\?$/, ''), seen)); continue; }
    // `<a>? <b>`: when every other term is optional, the required one alone is valid.
    const terms = splitTop(alt, ' ').filter(Boolean);
    const required = terms.filter(t => !t.endsWith('?'));
    if (terms.length > 1 && required.length === 1) { merge(classify(required[0], seen)); continue; }
    if (/^[a-z][a-z0-9-]*$/.test(alt)) { kw.push(alt); continue; }
    const kind = valueKinds.find(([, re]) => re.test(alt));
    if (kind) { kinds.add(kind[0]); continue; }
    const ref = /^<'([a-z-]+)'>$/.exec(alt) ?? /^<([a-z-]+)>$/.exec(alt);
    const target = ref && (alt.includes("'") ? props.get(ref[1]) : types.get(ref[1]));
    if (target && !seen.has(alt)) {
      seen.add(alt);
      merge(classify(target.syntax, seen));
    }
  }
  return { kw: [...new Set(kw)], kinds };
}

const keywordLines = [], propLines = [];
let keywordTypes = 0;
for (const [name, p] of [...props].sort(([a], [b]) => a.localeCompare(b))) {
  const cs = pascal(name);
  const { kw, kinds } = classify(p.syntax);
  const tokens = [`prop ${name}=${cs}`];
  if (kw.length >= 2) {
    let type = cs;
    if (reserved.has(type)) type += 'Keyword';
    keywordTypes++;
    const members = kw.map(k => { let m = pascal(k); if (/^\d/.test(m)) m = '_' + m; if (m === type) m += 'Value'; return `${k}=${m}`; });
    keywordLines.push(`keywords ${type} ${members.join(' ')}`);
    tokens.push(`kw=${type}`);
  }
  tokens.push(...[...kinds].sort());
  if (p.longhands || /\{\d|\|\||&&/.test(p.syntax)) tokens.push('multi');
  propLines.push(tokens.join(' '));
}

// Legacy aliases (`word-wrap`, `grid-gap`, ...): no method, but valid names for `.Prop` (the PMCSS020 analyzer reads them).
const aliasLines = css.properties.filter(p => !p.name.startsWith('-') && p.legacyAliasOf)
  .sort((a, b) => a.name.localeCompare(b.name)).map(p => `alias ${p.name}=${p.legacyAliasOf}`);
// Descriptors of the at-rules the API writes (`Css.FontFace`), also valid `.Prop` names there.
const descriptorLines = css.atrules.filter(a => a.name === '@font-face')
  .map(a => `descriptors ${a.name} ${a.descriptors.map(d => d.name).sort().join(' ')}`);

const header = [
  '# Generated by tools/cssdata/convert.mjs. Do not edit by hand.',
  `# Source: w3c/webref @webref/css ${version} (MIT): properties without vendor prefix or legacy alias.`,
  '# Formats:',
  '#   keywords <CsType> <keyword>=<Member> ...      (properties whose top-level alternatives hold 2+ keywords)',
  '#   prop <css-name>=<CsName> [kw=<CsType>] [angle] [color] [length] [number] [time] [multi]',
  '#     length covers <length>, <length-percentage> and <percentage>; multi = shorthand or space-separated parts',
  '#   alias <css-name>=<css-name>                    (legacy alias of a property: no method)',
  '#   descriptors <@at-rule> <name> ...              (at-rule descriptors: no method)',
];
process.stdout.write([...header, ...keywordLines, ...propLines, ...aliasLines, ...descriptorLines].join('\n') + '\n');
process.stderr.write(`properties ${propLines.length}, keyword types ${keywordTypes}\n`);
