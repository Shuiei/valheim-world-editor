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

});
