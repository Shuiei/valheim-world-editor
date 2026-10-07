// Sums up the browser tests' coverage (VWE_COVERAGE=<folder> node --test editor.test.mjs, then
// node coverage.mjs <folder>): per script, the share of its lines the tests ran, and the lines they
// never reached. A script loaded several times (page reloads) counts as run where any load ran it.
import fs from 'node:fs';
import path from 'node:path';
const dir = process.argv[2] ?? 'coverage';
const detail = process.argv.includes('--lines');
const files = new Map();   // url -> { text, used: Uint8Array per char }
for (const f of fs.readdirSync(dir).filter(f => f.startsWith('js-') && f.endsWith('.json'))) {
  for (const c of JSON.parse(fs.readFileSync(path.join(dir, f), 'utf8'))) {
    const url = c.url.split('?')[0];
    let e = files.get(url);
    if (!e || e.text.length !== c.text.length) { if (e) continue; e = { text: c.text, used: new Uint8Array(c.text.length) }; files.set(url, e); }
    for (const r of c.ranges) e.used.fill(1, r.start, r.end);
  }
}
const rows = [];
for (const [url, { text, used }] of files) {
  // A line counts when it has code and none of its code ran.
  let pos = 0, total = 0, missed = 0; const gaps = [];
  for (const [i, line] of text.split('\n').entries()) {
    const s = line.trim();
    const code = s && !s.startsWith('//') && !/^[\])};,]+$/.test(s);
    if (code) {
      total++;
      let ran = false;
      for (let k = pos; k < pos + line.length; k++) if (used[k] && line[k - pos].trim()) { ran = true; break; }
      if (!ran) { missed++; const g = gaps[gaps.length - 1]; if (g && g[1] === i) g[1] = i + 1; else gaps.push([i + 1, i + 1]); }
    }
    pos += line.length + 1;
  }
  rows.push({ url, total, missed, gaps });
}
rows.sort((a, b) => (a.total - a.missed) / a.total - (b.total - b.missed) / b.total);
let T = 0, M = 0;
for (const r of rows) {
  T += r.total; M += r.missed;
  console.log(`${(100 * (r.total - r.missed) / r.total).toFixed(0).padStart(4)}%  ${String(r.missed).padStart(5)} of ${String(r.total).padStart(5)} lines missed  ${r.url}`);
  if (detail) console.log('        ' + r.gaps.map(([a, b]) => a === b ? a : `${a}-${b}`).join(', '));
}
console.log(`${(100 * (T - M) / T).toFixed(1)}% of ${T} lines run by the tests`);
