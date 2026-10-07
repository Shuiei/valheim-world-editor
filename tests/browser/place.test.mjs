// End-to-end, the Place tool: lines, end to end, zones, grids, mixes: the real app on its own copy of the test world, as a user drives it.
import { describe, test } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { waitPhase, screenOf, lookAt, frames, sleep, stableHash } from './harness.mjs';
import { t, setup, page, noErrors, pending, openEditor, areaAction, applyArea, records } from './common.mjs';

setup({ open: true });

describe('The Place tool: lines, end to end, zones, grids, mixes', () => {

test('plant end to end: a rectangle of walls snaps to whole pieces and closes, a ring too', async () => {
  await openEditor(undefined, () => localStorage.setItem('plantChosen', '["woodwall"]'));
  await page().keyboard.press('t'); await sleep(300);
  await page().click('#plModes [data-m="line"]');
  await page().evaluate(() => { const e = document.getElementById('plSnap'); e.checked = true; e.dispatchEvent(new Event('input')); });
  await page().click('#plLineShape [data-ls="rect"]');
  const c = (await records()).find(r => r.name === 'piece_chest_wood');
  const A = [c.x + 10, c.z + 10], B = [c.x + 19.3, c.z + 15.6];
  await lookAt(page(), c.x + 15, c.z + 13, 0, 45, 30);
  const [ax, ay] = await screenOf(page(), ...A), [bx, by] = await screenOf(page(), ...B);
  await page().mouse.move(ax, ay); await page().mouse.down(); await page().mouse.move(bx, by, { steps: 8 }); await page().mouse.up(); await sleep(500);
  assert.match(await page().$eval('#plPreview', e => e.textContent), /^16 object.*\(10\.0 × 6\.0 m\)/, 'snapped to 10 × 6 m: 16 walls of 2 m');
  const before = (await records()).filter(r => r.added).map(r => r.id);
  await page().keyboard.press('Enter'); await sleep(1200);
  const walls = await page().evaluate(before => [...window.__ed.objects.records.values()].filter(r => r.added && !r.deleted && !before.includes(r.id)).map(r => ({ id: r.id, x: r.x, z: r.z, ry: r.ry })), before);
  assert.equal(walls.length, 16);
  // Each wall's ends (±1 m along its own x) meet another wall's end, or a corner.
  const ends = walls.flatMap(w => { const t = w.ry * Math.PI / 180; return [-1, 1].map(s => [w.x + s * Math.cos(t), w.z - s * Math.sin(t)]); });
  const lonely = ends.filter(([x, z]) => ends.filter(([u, v]) => Math.hypot(u - x, v - z) < 0.02).length < 2);
  assert.equal(lonely.length, 0, 'every end meets another one: the fence is closed');
  // A ring: one point per piece, closed.
  await page().click('#plLineShape [data-ls="circle"]');
  const O = [c.x - 15, c.z + 20];
  await lookAt(page(), ...O, 0, 45, 30);
  const [ox, oy] = await screenOf(page(), ...O), [rx, ry] = await screenOf(page(), O[0] + 6, O[1]);
  await page().mouse.move(ox, oy); await page().mouse.down(); await page().mouse.move(rx, ry, { steps: 8 }); await page().mouse.up(); await sleep(500);
  const ring = await page().$eval('#plPreview', e => e.textContent);
  assert.match(ring, /^\d+ object/);
  assert.doesNotMatch(ring, /left at the end/, 'no gap: whole pieces close the ring');
  await page().keyboard.press('Escape');
  await page().evaluate(ids => window.__ed.setDeleted(ids, true), walls.map(w => w.id));
  await page().evaluate(() => { const e = document.getElementById('plSnap'); e.checked = false; e.dispatchEvent(new Event('input')); });
  await page().click('#plLineShape [data-ls="points"]');
  noErrors();
});

test('plant end to end on a slope: level pieces, each on its own ground, touching the last', async () => {
  await openEditor(undefined, () => localStorage.setItem('plantChosen', '["woodwall"]'));
  await page().keyboard.press('t'); await sleep(300);
  await page().click('#plModes [data-m="line"]');
  await page().evaluate(() => { const e = document.getElementById('plSnap'); e.checked = true; e.dispatchEvent(new Event('input')); });
  const c = (await records()).find(r => r.name === 'piece_chest_wood');
  // Down the hill west of the chests (the test world's ground falls towards the east).
  const A = [c.x - 40, c.z + 6], B = [c.x - 4, c.z + 6];
  await lookAt(page(), c.x - 22, c.z + 6, 0, 45, 30);
  for (const p of [A, B]) { const [x, y] = await screenOf(page(), ...p); await page().mouse.click(x, y); await sleep(150); }
  await sleep(500);
  const walls = await page().evaluate(() => {
    const ed = window.__ed, ground = (x, z) => {
      // Between the four nearest grid points, as placing does.
      const fx = x - ed.originX, fz = z - ed.originZ, x0 = Math.floor(fx), z0 = Math.floor(fz), tx = fx - x0, tz = fz - z0, h = (a, b) => ed.height(b * ed.W + a);
      return (h(x0, z0) * (1 - tx) + h(x0 + 1, z0) * tx) * (1 - tz) + (h(x0, z0 + 1) * (1 - tx) + h(x0 + 1, z0 + 1) * tx) * tz;
    };
    // A wall's ends (snap points at x = ±1, bottom at y = -1), on the ground at its start, middle and end.
    return window.__plantPreview().map(o => {
      const t = o.ry * Math.PI / 180, a = [o.x - Math.cos(t), o.z + Math.sin(t)], b = [o.x + Math.cos(t), o.z - Math.sin(t)];
      return { a, b, bottom: o.y - 1, lowest: Math.min(ground(...a), ground(...b), ground(o.x, o.z)), level: o.rx === 0 && o.rz === 0 };
    });
  });
  assert.ok(walls.length >= 10, `walls along the line (${walls.length})`);
  assert.ok(walls.every(w => w.level), 'every wall stays level');
  const worst = Math.max(...walls.map(w => Math.abs(w.bottom - w.lowest)));
  assert.ok(worst < 0.02, `each stands on the ground where it is (off by ${worst.toFixed(3)} m at most)`);
  for (let i = 1; i < walls.length; i++) assert.ok(Math.hypot(walls[i].a[0] - walls[i - 1].b[0], walls[i].a[1] - walls[i - 1].b[1]) < 0.02, `wall ${i} touches the last one`);
  const heights = walls.map(w => w.bottom);
  assert.ok(Math.max(...heights) - Math.min(...heights) > 0.5, 'they step down the slope');
  await page().keyboard.press('Escape');
  await page().evaluate(() => { const e = document.getElementById('plSnap'); e.checked = false; e.dispatchEvent(new Event('input')); });
  noErrors();
});

test('plant line: walls switch End to end on, and every object follows a rectangle', async () => {
  await openEditor(undefined, () => localStorage.setItem('plantChosen', '["woodwall"]'));
  await page().keyboard.press('t'); await sleep(300);
  await page().click('#plModes [data-m="line"]');
  await page().waitForFunction(() => document.getElementById('plSnap').checked);
  // Without End to end too, a rectangle's objects lie along its sides.
  await page().evaluate(() => { const e = document.getElementById('plSnap'); e.checked = false; e.dispatchEvent(new Event('input')); });
  await page().click('#plLineShape [data-ls="rect"]');
  const c = (await records()).find(r => r.name === 'piece_chest_wood');
  const A = [c.x + 10, c.z + 10], B = [c.x + 22, c.z + 18];
  await lookAt(page(), c.x + 16, c.z + 14, 0, 45, 30);
  const [ax, ay] = await screenOf(page(), ...A), [bx, by] = await screenOf(page(), ...B);
  await page().mouse.move(ax, ay); await page().mouse.down(); await page().mouse.move(bx, by, { steps: 8 }); await page().mouse.up(); await sleep(500);
  const yaws = await page().evaluate(() => window.__plantPreview().map(o => ((o.ry % 180) + 180) % 180));
  assert.ok(yaws.length >= 8, `objects along the rectangle (${yaws.length})`);
  // A wall lies along x: on east-west sides it is not turned (0), on north-south ones a quarter turn (90).
  const off = yaws.map(y => Math.min(Math.abs(y), Math.abs(y - 90), Math.abs(y - 180)));
  assert.ok(Math.max(...off) < 4, `every wall follows a side (off by ${Math.max(...off).toFixed(1)}° at most, rotation and wiggle aside)`);
  await page().keyboard.press('Escape');
  await page().click('#plLineShape [data-ls="points"]');
  noErrors();
});

test('lines can be fine-tuned: drag a point, drag the line to add one, Ctrl + click removes one', async () => {
  await openEditor();
  const c = (await records()).find(r => r.name === 'piece_chest_wood');
  const A = [c.x - 30, c.z + 20], B = [c.x - 10, c.z + 20];
  await lookAt(page(), c.x - 20, c.z + 22, 0, 40, 28);
  for (const [tool, pts] of [['p', '__pathPoints'], ['t', '__plantLine']]) {
    await page().keyboard.press(tool); await sleep(300);
    if (tool === 't') await page().click('#plModes [data-m="line"]');
    for (const q of [A, B]) { const [x, y] = await screenOf(page(), ...q, 0.6); await page().mouse.click(x, y); await sleep(150); }
    const grid = () => page().evaluate(n => window[n](), pts);
    const start = await grid();
    assert.equal(start.length, 2, `${tool}: two points`);
    // Drag the second point 6 m north.
    { const [x, y] = await screenOf(page(), ...B, 0.6), [x2, y2] = await screenOf(page(), B[0], B[1] + 6, 0.6); await page().mouse.move(x, y); await page().mouse.down(); await page().mouse.move(x2, y2, { steps: 6 }); await page().mouse.up(); }
    let now = await grid();
    assert.equal(now.length, 2, `${tool}: still two points`);
    assert.ok(Math.abs(now[1].gz - start[1].gz - 6) < 0.6, `${tool}: the point moved north (${(now[1].gz - start[1].gz).toFixed(2)} m)`);
    // Drag the middle of the line: a third point between the two.
    const mid = await page().evaluate(([a, b]) => { const ed = window.__ed; return [ed.originX + (a.gx + b.gx) / 2, ed.originZ + (a.gz + b.gz) / 2]; }, now);
    { const [x, y] = await screenOf(page(), ...mid, 0.6), [x2, y2] = await screenOf(page(), mid[0], mid[1] - 4, 0.6); await page().mouse.move(x, y); await page().mouse.down(); await page().mouse.move(x2, y2, { steps: 6 }); await page().mouse.up(); }
    now = await grid();
    assert.equal(now.length, 3, `${tool}: a point was added on the line`);
    // Ctrl + click the new point: removed.
    { const p = await page().evaluate(q => [window.__ed.originX + q.gx, window.__ed.originZ + q.gz], now[1]); const [x, y] = await screenOf(page(), ...p, 0.6);
      await page().keyboard.down('Control'); await page().mouse.click(x, y); await page().keyboard.up('Control'); }
    assert.equal((await grid()).length, 2, `${tool}: Ctrl + click removed it`);
    await page().keyboard.press('Escape');
  }
  noErrors();
});

test('Place tool: named Place, the sapling option only shows for saplings and crops', async () => {
  await openEditor(undefined, () => localStorage.setItem('plantChosen', '["woodwall"]'));
  assert.match(await page().$eval('[data-tool="plant"]', e => e.textContent.trim()), /^Place/);
  await page().keyboard.press('t'); await sleep(300);
  await page().waitForFunction(() => document.querySelector('#plMix [data-w="woodwall"]'));
  await page().evaluate(() => window.__ed.plantDrawer(true));
  assert.ok(await page().$eval('#plGrowRow', e => e.hidden), 'hidden for walls');
  await page().$eval('#plSearch', e => { e.value = 'sapling_turnip'; e.dispatchEvent(new Event('input')); });
  await page().click('#plList input[value="sapling_turnip"]');
  await page().waitForFunction(() => !document.getElementById('plGrowRow').hidden);
  await page().click('#plList input[value="sapling_turnip"]');
  assert.ok(await page().$eval('#plGrowRow', e => e.hidden), 'hidden again once the sapling is unticked');
  // Grown crops and trees from saplings too; bushes (nothing grows them) not.
  for (const [kind, want] of [['Pickable_Carrot', true], ['Beech1', true], ['BlueberryBush', false]]) {
    await page().evaluate(k => { localStorage.setItem('plantChosen', JSON.stringify([k])); }, kind);
    await page().$eval('#plSearch', (e, k) => { e.value = k; e.dispatchEvent(new Event('input')); }, kind);
    await page().click('#plNone');
    await page().click(`#plList input[value="${kind}"]`);
    assert.equal(!(await page().$eval('#plGrowRow', e => e.hidden)), want, kind);
  }
  noErrors();
});

test('Place zone: Ctrl + click removes a point, drag moves one, Remove last point', async () => {
  await openEditor(undefined, () => localStorage.setItem('plantChosen', '["Beech1"]'));
  const c = (await records()).find(r => r.name === 'piece_chest_wood');
  await lookAt(page(), c.x - 20, c.z + 20, 0, 45, 30);
  await page().keyboard.press('t'); await sleep(300);
  await page().click('#plModes [data-m="zone"]');
  const pts = () => page().evaluate(() => window.__plantLine());
  for (const [dx, dz] of [[-28, 14], [-12, 14], [-12, 28], [-28, 28]]) { const [x, y] = await screenOf(page(), c.x + dx, c.z + dz); await page().mouse.click(x, y); await sleep(150); }
  assert.equal((await pts()).length, 4);
  assert.ok(!(await page().$eval('#plUndoRow', e => e.hidden)), 'Remove last point shown');
  // Ctrl + click on the second point removes it.
  { const [x, y] = await screenOf(page(), c.x - 12, c.z + 14); await page().keyboard.down('Control'); await page().mouse.click(x, y); await page().keyboard.up('Control'); await sleep(150); }
  const left = await pts();
  assert.equal(left.length, 3, 'one point removed');
  // Drag the first point 4 m east.
  { const [x, y] = await screenOf(page(), c.x - 28, c.z + 14), [x2, y2] = await screenOf(page(), c.x - 24, c.z + 14);
    await page().mouse.move(x, y); await page().mouse.down(); await page().mouse.move(x2, y2, { steps: 6 }); await page().mouse.up(); await sleep(150); }
  const moved = await pts();
  assert.equal(moved.length, 3, 'a drag adds no point');
  assert.ok(Math.abs(moved[0].gx - left[0].gx - 4) < 1, `the point moved (${(moved[0].gx - left[0].gx).toFixed(2)} m)`);
  await page().click('#plUndoPt');
  assert.equal((await pts()).length, 2, 'the last point removed');
  await page().keyboard.press('Escape');
  noErrors();
});

test('Place grid: a warning and fit when the kinds are wider than the spacing', async () => {
  await openEditor(undefined, () => localStorage.setItem('plantChosen', '["BlueberryBush"]'));
  await page().keyboard.press('t'); await sleep(300);
  await page().click('#plModes [data-m="grid"]');
  await page().$eval('#plCell', e => { e.value = 1; e.dispatchEvent(new Event('input')); });
  // The test world has no models: give the bush the 3 m of the game's.
  await page().evaluate(() => window.__plantWidth('BlueberryBush', 2.9));
  await page().waitForFunction(() => !document.getElementById('plFitHint').hidden);
  assert.match(await page().$eval('#plFitHint', e => e.textContent), /BlueberryBush is about 3\.5 m wide/);
  await page().click('#plFit');
  assert.equal(await page().$eval('#plCell', e => +e.value), 3.5, 'spacing set to the width at the largest size (120 %)');
  assert.ok(await page().$eval('#plFitHint', e => e.hidden), 'no warning once they fit');
  noErrors();
});

test('Place mix: weights decide how often each kind is used; presets load and save', async () => {
  await openEditor(undefined, () => { localStorage.setItem('plantChosen', '["Beech1","Bush01"]'); localStorage.setItem('plantWeights', '{}'); localStorage.removeItem('plantPresets'); });
  const c = (await records()).find(r => r.name === 'piece_chest_wood');
  await lookAt(page(), c.x + 20, c.z + 20, 0, 60, 0.01);
  await page().keyboard.press('t'); await sleep(300);
  await page().click('#plModes [data-m="grid"]');
  await page().$eval('#plCell', e => { e.value = 2; e.dispatchEvent(new Event('input')); });
  { const [a, b] = await screenOf(page(), c.x + 5, c.z + 5); const [x2, y2] = await screenOf(page(), c.x + 35, c.z + 35); await page().mouse.move(a, b); await page().mouse.down(); await page().mouse.move(x2, y2, { steps: 6 }); await page().mouse.up(); await sleep(400); }
  const share = () => page().evaluate(() => { const p = window.__plantPreview(); return p.filter(o => o.name === 'Beech1').length / p.length; });
  assert.equal(await page().$$eval('#plMix [data-w]', r => r.length), 2, 'a weight per ticked kind');
  const even = await share();
  assert.ok(even > 0.35 && even < 0.65, `even weights: about half beeches (${even.toFixed(2)})`);
  // Beech 9, Bush 1: about nine in ten.
  await page().$eval('#plMix [data-w="Beech1"] input', e => { e.value = 9; e.dispatchEvent(new Event('input')); });
  assert.equal(await page().$eval('#plMix [data-w="Beech1"] .pct', e => e.textContent), '90%');
  const heavy = await share();
  assert.ok(heavy > 0.8, `weighted: mostly beeches (${heavy.toFixed(2)})`);
  // A built-in preset ticks its kinds (those the world can place) with their weights.
  await page().select('#plPreset', 'Meadows woods');
  const chosen = await page().evaluate(() => JSON.parse(localStorage.getItem('plantChosen')));
  assert.ok(chosen.includes('Beech1') && chosen.includes('Bush01'), `preset kinds ticked (${chosen})`);
  assert.equal(await page().$eval('#plDensity', e => +e.value), 3);
  // Save your own, load it back after changing things, delete it.
  await page().evaluate(() => { window.prompt = () => 'My woods'; });
  await page().$eval('#plPresetSave', e => e.click());
  assert.equal(await page().$eval('#plPreset', e => e.value), 'own:My woods');
  await page().$eval('#plNone', e => e.click());
  await page().select('#plPreset', 'own:My woods');
  assert.deepEqual((await page().evaluate(() => JSON.parse(localStorage.getItem('plantChosen')))).sort(), chosen.slice().sort());
  await page().evaluate(() => { window.confirm = () => true; });
  await page().$eval('#plPresetDel', e => e.click());
  assert.equal(await page().$$eval('#plPreset option[value^="own:"]', o => o.length), 0);
  await page().keyboard.press('Escape');
  noErrors();
});

test('Place clumping: groves and clearings instead of an even spread', async () => {
  await openEditor(undefined, () => localStorage.setItem('plantChosen', '["Beech1"]'));
  const c = (await records()).find(r => r.name === 'piece_chest_wood');
  await lookAt(page(), c.x + 30, c.z + 30, 0, 90, 0.01);
  await page().keyboard.press('t'); await sleep(300);
  await page().click('#plModes [data-m="zone"]');
  await page().$eval('#plDensity', e => { e.value = 4; e.dispatchEvent(new Event('input')); });
  await page().$eval('#plSpacing', e => { e.value = 2; e.dispatchEvent(new Event('input')); });
  assert.ok(await page().$eval('#plPatchRow', e => e.hidden), 'Patch size hidden while Clumping is 0');
  for (const [dx, dz] of [[5, 5], [55, 5], [55, 55], [5, 55]]) { const [x, y] = await screenOf(page(), c.x + dx, c.z + dz); await page().mouse.click(x, y); await sleep(150); }
  const even = (await page().evaluate(() => window.__plantPreview())).length;
  await page().$eval('#plClump', e => { e.value = 80; e.dispatchEvent(new Event('input')); });
  await page().$eval('#plPatch', e => { e.value = 20; e.dispatchEvent(new Event('input')); });
  assert.ok(!(await page().$eval('#plPatchRow', e => e.hidden)), 'Patch size shown');
  const pts = await page().evaluate(() => window.__plantPreview().map(o => [o.gx, o.gz]));
  assert.ok(pts.length > 0 && pts.length < even * 0.6, `fewer objects, in patches (${pts.length} of ${even})`);
  // Patches: the 10 m squares are far from evenly filled (some full, some empty).
  const cells = new Map();
  for (const [x, z] of pts) { const k = `${Math.floor(x / 10)},${Math.floor(z / 10)}`; cells.set(k, (cells.get(k) ?? 0) + 1); }
  const empty = 25 - cells.size;
  assert.ok(empty >= 5, `clearings: ${empty} of the 25 squares are empty`);
  await page().$eval('#plClump', e => { e.value = 0; e.dispatchEvent(new Event('input')); });
  await page().keyboard.press('Escape');
  noErrors();
});

});
