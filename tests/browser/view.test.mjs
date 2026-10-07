// End-to-end, views, overlays and the script console: the real app on its own copy of the test world, as a user drives it.
import { describe, test } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { waitPhase, screenOf, lookAt, frames, sleep, stableHash } from './harness.mjs';
import { t, setup, page, noErrors, pending, openEditor, areaAction, applyArea, records } from './common.mjs';

setup({ open: true });

describe('Views, overlays and the script console', () => {

test('eye views: walk at 1.8 m over the ground, fly up and down, back to the usual view', async () => {
  await openEditor();
  const c = (await records()).find(r => r.name === 'piece_chest_wood');
  await lookAt(page(), c.x + 20, c.z + 20, 0, 40, 30);
  const eye = () => page().evaluate(() => { const ed = window.__ed, p = ed.camera.position; return { y: p.y, ground: ed.sampleHeight(p.x + ed.cx, -p.z + ed.cz), mode: ed.eyeMode() }; });
  await page().keyboard.press('f'); await sleep(200);
  let e = await eye();
  assert.equal(e.mode, 'walk');
  assert.ok(Math.abs(e.y - Math.max(e.ground, 28.8) - 1.8) < 0.05, `eyes 1.8 m up (${(e.y - e.ground).toFixed(2)})`);
  // Walk forward a second: still 1.8 m over the ground where it is now.
  await page().keyboard.down('w'); await sleep(1000); await page().keyboard.up('w'); await sleep(100);
  e = await eye();
  assert.ok(Math.abs(e.y - Math.max(e.ground, 28.8) - 1.8) < 0.15, `still at eye height after walking (${(e.y - e.ground).toFixed(2)})`);
  // Fly: Space goes up.
  await page().keyboard.press('f'); await sleep(100);
  const y0 = (await eye()).y;
  await page().keyboard.down('Space'); await sleep(600); await page().keyboard.up('Space'); await sleep(100);
  e = await eye();
  assert.equal(e.mode, 'fly');
  assert.ok(e.y > y0 + 3, `flew up (${(e.y - y0).toFixed(1)} m)`);
  await page().keyboard.press('f'); await sleep(100);
  assert.equal((await eye()).mode, null, 'back to the usual view');
  noErrors();
});

test('overlays: a workbench\'s build range and a ward\'s area are drawn as rings', async () => {
  await openEditor();
  const c = (await records()).find(r => r.name === 'piece_chest_wood');
  await lookAt(page(), c.x + 10, c.z + 10, 0, 60, 30);
  // One workbench and one ward, placed like the Place tool does (one undo step).
  await page().evaluate(async (x, y, z) => {
    const ed = window.__ed, added = await ed.objects.add([{ name: 'piece_workbench', x: x + 5, y, z: z + 5, fresh: true }, { name: 'guard_stone', x: x + 15, y, z: z + 5, fresh: true }]);
    ed.pushHistory({ added, label: 'test' });
  }, c.x, c.y, c.z);
  await sleep(1500);
  const rings = () => page().evaluate(() => Object.fromEntries(Object.entries(window.__ed.overlayGroups).map(([k, g]) => [k, g.children.length])));
  await page().waitForFunction(() => window.__ed.overlayGroups.stations.children.length > 0, { timeout: 5000 });
  const r = await rings();
  assert.ok(r.stations >= 1 && r.wards >= 1, `rings (${JSON.stringify(r)})`);
  assert.equal(await page().evaluate(() => window.__ed.overlayGroups.wards.visible), false, 'off until switched on');
  await page().evaluate(() => { document.getElementById('viewPanel').hidden = false; });
  await page().$eval('[data-show="wards"]', e => { e.checked = true; e.dispatchEvent(new Event('change')); });
  assert.equal(await page().evaluate(() => window.__ed.overlayGroups.wards.visible), true);
  // The workbench ring is 20 m round its piece.
  const radius = await page().evaluate(() => { const g = window.__ed.overlayGroups.stations.children[0], p = g.geometry.attributes.position; let mx = Infinity, Mx = -Infinity; for (let i = 0; i < p.count; i++) { mx = Math.min(mx, p.getX(i)); Mx = Math.max(Mx, p.getX(i)); } return (Mx - mx) / 2; });
  assert.ok(Math.abs(radius - 20) < 0.5, `20 m build range (${radius.toFixed(2)})`);
  await page().$eval('[data-show="wards"]', e => { e.checked = false; e.dispatchEvent(new Event('change')); });
  await page().click('#undo'); await sleep(800);
  noErrors();
});

test('script console: a run changes ground and objects, and is one undo step', async () => {
  await openEditor();
  const start = await pending();
  const n0 = (await records()).length;
  await page().click('#scriptToggle');
  assert.equal(await page().$eval('#scriptPanel', e => e.hidden), false);
  const code = `const c = vwe.objects({ name: 'piece_chest_wood' })[0];
const h = vwe.ground(c.x + 20, c.z + 20);
for (let dx = -3; dx <= 3; dx++) for (let dz = -3; dz <= 3; dz++) vwe.setGround(c.x + 20 + dx, c.z + 20 + dz, h + 2);
await vwe.add([{ name: 'Beech1', x: c.x + 30, z: c.z + 30 }]);
vwe.log('raised', h);`;
  await page().$eval('#scCode', (e, v) => { e.value = v; }, code);
  await page().click('#scRun'); await sleep(1500);
  const out = await page().$eval('#scOut', e => e.textContent);
  assert.match(out, /^raised \d/, `log shown (${out})`);
  const c = (await records()).find(r => r.name === 'piece_chest_wood');
  const h = await page().evaluate(([x, z]) => { const ed = window.__ed; return ed.sampleHeight(x - ed.originX, z - ed.originZ); }, [c.x + 20, c.z + 20]);
  assert.ok(Math.abs(h - (parseFloat(out.split(' ')[1]) + 2)) < 0.05, `ground set (${h})`);
  assert.equal((await records()).length, n0 + 1, 'one object added');
  // An error is shown, and what ran before it is kept as a step too.
  await page().$eval('#scCode', e => { e.value = 'vwe.log("before"); nope();'; });
  await page().click('#scRun'); await sleep(500);
  assert.match(await page().$eval('#scOut', e => e.textContent), /before\nReferenceError: nope is not defined/);
  // One undo takes the whole first run back.
  await page().click('#undo'); await sleep(1500);
  assert.equal((await records()).length, n0, 'object gone');
  assert.equal(await pending(), start);
  // The examples work inside the selection: without one they stop and say so.
  await page().select('#scPick', 'ex:Terraces');
  await page().click('#scRun'); await sleep(500);
  assert.match(await page().$eval('#scOut', e => e.textContent), /Select an area first/);
  assert.equal(await pending(), start, 'nothing changed');
  await page().click('#scClose');
  noErrors();
});

test('the 3D view is drawn only while something happens, and Help says what draws it', async () => {
  await openEditor();
  const drawn = () => page().evaluate(() => window.__ed.renderer.info.render.frame);
  await sleep(1500);   // past the second of full speed after the last input
  const a = await drawn(); await sleep(2000); const idle = (await drawn()) - a;
  assert.ok(idle <= 12, `idle: a few frames a second (${idle} in 2 s)`);
  // A camera move or the mouse: full speed again.
  const b = await drawn();
  for (let i = 0; i < 20; i++) { await page().mouse.move(600 + i * 5, 400); await sleep(50); }
  const busy = (await drawn()) - b;
  assert.ok(busy > idle, `drawn more while the mouse moves (${busy} vs ${idle} idle)`);
  assert.match(await page().$eval('#gpuInfo', e => e.textContent), /^3D drawn by: .+\((the graphics card|the processor)/);
  // Moving the view measures the frame rate, shown in Help.
  for (let i = 0; i < 40; i++) { await page().mouse.move(600 + (i % 10) * 8, 400 + i); await sleep(30); }
  await page().keyboard.press('?'); await sleep(200);
  assert.match(await page().$eval('#gpuInfo', e => e.textContent), /\d+ frames\/s while moving \([\d.]+ ms of work each\)/);
  // Selected (to copy it), the line stays as it is while the view keeps drawing.
  await page().evaluate(() => { const r = document.createRange(); r.selectNodeContents(document.getElementById('gpuInfo')); getSelection().removeAllRanges(); getSelection().addRange(r); });
  const text = await page().$eval('#gpuInfo', e => e.textContent);
  for (let i = 0; i < 40; i++) { await page().evaluate(() => window.__ed.wake()); await sleep(30); }
  assert.equal(await page().evaluate(() => getSelection().toString()), text, 'still selected, unchanged');
  // perf-browser.log (opened with --browser): a header (the browser, the view) and a sample every 0.2 s.
  await page().keyboard.press('?');
  { const [x, y] = [700, 450]; await page().mouse.move(x, y); await page().mouse.down({ button: 'right' }); await page().mouse.move(x + 150, y, { steps: 40 }); await page().mouse.up({ button: 'right' }); }
  for (let i = 0; i < 20; i++) { await page().mouse.move(500 + (i % 10) * 10, 380 + i); await sleep(30); }
  await sleep(2600); await page().mouse.move(650, 420); await sleep(400);
  const { perfLog } = await t.api('/api/app');
  assert.match(perfLog, /perf-browser\.log$/);
  let log = '';
  for (let i = 0; i < 20 && !/still/.test(log); i++) { await sleep(250); try { log = fs.readFileSync(perfLog, 'utf8'); } catch { } }
  assert.match(log, /^# \d{4}-\d\d-\d\d \d\d:\d\d:\d\d  v[\d.]+  browser \(--browser\)  view \d+×\d+ px/m, `header line (${log.slice(0, 300)})`);
  assert.match(log, /^\d\d:\d\d:\d\d\.\d{3}  moving  +\d+ fps +\d+ frames +[\d.]+ ms work  longest gap \d+ ms$/m, 'a sample while the camera moved');
  assert.match(log, /^\d\d:\d\d:\d\d\.\d{3}  still   +\d+ fps/m, 'a sample with the camera still');
  // 3D resolution: Fast draws three quarters of a pixel per screen point (remembered).
  await page().evaluate(() => { document.getElementById('viewPanel').hidden = false; const e = document.getElementById('res3d'); e.value = 'fast'; e.dispatchEvent(new Event('change')); });
  const w = await page().evaluate(() => [window.__ed.renderer.domElement.width, innerWidth]);
  assert.ok(Math.abs(w[0] - w[1] * 0.75 * Math.min(1, 1)) <= 1, `three quarters wide (${w})`);
  assert.equal(await page().evaluate(() => localStorage.getItem('view3dRes')), 'fast');
  await page().evaluate(() => { const e = document.getElementById('res3d'); e.value = 'sharp'; e.dispatchEvent(new Event('change')); document.getElementById('viewPanel').hidden = true; });
  noErrors();
});

test('measure: two clicks give the distance, heights and slope; Esc and Clear start over', async () => {
  await openEditor();
  const c = (await records()).find(r => r.name === 'piece_chest_wood');
  const A = [c.x + 10, c.z - 10], B = [c.x + 30, c.z - 10];
  await lookAt(page(), c.x + 20, c.z - 10, 0, 45, 30);
  await page().keyboard.press('m'); await sleep(200);
  assert.match(await page().$eval('#msHint', e => e.textContent), /first point/);
  for (const p of [A, B]) { const [x, y] = await screenOf(page(), ...p); await page().mouse.click(x, y); await sleep(200); }
  const out = await page().$eval('#msOut', e => e.textContent);
  const dist = +out.match(/Distance\s*([\d.]+) m/)[1];
  assert.ok(Math.abs(dist - 20) < 1, `about 20 m (${dist})`);
  const hA = await page().evaluate(([x, z]) => { const ed = window.__ed; return ed.sampleHeight(x - ed.originX, z - ed.originZ); }, A);
  const shownA = +out.match(/Height A → B\s*(-?[\d.]+) →/)[1];
  assert.ok(Math.abs(shownA - hA) < 0.5, `height at A (${shownA} vs ${hA.toFixed(2)})`);
  assert.match(out, /Slope\s*-?\d+ %/);
  await page().keyboard.press('Escape'); await sleep(100);
  assert.equal(await page().$eval('#msOut', e => e.textContent.trim()), '');
  for (const p of [A, B]) { const [x, y] = await screenOf(page(), ...p); await page().mouse.click(x, y); await sleep(200); }
  await page().click('#msClear');
  assert.equal(await page().$eval('#msOut', e => e.textContent.trim()), '');
  noErrors();
});

test('script console: keep and delete your own scripts, Ctrl+Enter runs, examples work inside the selection', async () => {
  await openEditor();
  await page().click('#scriptToggle');
  await page().evaluate(() => { window.prompt = () => 'My count'; });
  await page().$eval('#scCode', e => { e.value = 'vwe.log("hello", 1 + 1);'; });
  await page().click('#scSave');
  assert.equal(await page().$eval('#scPick', e => e.value), 'my:My count', 'kept under its name');
  assert.equal(await page().$eval('#scDel', e => e.disabled), false);
  await page().focus('#scCode');
  await page().keyboard.down('Control'); await page().keyboard.press('Enter'); await page().keyboard.up('Control'); await sleep(400);
  assert.match(await page().$eval('#scOut', e => e.textContent), /hello 2/, 'Ctrl+Enter ran it');
  await page().click('#scDel');
  assert.ok(!(await page().$$eval('#scPick option', o => o.map(x => x.value))).includes('my:My count'), 'deleted');
  // An example inside an Area selection: the kinds counted there.
  await page().click('#scriptToggle');
  const tree = (await records()).find(r => r.name === 'Beech1' && !r.added);
  await lookAt(page(), tree.x, tree.z, 0, 50, 35);
  await page().keyboard.press('b'); await sleep(200);
  const [ax, ay] = await screenOf(page(), tree.x - 5, tree.z - 5), [bx, by] = await screenOf(page(), tree.x + 5, tree.z + 5);
  await page().mouse.move(ax, ay); await page().mouse.down(); await page().mouse.move(bx, by, { steps: 8 }); await page().mouse.up(); await sleep(300);
  await page().click('#scriptToggle');
  await page().select('#scPick', 'ex:Count the kinds in the selection');
  await page().click('#scRun'); await sleep(800);
  assert.match(await page().$eval('#scOut', e => e.textContent), /Beech1/, 'the beech inside is counted');
  noErrors();
});

});
