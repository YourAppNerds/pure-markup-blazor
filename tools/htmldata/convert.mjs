// Builds src/PureMarkup.Blazor/Html/html.data from:
//   1. microsoft/vscode-custom-data web-data/data/browsers.html-data.json (MIT), passed as argv[2]
//   2. Blazor's [EventHandler] registrations, from `dotnet run events.cs` (argv[3])
// Usage: node convert.mjs browsers.html-data.json events.txt > ../../src/PureMarkup.Blazor/Html/html.data
import { readFileSync } from 'node:fs';

const data = JSON.parse(readFileSync(process.argv[2], 'utf8'));
const events = readFileSync(process.argv[3], 'utf8').split(/\r?\n/).filter(l => l.startsWith('event '));

// Dropped: obsolete or non-baseline elements.
const skipTags = new Set(['rb', 'param', 'fencedframe', 'selectedcontent']);
// Dropped attributes: obsolete per the HTML spec. Event attributes come from Blazor instead.
const skipAttrs = new Set(['manifest', 'version', 'scoped', 'seamless', 'typemustmatch', 'contextmenu', 'dropzone', 'alink', 'background',
  'bgcolor', 'bottommargin', 'leftmargin', 'rightmargin', 'topmargin', 'vlink', 'link', 'text', 'align', 'noshade', 'compact', 'clear',
  'axis', 'archive', 'codebase', 'codetype', 'declare', 'standby', 'classid', 'profile', 'scheme', 'intrinsicsize',
  'allowpaymentrequest', 'importance', 'moz-opaque', 'sorted', 'nowrap', 'border', 'charset@a']);
const skipValues = { t: new Set(['datetime']) };

// Closed value sets that become C# enums: valueSet -> [enum name, member-name overrides].
const enums = {
  t: ['InputType', { 'datetime-local': 'DateTimeLocal', tel: 'Tel' }],
  bt: ['ButtonType', {}],
  m: ['FormMethod', {}],
  fm: ['FormMethod', {}],
  et: ['FormEnctype', { 'application/x-www-form-urlencoded': 'UrlEncoded', 'multipart/form-data': 'MultipartFormData', 'text/plain': 'TextPlain' }],
  target: ['Target', {}],
  xo: ['CrossOrigin', {}],
  d: ['Dir', {}],
  decoding: ['ImageDecoding', {}],
  loading: ['Loading', {}],
  fetchpriority: ['FetchPriority', {}],
  referrerpolicy: ['ReferrerPolicy', {}],
  pl: ['Preload', {}],
  tk: ['TrackKind', {}],
  sh: ['AreaShape', {}],
  s: ['Scope', {}],
  w: ['TextWrap', {}],
  im: ['InputMode', {}],
  enterkeyhint: ['EnterKeyHint', {}],
  popover: ['PopoverMode', {}],
};
// Value sets that become bool parameters: v = boolean attribute (present/absent), b = "true"/"false", y = "yes"/"no".
const bools = { v: 'bool', b: 'tf', y: 'yn' };
// Boolean attributes the source data leaves untyped.
const boolAttrs = new Set(['autofocus', 'inert', 'nomodule', 'playsinline', 'open', 'default', 'novalidate', 'formnovalidate',
  'allowfullscreen', 'async', 'defer', 'checked', 'controls', 'loop', 'muted', 'autoplay', 'multiple', 'readonly', 'required',
  'reversed', 'selected', 'disabled', 'ismap', 'hidden', 'itemscope']);

// Word pieces for PascalCase names; segmentation picks the fewest pieces.
const pieces = `access key auto capitalize correct focus complete play content editable tab index max min length read only no
validate form action method target src set doc lang href cross origin referrer policy fetch priority item prop ref scope id
col row span count pos size value now text labelled by described error message active descendant role description has popup
multi line selectable shortcuts flow to enter hint input mode spell check export parts virtual keyboard dir name is map use
allow full screen popover http equiv accept charset date time plays inline inert nonce ping rel list step checked disabled
multiple required placeholder pattern width height alt decoding loading sizes media blocking integrity async defer kind
label default poster preload controls loop muted coords shape download headers abbr cols rows wrap open selected optimum low
high reversed start cite data sandbox frame border summary for class style title translate hidden draggable slot part busy
atomic expanded grabbed invalid level live modal orientation owns pressed relevant sort details current drop effect command
shadow root abort activate before copy cut deactivate paste blur cancel can through change click close context menu cue dbl
drag end leave over duration emptied ended in out got pointer capture lock lost mouse down up move wheel progress rate ready
state scroll seeked seeking selection select stalled stop submit suspend timeout update toggle touch volume waiting reset load
loaded meta on playing pause fullscreen directory margin attribution browsing shared storage dialog get post self blank parent
top anonymous use credentials ltr rtl sync eager lazy downgrade when strict unsafe url none metadata subtitles captions
descriptions chapters circle poly rect group verbatim latin prose kana katakana numeric tel email done go next previous search
send manual hidden image module press file radio checkbox color range number week month password button`.split(/\s+/).filter(Boolean);
const dict = new Set(pieces);
const unknown = new Set();
function pascal(name) {
  const out = [];
  for (const part of name.split('-')) {
    const n = part.length, best = Array(n + 1).fill(null); best[0] = [];
    for (let i = 0; i < n; i++) {
      if (!best[i]) continue;
      for (let j = i + 1; j <= n; j++) {
        const w = part.slice(i, j);
        if (dict.has(w) && (!best[j] || best[j].length > best[i].length + 1)) best[j] = [...best[i], w];
      }
    }
    if (!best[n]) unknown.add(part);
    out.push(...(best[n] ?? [part]).map(w => w[0].toUpperCase() + w.slice(1)));
  }
  return out.join('');
}
// Hand-fixed names where segmentation guesses wrong.
const nameOverrides = {
  ismap: 'IsMap', usemap: 'UseMap', srclang: 'SrcLang', datetime: 'DateTime', formenctype: 'FormEnctype', enctype: 'Enctype',
  'http-equiv': 'HttpEquiv', itemscope: 'ItemScope', itemprop: 'ItemProp', itemref: 'ItemRef', itemid: 'ItemId', itemtype: 'ItemType',
};
const csName = n => nameOverrides[n] ?? pascal(n);

const header = [
  '# Generated by tools/htmldata/convert.mjs. Do not edit by hand.',
  `# Elements and attributes: microsoft/vscode-custom-data browsers.html-data.json v${data.version} (MIT).`,
  '# Events: Blazor [EventHandler] registrations (tools/htmldata/events.cs), .NET 10.',
  '# Formats:',
  '#   enum <CsName> <value>=<Member> ...',
  '#   global: <attr>=<CsName>[:<kind>] ...   kind: bool (boolean attribute), tf ("true"/"false"), yn ("yes"/"no"), or an enum',
  '#   element <tag>[ void]: <attr>=<CsName>[:<kind>] ...',
  '#   event <name>=<CsName> <EventArgsType>',
];

const used = new Map();
function attrTok(a) {
  let kind = boolAttrs.has(a.name) ? 'bool' : '';
  if (kind) { /* known boolean attribute */ }
  else if (a.valueSet && bools[a.valueSet]) kind = bools[a.valueSet];
  else if (a.valueSet && enums[a.valueSet]) { kind = enums[a.valueSet][0]; if (!used.has(kind)) used.set(kind, a.valueSet); }
  return `${a.name}=${csName(a.name)}${kind ? ':' + kind : ''}`;
}
const body = [];
const globals = data.globalAttributes.filter(a => !a.name.startsWith('on') && !skipAttrs.has(a.name));
body.push(`global: ${globals.map(attrTok).join(' ')}`);
let elementCount = 0, attrCount = 0;
for (const t of data.tags) {
  if (skipTags.has(t.name)) continue;
  elementCount++;
  const attrs = t.attributes.filter(a => !skipAttrs.has(a.name) && !a.name.startsWith('on') && !globals.some(g => g.name === a.name));
  attrCount += attrs.length;
  body.push(`element ${t.name}${t.void ? ' void' : ''}: ${attrs.map(attrTok).join(' ')}`.trimEnd());
}
const enumLines = [];
for (const [cs, set] of used) {
  const vs = data.valueSets.find(v => v.name === set);
  const over = Object.values(enums).find(e => e[0] === cs)[1];
  const vals = vs.values.map(v => v.name).filter(v => !skipValues[set]?.has(v));
  enumLines.push(`enum ${cs} ${vals.map(v => `${v}=${over[v] ?? pascal(v.replace(/^_/, ''))}`).join(' ')}`);
}
const eventLines = events.map(e => {
  const [, name, type] = e.split(' ');
  return `event ${name}=${csName(name)} ${type}`;
});
process.stdout.write([...header, ...enumLines, ...body, ...eventLines].join('\n') + '\n');
process.stderr.write(`elements ${elementCount}, element attributes ${attrCount}, globals ${globals.length}, events ${events.length}, enums ${used.size}\n`);
if (unknown.size) process.stderr.write('unsegmented: ' + [...unknown].join(' ') + '\n');
