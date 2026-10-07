// Runs the real app (--browser) on a copy of tests/fixtures/CITest in a temporary home folder, with
// stand-in box models instead of the game's (which cannot be in the repo or on CI), and drives it
// with headless Chrome.
//   APP: the program to run (default: ../../out/ValheimWorldEditor, or `dotnet <dll>` via APP_DLL).
import { spawn } from 'node:child_process';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import net from 'node:net';
import { fileURLToPath } from 'node:url';
import puppeteer from 'puppeteer';

const here = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(here, '..', '..');
export const sleep = ms => new Promise(r => setTimeout(r, ms));

// Valheim's String.GetStableHashCode.
export function stableHash(s) {
  let a = 5381 | 0, b = a;
  for (let i = 0; i < s.length; i += 2) {
    a = (Math.imul(a, 33) ^ s.charCodeAt(i)) | 0;
    if (i === s.length - 1) break;
    b = (Math.imul(b, 33) ^ s.charCodeAt(i + 1)) | 0;
  }
  return (a + Math.imul(b, 1566083941)) | 0;
}

const KINDS = ['Beech1', 'Oak1', 'FirTree', 'Rock_4', 'rock4_forest', 'Bush01', 'RaspberryBush', 'Pickable_Mushroom', 'Pickable_Stone',
  'sapling_carrot', 'wood_floor', 'woodwall', 'piece_chest_wood'];

// One box mesh (1 x 1 x 1, sitting on the ground) and a model per kind, in the editor's model format.
function writeBoxModels(dir) {
  for (const d of ['pieces', 'meshes', 'tex']) fs.mkdirSync(path.join(dir, d), { recursive: true });
  const v = [], idx = [];
  const faces = [[[0, 0, 1], [[-.5, 0, .5], [.5, 0, .5], [.5, 1, .5], [-.5, 1, .5]]], [[0, 0, -1], [[.5, 0, -.5], [-.5, 0, -.5], [-.5, 1, -.5], [.5, 1, -.5]]],
    [[1, 0, 0], [[.5, 0, .5], [.5, 0, -.5], [.5, 1, -.5], [.5, 1, .5]]], [[-1, 0, 0], [[-.5, 0, -.5], [-.5, 0, .5], [-.5, 1, .5], [-.5, 1, -.5]]],
    [[0, 1, 0], [[-.5, 1, .5], [.5, 1, .5], [.5, 1, -.5], [-.5, 1, -.5]]], [[0, -1, 0], [[-.5, 0, -.5], [.5, 0, -.5], [.5, 0, .5], [-.5, 0, .5]]]];
  for (const [n, quad] of faces) {
    const base = v.length / 8;
    quad.forEach((p, k) => v.push(...p, ...n, k & 1, k >> 1));
    idx.push(base, base + 2, base + 1, base, base + 3, base + 2);
  }
  const buf = Buffer.alloc(v.length * 4 + idx.length * 4);
  v.forEach((x, i) => buf.writeFloatLE(x, i * 4));
  idx.forEach((x, i) => buf.writeUInt32LE(x, v.length * 4 + i * 4));
  fs.writeFileSync(path.join(dir, 'meshes', 'box.bin'), buf);
  fs.writeFileSync(path.join(dir, 'meshinfo.json'), JSON.stringify({ box: { v: v.length / 8, sub: [idx.length] } }));
  fs.writeFileSync(path.join(dir, 'materials.json'), JSON.stringify({ m: { color: [0.55, 0.42, 0.3] } }));
  const objects = {};
  for (const k of KINDS) {
    const tall = /tree|beech|oak|fir/i.test(k) ? 6 : 1.2;
    fs.writeFileSync(path.join(dir, 'pieces', `${k}.json`), JSON.stringify({ parts: [{ mesh: 'box', sub: 0, mat: 'm', m: [1.2, 0, 0, 0, 0, tall, 0, 0, 0, 0, 1.2, 0, 0, 0, 0, 1] }], rootScale: [1, 1, 1] }));
    objects[stableHash(k)] = k;
  }
  fs.writeFileSync(path.join(dir, 'objects.json'), JSON.stringify(objects));
}

function freePort() {
  return new Promise(resolve => { const s = net.createServer(); s.listen(0, '127.0.0.1', () => { const p = s.address().port; s.close(() => resolve(p)); }); });
}

export async function startApp() {
  const home = fs.mkdtempSync(path.join(os.tmpdir(), 'vwe-e2e-'));
  const worldDir = path.join(home, '.config', 'unity3d', 'IronGate', 'Valheim', 'worlds_local', 'CITest');
  fs.cpSync(path.join(repo, 'tests', 'fixtures', 'CITest'), worldDir, { recursive: true });
  writeBoxModels(path.join(home, '.local', 'share', 'ValheimWorldEditor', 'game-look', 'models'));
  const port = await freePort();
  const app = process.env.APP ?? path.join(repo, 'out', 'ValheimWorldEditor');
  const cmd = process.env.APP_DLL ? 'dotnet' : app, args = process.env.APP_DLL ? [process.env.APP_DLL] : [];
  const env = { ...process.env, HOME: home, VWE_NO_OPEN: '1', XDG_DATA_HOME: path.join(home, '.local', 'share'), XDG_CONFIG_HOME: path.join(home, '.config') };
  const log = [];
  const proc = spawn(cmd, [...args, '--browser', '--port', String(port)], { env, stdio: ['ignore', 'pipe', 'pipe'] });
  proc.stdout.on('data', d => log.push(String(d))); proc.stderr.on('data', d => log.push(String(d)));
  const base = `http://127.0.0.1:${port}`;
  for (let i = 0; ; i++) {
    try { if ((await fetch(base + '/api/app')).ok) break; } catch { }
    if (i > 120 || proc.exitCode != null) throw new Error('the app did not start:\n' + log.join(''));
    await sleep(500);
  }
  // How long a step may take before the test fails (VWE_TIMEOUT, ms): short, so a broken step fails
  // in seconds; CI machines (software 3D on 2 processors) set a longer one.
  const timeout = +(process.env.VWE_TIMEOUT ?? 30000);
  const browser = await puppeteer.launch({ headless: true, protocolTimeout: timeout + 10000, args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--no-sandbox'] });
  const page = await browser.newPage();
  await page.setViewport({ width: 1400, height: 850 });
  page.setDefaultNavigationTimeout(Math.max(timeout, 120000));
  page.setDefaultTimeout(timeout);
  const errors = [];
  page.on('pageerror', e => errors.push(e.message));
  // Confirmations are accepted; prompts get their suggested answer.
  page.on('dialog', d => d.accept(d.type() === 'prompt' ? d.defaultValue() : undefined));
  // VWE_COVERAGE=<folder>: which parts of the page's own scripts the tests ran, written there on stop
  // (summed up by coverage.mjs). Taken before every navigation, since a page's counts go with it.
  const coverDir = process.env.VWE_COVERAGE;
  const cover = new Map();   // url -> { text, used: Uint8Array }
  let cdp = null;
  const sources = new Map();   // scriptId -> source text
  // Never waits more than 30 s for the browser (a page stuck in a long task must not hang the run).
  const within = p => Promise.race([p, new Promise((_, no) => setTimeout(() => no(new Error('timeout')), 30000))]);
  async function takeCoverage() {
    if (!cdp) return;
    let res;
    try { res = await within(cdp.send('Profiler.takePreciseCoverage')); } catch { return; }
    for (const sc of res.result) {
      if (!sc.url.startsWith(base) || sc.url.includes('/lib/')) continue;
      let text = sources.get(sc.scriptId);
      if (text == null) { try { text = (await within(cdp.send('Debugger.getScriptSource', { scriptId: sc.scriptId }))).scriptSource; } catch { continue; } sources.set(sc.scriptId, text); }
      const url = sc.url.slice(base.length).split('?')[0];
      let e = cover.get(url);
      if (!e || e.text !== text) { if (e && e.text.length !== text.length) continue; e = e ?? { text, used: new Uint8Array(text.length) }; cover.set(url, e); }
      // Block coverage: ranges nest, later (inner) ranges override outer ones.
      const mark = new Int8Array(text.length).fill(-1);
      for (const fn of sc.functions) for (const r of fn.ranges) mark.fill(r.count > 0 ? 1 : 0, r.startOffset, r.endOffset);
      for (let i = 0; i < mark.length; i++) if (mark[i] === 1) e.used[i] = 1;
    }
  }
  if (coverDir) {
    cdp = await page.createCDPSession();
    await cdp.send('Debugger.enable'); await cdp.send('Profiler.enable');
    await cdp.send('Profiler.startPreciseCoverage', { callCount: false, detailed: true });
    page.on('request', r => { if (r.isNavigationRequest() && r.frame() === page.mainFrame()) takeCoverage(); });
    for (const m of ['goto', 'reload']) { const f = page[m].bind(page); page[m] = async (...a) => { await takeCoverage(); return f(...a); }; }
  }
  return {
    base, home, worldDir, page, browser, errors, log,
    api: async (p, body) => (await fetch(base + p, body === undefined ? {} : { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) })).json(),
    async stop() {
      if (coverDir) {
        await takeCoverage();
        fs.mkdirSync(coverDir, { recursive: true });
        fs.writeFileSync(path.join(coverDir, `js-${Date.now()}.json`), JSON.stringify([...cover].map(([url, { text, used }]) => {
          const ranges = []; for (let i = 0; i < used.length; i++) if (used[i]) { const st = i; while (i < used.length && used[i]) i++; ranges.push({ start: st, end: i }); }
          return { url, text, ranges };
        })));
      }
      await browser.close();
      proc.kill('SIGTERM');
      await new Promise(r => { if (proc.exitCode != null) r(); else proc.on('exit', r); setTimeout(r, 60000); });
      fs.rmSync(home, { recursive: true, force: true });
    },
  };
}

// Waits until the start page / editor phase is reached.
export async function waitPhase(t, phase) {
  for (let i = 0; i < 240; i++) {
    try { const s = await t.api('/api/launcher/state'); if (s.phase === phase) return; } catch { }
    await sleep(250);
  }
  throw new Error('phase ' + phase + ' not reached');
}

// Screen position of a world point (Unity x, z) on the ground, in the 3D editor.
export const screenOf = (page, x, z, lift = 0) => page.evaluate((x, z, lift) => {
  const ed = window.__ed, gx = x - ed.originX, gz = z - ed.originZ;
  const v = new ed.THREE.Vector3(gx - ed.cx, ed.sampleHeight(gx, gz) + lift, -(gz - ed.cz)).project(ed.camera);
  const r = ed.el.getBoundingClientRect(); return [r.left + (v.x + 1) / 2 * r.width, r.top + (1 - v.y) / 2 * r.height];
}, x, z, lift);

// Waits for the editor to draw n more frames. The camera's matrices (used to turn screen points
// into ground points and back) and the selection arrows only follow a camera move when a frame
// is drawn, and with software 3D under load one frame can take longer than any fixed pause.
export const frames = (page, n = 2) => page.evaluate(n => new Promise(r => {
  window.__ed?.wake?.();   // the editor draws only when something happens: these frames are drawn
  const next = k => k ? requestAnimationFrame(() => next(k - 1)) : r();
  next(n);
}), n);

// Camera looking down at world point (x, z); returns once a frame shows the new view.
export async function lookAt(page, x, z, dx = 0, up = 40, dz = 30) {
  await page.evaluate((x, z, dx, up, dz) => {
    const ed = window.__ed, gx = x - ed.originX, gz = z - ed.originZ, h = ed.sampleHeight(gx, gz), tx = gx - ed.cx, tz = -(gz - ed.cz);
    window.__view(tx + dx, h + up, tz + dz, tx, h, tz);
  }, x, z, dx, up, dz);
  await frames(page);
}
