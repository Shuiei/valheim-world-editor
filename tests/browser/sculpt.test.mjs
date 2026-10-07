// End-to-end, sculpting: brushes, stamps, heightmaps, erosion, shapes and paths: the real app on its own copy of the test world, as a user drives it.
import { describe, test } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { waitPhase, screenOf, lookAt, frames, sleep, stableHash } from './harness.mjs';
import { t, setup, page, noErrors, pending, openEditor, areaAction, applyArea, records } from './common.mjs';

setup({ open: true });

describe('Sculpting: brushes, stamps, heightmaps, erosion, shapes and paths', () => {

test('brush shapes: square, ring and falloff change where the brush works', async () => {
  await openEditor();
  await page().keyboard.press('1');
  const w = await page().evaluate(() => {
    const ed = window.__ed, sel = (id, v) => { const e = document.getElementById(id); e.value = v; e.dispatchEvent(new Event('input')); };
    const at = (x, z) => ed.brush.weight(x, z, 10);
    sel('bShape', 'circle'); sel('bFalloff', 'smooth');
    const circleCorner = at(8, 8), circleMid = at(0, 0);
    sel('bShape', 'square'); const squareCorner = at(8, 8);
    sel('bShape', 'ring'); const ringMid = at(0, 0), ringBand = at(7, 0);
    sel('bShape', 'circle'); sel('bFalloff', 'linear'); const linearHalf = at(5, 0);
    sel('bFalloff', 'flat'); const flatNear = at(7.5, 0);
    sel('bShape', 'square'); sel('bTurn', 45); const turnedCorner = at(8, 8);
    sel('bTurn', 0); sel('bFalloff', 'smooth');
    return { circleCorner, circleMid, squareCorner, ringMid, ringBand, linearHalf, flatNear, turnedCorner };
  });
  assert.equal(w.circleCorner, 0, 'a circle does not reach its box corners');
  assert.equal(w.circleMid, 1);
  assert.ok(w.squareCorner > 0, 'a square does');
  assert.equal(w.ringMid, 0, 'a ring leaves its middle alone');
  assert.equal(w.ringBand, 1);
  assert.ok(Math.abs(w.linearHalf - 0.5) < 1e-6, 'linear falloff is half way at half the radius');
  assert.equal(w.flatNear, 1, 'flat top is full strength near the edge');
  assert.equal(w.turnedCorner, 0, 'turned 45°, the square no longer reaches that corner');
  // A stroke with the square brush changes the ground.
  const start = await pending();
  const c = (await records()).find(r => r.name === 'piece_chest_wood');
  await lookAt(page(), c.x + 20, c.z - 20);
  const [x, y] = await screenOf(page(), c.x + 20, c.z - 20);
  await page().mouse.move(x, y); await page().mouse.down(); await page().mouse.move(x + 3, y + 3, { steps: 15 }); await page().mouse.up(); await sleep(800);
  assert.notEqual(await pending(), start, 'the square brush changed the ground');
  await page().click('#undo'); await sleep(1000);
  // The Ring shape shows its inner edge too.
  const inner = () => page().evaluate(() => window.__ed.scene.children.filter(o => o.isLineLoop && o.renderOrder === 10 && o.material.opacity < 1)[0]?.visible);
  await page().evaluate(() => { const e = document.getElementById('bShape'); e.value = 'ring'; e.dispatchEvent(new Event('input')); });
  await page().mouse.move(x + 1, y + 1); await frames(page());
  assert.equal(await inner(), true, 'the ring brush draws its inner edge');
  await page().evaluate(() => { const e = document.getElementById('bShape'); e.value = 'circle'; e.dispatchEvent(new Event('input')); });
  await page().mouse.move(x, y); await frames(page());
  assert.equal(await inner(), false, 'a circle does not');
  noErrors();
});

test('stamps: a mesa stamped once, and a picture loaded as a stamp', async () => {
  await openEditor();
  await page().keyboard.press('1');
  const c = (await records()).find(r => r.name === 'piece_chest_wood');
  const spot = [c.x + 22, c.z + 22];
  await page().evaluate(() => {
    const s = (id, v) => { const e = document.getElementById(id); e.value = v; e.dispatchEvent(new Event('input')); };
    s('bShape', 'stamp:mesa'); s('radius', 8); document.getElementById('stOnce').checked = true; document.getElementById('stOnce').dispatchEvent(new Event('input')); s('stHeight', 3);
  });
  const before = await page().evaluate(([x, z]) => { const ed = window.__ed; return ed.sampleHeight(x - ed.originX, z - ed.originZ); }, spot);
  await lookAt(page(), ...spot);
  const [x, y] = await screenOf(page(), ...spot);
  await page().mouse.click(x, y); await sleep(800);
  const after = await page().evaluate(([x, z]) => { const ed = window.__ed; return ed.sampleHeight(x - ed.originX, z - ed.originZ); }, spot);
  assert.ok(Math.abs(after - before - 3) < 0.3, `the mesa's flat top is 3 m up (${(after - before).toFixed(2)})`);
  await page().click('#undo'); await sleep(800);
  // A picture: a white square on black, loaded from a file.
  const png = await page().evaluate(() => { const c = document.createElement('canvas'); c.width = c.height = 32; const g = c.getContext('2d'); g.fillStyle = '#000'; g.fillRect(0, 0, 32, 32); g.fillStyle = '#fff'; g.fillRect(8, 8, 16, 16); return c.toDataURL('image/png'); });
  const file = path.join(t.home, 'square-stamp.png');
  fs.writeFileSync(file, Buffer.from(png.split(',')[1], 'base64'));
  await (await page().$('#stFile')).uploadFile(file);
  await page().waitForFunction(() => document.getElementById('bShape').selectedOptions[0]?.text === 'Stamp: square-stamp');
  const w = await page().evaluate(() => [window.__ed.brush.weight(0, 0, 10), window.__ed.brush.weight(9, 9, 10)]);
  assert.ok(w[0] > 0.9 && w[1] < 0.05, `white middle works, black corner not (${w})`);
  await page().evaluate(() => { const s = (id, v) => { const e = document.getElementById(id); e.value = v; e.dispatchEvent(new Event('input')); }; document.getElementById('stOnce').checked = false; s('bShape', 'circle'); });
  noErrors();
});

test('heightmap: an exported area imports back unchanged, a white picture lifts a selection', async () => {
  await openEditor();
  await page().evaluate(() => window.__ed.setTool('area'));
  const png = Buffer.from(await (await fetch(`${t.base}/api/heightmap.png?x0=-1&z0=-1&x1=1&z1=1`)).arrayBuffer());
  const file = path.join(t.home, 'area.png'); fs.writeFileSync(file, png);
  const heightsNow = () => page().evaluate(() => { const ed = window.__ed, out = []; for (let g = 0; g < ed.N; g += 997) out.push(ed.height(g)); return out; });
  const before = await heightsNow();
  await areaAction('heightmap');
  await (await page().$('#hmFile')).uploadFile(file);
  await page().waitForFunction(() => !document.getElementById('hmBox').hidden);
  await page().click('#hmApply'); await sleep(800);
  const after = await heightsNow();
  const worst = Math.max(...before.map((h, i) => Math.abs(h - after[i])));
  assert.ok(worst < 0.02, `the ground came back within 2 cm (${worst.toFixed(4)} m)`);
  await page().click('#undo'); await sleep(800);
  // A white picture (8-bit RGBA, as browsers write PNGs) into a box selection: everything goes to Highest.
  const c = (await records()).find(r => r.name === 'piece_chest_wood');
  const spot = [c.x + 25, c.z - 25];
  await lookAt(page(), ...spot, 0, 50, 35);
  const [ax, ay] = await screenOf(page(), spot[0] - 6, spot[1] - 6), [bx, by] = await screenOf(page(), spot[0] + 6, spot[1] + 6);
  await page().mouse.move(ax, ay); await page().mouse.down(); await page().mouse.move(bx, by, { steps: 6 }); await page().mouse.up();
  await page().$eval('#aSoft', e => { e.value = 0; e.dispatchEvent(new Event('input')); });
  const white = await page().evaluate(() => { const cv = document.createElement('canvas'); cv.width = cv.height = 16; const g = cv.getContext('2d'); g.fillStyle = '#fff'; g.fillRect(0, 0, 16, 16); return cv.toDataURL('image/png'); });
  const wfile = path.join(t.home, 'white.png'); fs.writeFileSync(wfile, Buffer.from(white.split(',')[1], 'base64'));
  const h0 = await page().evaluate(([x, z]) => { const ed = window.__ed; return ed.sampleHeight(x - ed.originX, z - ed.originZ); }, spot);
  await areaAction('heightmap');
  await (await page().$('#hmFile')).uploadFile(wfile);
  await page().waitForFunction(() => !document.getElementById('hmBox').hidden);
  await page().$eval('#hmMax', (e, v) => { e.value = v; }, (h0 + 2).toFixed(1));
  await page().click('#hmApply'); await sleep(800);
  const h1 = await page().evaluate(([x, z]) => { const ed = window.__ed; return ed.sampleHeight(x - ed.originX, z - ed.originZ); }, spot);
  assert.ok(Math.abs(h1 - (+(h0 + 2).toFixed(1))) < 0.05, `lifted to Highest (${h0.toFixed(2)} -> ${h1.toFixed(2)})`);
  await page().click('#undo'); await sleep(800);
  await page().$eval('#aSoft', e => { e.value = 3; e.dispatchEvent(new Event('input')); });
  noErrors();
});

test('erosion: thermal settles a spike to its rest angle, the Area action erodes', async () => {
  await openEditor();
  const r = await page().evaluate(async () => {
    const { thermal, hydraulic } = await import('/editor/erosion.js');
    const w = 21, h = new Float32Array(w * w), wt = new Float32Array(w * w).fill(1);
    h[10 * w + 10] = 20;
    const volume = a => a.reduce((s, v) => s + v, 0), v0 = volume(h);
    thermal(h, wt, w, w, 1, 1, 400);
    let steepest = 0;
    for (let z = 1; z < w - 1; z++) for (let x = 1; x < w - 1; x++) steepest = Math.max(steepest, h[z * w + x] - h[z * w + x + 1], h[z * w + x] - h[(z + 1) * w + x]);
    const g = new Float32Array(w * w).map((_, i) => (i % w) * 0.5 + Math.sin(i) * 0.2);
    hydraulic(g, wt, w, w, 300, 1, (() => { let s = 1; return () => (s = (s * 16807) % 2147483647) / 2147483647; })());
    return { kept: Math.abs(volume(h) - v0), peak: h[10 * w + 10], steepest, finite: g.every(Number.isFinite) };
  });
  assert.ok(r.kept < 1e-3, `thermal moves ground, never loses it (${r.kept})`);
  assert.ok(r.peak < 4, `the spike came down (${r.peak.toFixed(2)})`);
  assert.ok(r.steepest < 1.3, `slopes near the rest angle (${r.steepest.toFixed(2)})`);
  assert.ok(r.finite, 'water erosion stays finite');
  // The Area tool's Erode changes the ground of a selection.
  await page().evaluate(() => window.__ed.setTool('area'));
  const c = (await records()).find(r => r.name === 'piece_chest_wood');
  const spot = [c.x - 25, c.z + 25];
  await lookAt(page(), ...spot, 0, 50, 35);
  const [ax, ay] = await screenOf(page(), spot[0] - 10, spot[1] - 10), [bx, by] = await screenOf(page(), spot[0] + 10, spot[1] + 10);
  await page().mouse.move(ax, ay); await page().mouse.down(); await page().mouse.move(bx, by, { steps: 6 }); await page().mouse.up();
  const start = await pending();
  await applyArea('erode'); await sleep(1500);
  assert.notEqual(await pending(), start, 'the ground changed');
  await page().click('#undo'); await sleep(1000);
  noErrors();
});

test('shapes: formulas are read safely, and a click puts a mesa in', async () => {
  await openEditor();
  const f = await page().evaluate(async () => {
    const { compile } = await import('/editor/shapes.js');
    const run = (src, env = {}) => compile(src, ['x', 'z', 'd', 'r', 'h'])({ x: 2, z: 3, d: 4, r: 10, h: 5, n: () => 0, ...env });
    const err = src => { try { compile(src, ['x', 'z', 'd', 'r', 'h']); return null; } catch (e) { return e.message; } };
    return { prec: run('1 + 2 * 3 ^ 2'), neg: run('-2 ^ 2'), fn: run('max(x, z) + smooth(0.5)'), tern: run('d < r ? h : 0'), cmp: run('x > z'),
      unknown: err('foo + 1'), func: err('nope(1)'), open: err('(1 + 2'), code: err('alert(1)'), empty: err('  ') };
  });
  assert.equal(f.prec, 19); assert.equal(f.neg, -4); assert.equal(f.fn, 3.5); assert.equal(f.tern, 5); assert.equal(f.cmp, 0);
  assert.match(f.unknown, /no “foo”/); assert.match(f.func, /no function/); assert.match(f.open, /“\)” expected/); assert.ok(f.code); assert.match(f.empty, /empty/);
  await page().keyboard.press('g');
  await page().evaluate(() => { const s = (id, v) => { const e = document.getElementById(id); e.value = v; e.dispatchEvent(new Event('input')); }; s('shPreset', 'mesa'); s('shRadius', 10); s('shHeight', 3); });
  const c = (await records()).find(r => r.name === 'piece_chest_wood');
  const spot = [c.x - 22, c.z - 22];
  const h0 = await page().evaluate(([x, z]) => { const ed = window.__ed; return ed.sampleHeight(x - ed.originX, z - ed.originZ); }, spot);
  await lookAt(page(), ...spot);
  const [x, y] = await screenOf(page(), ...spot);
  await page().mouse.click(x, y); await sleep(800);
  const h1 = await page().evaluate(([x, z]) => { const ed = window.__ed; return ed.sampleHeight(x - ed.originX, z - ed.originZ); }, spot);
  assert.ok(Math.abs(h1 - h0 - 3) < 0.2, `the mesa's top is 3 m up (${(h1 - h0).toFixed(2)})`);
  await page().click('#undo'); await sleep(800);
  noErrors();
});

test('path: a river digs its bed below sea level (down to the game limit)', async () => {
  await openEditor();
  await page().keyboard.press('p');
  await page().select('#pAction', 'river');
  await page().$eval('#pDepth', e => { e.value = 1.5; e.dispatchEvent(new Event('input')); });
  const c = (await records()).find(r => r.name === 'piece_chest_wood');
  const a = [c.x - 30, c.z + 30], b = [c.x - 10, c.z + 30], mid = [c.x - 20, c.z + 30];
  await lookAt(page(), ...mid, 0, 50, 30);
  const height = p => page().evaluate(([x, z]) => { const ed = window.__ed; return { h: ed.sampleHeight(x - ed.originX, z - ed.originZ), base: ed.base[Math.round(z - ed.originZ) * ed.W + Math.round(x - ed.originX)], water: ed.WATER }; }, p);
  const before = await height(mid);
  for (const p of [a, b]) { const [x, y] = await screenOf(page(), ...p); await page().mouse.click(x, y); await sleep(150); }
  await page().keyboard.press('Enter'); await sleep(800);
  const after = await height(mid);
  const want = Math.max(before.water - 1.5, before.base - 8);
  assert.ok(Math.abs(after.h - want) < 0.3, `the bed is at ${want.toFixed(2)} m (got ${after.h.toFixed(2)})`);
  if (before.base - 8 > before.water - 1.5) assert.match(await page().$eval('#sMsg', e => e.textContent), /limit/);
  await page().keyboard.press('Escape');
  await page().click('#undo'); await sleep(800);
  noErrors();
});

test('sharp edges: the pickaxe falloff, and a straight edge along a diagonal path with no soft edge', async () => {
  await openEditor();
  await page().keyboard.press('2');
  const w = await page().evaluate(() => {
    const ed = window.__ed, sel = (id, v) => { const e = document.getElementById(id); e.value = v; e.dispatchEvent(new Event('input')); };
    sel('bShape', 'circle'); sel('bFalloff', 'sharp');
    const r = [ed.brush.weight(0, 0, 10), ed.brush.weight(9.9, 0, 10), ed.brush.weight(10.1, 0, 10)];
    sel('bFalloff', 'smooth');
    return r;
  });
  assert.deepEqual(w, [1, 1, 0], 'full strength right to the edge, nothing beyond');
  // A path lowered 2 m along a diagonal, no soft edge: along a line across the edge the depth goes
  // from full to nothing within about a metre, with no ground point in between left at the step.
  await page().keyboard.press('p');
  await page().select('#pAction', 'lower');
  await page().$eval('#pAmount', e => { e.value = 2; e.dispatchEvent(new Event('input')); });
  await page().$eval('#pSoft', e => { e.value = 0; e.dispatchEvent(new Event('input')); });
  await page().$eval('#pWidth', e => { e.value = 6; e.dispatchEvent(new Event('input')); });
  const c = (await records()).find(r => r.name === 'piece_chest_wood');
  const A = [c.x + 25, c.z + 5], B = [c.x + 45, c.z + 25];
  await lookAt(page(), c.x + 35, c.z + 15, 0, 45, 30);
  for (const p of [A, B]) { const [x, y] = await screenOf(page(), ...p); await page().mouse.click(x, y); await sleep(150); }
  await page().evaluate(() => { window.__levelBefore = window.__ed.level.slice(); });
  await page().keyboard.press('Enter'); await sleep(500);
  const prof = await page().evaluate(([A, B]) => {
    const before = window.__levelBefore;
    const ed = window.__ed, mx = (A[0] + B[0]) / 2 - ed.originX, mz = (A[1] + B[1]) / 2 - ed.originZ;
    // Ground points along the path's crosswise direction (-1, 1)/√2 near its edge (3 m from the line).
    const out = [];
    for (let k = -8; k <= 8; k++) {
      const d = 3 + k * 0.25, gx = Math.round(mx - d / Math.SQRT2), gz = Math.round(mz + d / Math.SQRT2), g = gz * ed.W + gx;
      out.push({ dist: Math.abs(-(gx - mx) + (gz - mz)) / Math.SQRT2, dug: before[g] - ed.level[g] });
    }
    return out;
  }, [A, B]);
  for (const p of prof) {
    if (p.dist < 2.5) assert.ok(p.dug > 1.99, `full depth inside (${p.dist.toFixed(2)} m: ${p.dug.toFixed(2)})`);
    if (p.dist > 3.5) assert.ok(p.dug < 0.01, `untouched outside (${p.dist.toFixed(2)} m: ${p.dug.toFixed(2)})`);
    if (p.dist >= 2.5 && p.dist <= 3.5) assert.ok(Math.abs(p.dug - 2 * Math.max(0, Math.min(1, 3.5 - p.dist))) < 0.02, `the edge follows the line (${p.dist.toFixed(2)} m: ${p.dug.toFixed(2)})`);
  }
  await page().keyboard.press('Escape');
  await page().click('#undo'); await sleep(800);
  await page().$eval('#pSoft', e => { e.value = 4; e.dispatchEvent(new Event('input')); });
  noErrors();
});

test('every sculpt and paint brush does its job: lower, raise, restore, smooth, natural, the four paints', async () => {
  await openEditor();
  const c = (await records()).find(r => r.name === 'piece_chest_wood');
  const spot = [c.x + 25, c.z - 15];
  await lookAt(page(), ...spot, 0, 40, 30);
  const at = () => page().evaluate(([x, z]) => { const ed = window.__ed, g = Math.round(z - ed.originZ) * ed.W + Math.round(x - ed.originX); return { h: ed.height(g), base: ed.base[g], pmod: ed.pmod[g], paint: Array.from(ed.paint.slice(g * 4, g * 4 + 4)) }; }, spot);
  const stroke = async key => {
    await page().keyboard.press(key);
    const [x, y] = await screenOf(page(), ...spot);
    await page().mouse.move(x, y); await page().mouse.down(); await page().mouse.move(x + 3, y + 3, { steps: 20 }); await page().mouse.up(); await sleep(400);
  };
  const h0 = (await at()).h;
  await stroke('2');
  const low = (await at()).h;
  assert.ok(low < h0 - 0.05, 'Lower digs');
  const radius = v => page().$eval('#radius', (e, v) => { e.value = v; e.dispatchEvent(new Event('input')); }, v);
  await radius(2);
  await stroke('1'); await stroke('1'); await stroke('1');
  await radius(8);
  const raised = (await at()).h;
  // Smooth: the ground around the spot gets less bumpy (each point nearer the average of its neighbours).
  const bumps = () => page().evaluate(([x, z]) => {
    const ed = window.__ed, cx = Math.round(x - ed.originX), cz = Math.round(z - ed.originZ), H = (gx, gz) => ed.height(gz * ed.W + gx);
    let s = 0;
    for (let gz = cz - 3; gz <= cz + 3; gz++) for (let gx = cx - 3; gx <= cx + 3; gx++) {
      let a = 0; for (let dz = -1; dz <= 1; dz++) for (let dx = -1; dx <= 1; dx++) a += H(gx + dx, gz + dz);
      s += Math.abs(H(gx, gz) - a / 9);
    }
    return s;
  }, spot);
  const rough = await bumps();
  await stroke('4');
  { const r2 = await bumps(); assert.ok(r2 < rough, `Smooth evens it out (bumps ${rough.toFixed(3)} -> ${r2.toFixed(3)})`); }
  const beforeNatural = (await at()).h;
  await stroke('0');
  assert.ok(Math.abs((await at()).h - beforeNatural) > 0.001, 'Natural changes the ground');
  const off = Math.abs((await at()).h - (await at()).base);
  await stroke('5'); await stroke('5'); await stroke('5');
  assert.ok(Math.abs((await at()).h - (await at()).base) < off, 'Restore brings it back towards the generated ground');
  for (const [key, ch, name] of [['6', 0, 'Dirt'], ['7', 1, 'Cultivate'], ['8', 2, 'Paved']]) {
    await stroke(key); await stroke(key);
    const p = await at();
    assert.ok(p.pmod && p.paint[ch] > 0.5, `${name} paints its channel (${p.paint.map(v => v.toFixed(2))})`);
  }
  await stroke('9'); await stroke('9'); await stroke('9');
  const cleared = await at();
  assert.ok(cleared.paint.slice(0, 3).every(v => v < 0.5), `Clear takes the paint off (${cleared.paint.map(v => v.toFixed(2))})`);
  noErrors();
});

test('path: raise, lower, flatten, smooth and paint along a line; Alt + click picks the height', async () => {
  await openEditor();
  const c = (await records()).find(r => r.name === 'piece_chest_wood');
  const a = [c.x + 10, c.z - 30], b = [c.x + 30, c.z - 30], mid = [c.x + 20, c.z - 30];
  await lookAt(page(), ...mid, 0, 50, 30);
  await page().keyboard.press('p');
  const at = () => page().evaluate(([x, z]) => { const ed = window.__ed, g = Math.round(z - ed.originZ) * ed.W + Math.round(x - ed.originX); return { h: ed.height(g), paint: Array.from(ed.paint.slice(g * 4, g * 4 + 4)), pmod: ed.pmod[g] }; }, mid);
  const run = async action => {
    await page().select('#pAction', action);
    for (const p of [a, b]) { const [x, y] = await screenOf(page(), ...p); await page().mouse.click(x, y); await sleep(150); }
    await page().keyboard.press('Enter'); await sleep(600);
    await page().keyboard.press('Escape');
  };
  const h0 = (await at()).h;
  await page().$eval('#pAmount', e => { e.value = 2; e.dispatchEvent(new Event('input')); });
  await run('raise');
  assert.ok(Math.abs((await at()).h - (h0 + 2)) < 0.2, 'Raise by 2 m');
  await run('lower');
  assert.ok(Math.abs((await at()).h - h0) < 0.2, 'Lower by 2 m');
  await page().$eval('#pHeight', (e, v) => { e.value = v; e.dispatchEvent(new Event('input')); }, (h0 + 3).toFixed(1));
  await run('flatten');
  assert.ok(Math.abs((await at()).h - (h0 + 3)) < 0.2, 'Flatten to the Height');
  const flat = (await at()).h;
  await run('smooth');
  assert.ok(Math.abs((await at()).h - flat) < 1.5, 'Smooth keeps it close');
  await run('paint-paved');
  const p = await at();
  assert.ok(p.pmod && p.paint[2] > 0.5, `Paint paved along the line (${p.paint.map(v => v.toFixed(2))})`);
  // Alt + click: the ground height there goes into Height.
  await page().select('#pAction', 'flatten');
  { const [x, y] = await screenOf(page(), ...mid); await page().keyboard.down('Alt'); await page().mouse.click(x, y); await page().keyboard.up('Alt'); await sleep(200); }
  assert.ok(Math.abs(await page().$eval('#pHeight', e => +e.value) - (await at()).h) < 0.2, 'Alt + click picked the height');
  noErrors();
});

test('mask: biome, height, slope and paint filters decide where brushes work', async () => {
  await openEditor();
  const c = (await records()).find(r => r.name === 'piece_chest_wood');
  const spot = [c.x + 25, c.z - 15];
  await lookAt(page(), ...spot, 0, 40, 30);
  await page().keyboard.press('1');
  const g = await page().evaluate(([x, z]) => { const ed = window.__ed; return Math.round(z - ed.originZ) * ed.W + Math.round(x - ed.originX); }, spot);
  const info = await page().evaluate(g => ({ biome: window.__ed.vbiome[g], h: window.__ed.height(g) }), g);
  const mask = () => page().evaluate(g => window.__ed.mask(g), g);
  const set = (id, v) => page().$eval('#' + id, (e, v) => { e.value = v; e.dispatchEvent(new Event('input')); e.dispatchEvent(new Event('change')); }, v);
  await page().click('#mOn');
  assert.equal(await mask(), 1, 'on, with no filter: everywhere');
  // Biomes: another biome only leaves this spot out; its own biome lets it in.
  const other = info.biome === 1 ? 8 : 1;
  await page().click(`#mBiomes [data-b="${other}"]`);
  assert.equal(await mask(), 0, 'another biome: left out');
  await page().click(`#mBiomes [data-b="${info.biome}"]`);
  assert.equal(await mask(), 1, 'its own biome too: in');
  await page().click(`#mBiomes [data-b="${other}"]`); await page().click(`#mBiomes [data-b="${info.biome}"]`);
  // Height range.
  await set('mHmax', (info.h - 1).toFixed(1));
  assert.equal(await mask(), 0, 'above the highest: left out');
  await set('mHmax', ''); await set('mHmin', (info.h - 1).toFixed(1));
  assert.equal(await mask(), 1, 'above the lowest: in');
  await set('mHmin', '');
  // Slope: a maximum of 0° leaves out anything not dead flat; 90° lets everything in.
  await set('mSmax', 0);
  const flatOnly = await mask();
  await set('mSmax', 90);
  assert.equal(await mask(), 1, 'any slope up to 90°');
  assert.ok(flatOnly < 1, 'only flat ground: this spot (a slope) is out');
  await set('mSmax', '');
  // Paint: only painted ground leaves out unpainted ground.
  await page().select('#mPaint', 'painted');
  assert.equal(await mask(), 0, 'unpainted ground is out');
  await page().select('#mPaint', 'unpainted');
  assert.equal(await mask(), 1, 'only unpainted: in');
  await page().select('#mPaint', 'any');
  // A stroke where the mask leaves everything out changes nothing, and says so.
  await set('mHmax', (info.h - 5).toFixed(1));
  const before = await page().evaluate(g => window.__ed.height(g), g);
  const [x, y] = await screenOf(page(), ...spot);
  await page().mouse.move(x, y); await page().mouse.down(); await page().mouse.move(x + 3, y + 3, { steps: 10 }); await page().mouse.up(); await sleep(400);
  assert.equal(await page().evaluate(g => window.__ed.height(g), g), before, 'the masked stroke changed nothing');
  await set('mHmax', '');
  await page().click('#mOn');
  noErrors();
});

});
