// End-to-end, saving, backups, undo and the history: the real app on its own copy of the test world, as a user drives it.
import { describe, test } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { waitPhase, screenOf, lookAt, frames, sleep, stableHash } from './harness.mjs';
import { t, setup, page, noErrors, pending, openEditor, areaAction, applyArea, records } from './common.mjs';

setup({ open: true });

describe('Saving, backups, undo and the history', () => {

test('3D editor loads the objects with their models', async () => {
  await openEditor();
  const drawn = await page().evaluate(() => [...window.__ed.objects.records.values()].filter(r => window.__ed.entityOf(r.id)?.inst.length).length);
  assert.ok(drawn >= 30, `objects drawn: ${drawn}`);
  noErrors();
});

test('a Raise stroke, then Save, writes a new save', async () => {
  await openEditor();
  const chest = (await records()).find(r => r.name === 'piece_chest_wood');
  await lookAt(page(), chest.x + 15, chest.z + 15);
  await page().keyboard.press('1');
  const [x, y] = await screenOf(page(), chest.x + 15, chest.z + 15);
  await page().mouse.move(x, y); await page().mouse.down(); await page().mouse.move(x + 4, y + 4, { steps: 25 }); await page().mouse.up();
  await sleep(800);
  assert.match(await pending(), /1 zone/);
  await page().click('#saveBtn');
  await page().waitForNavigation({ waitUntil: 'networkidle0', timeout: 120000 }).catch(() => {});
  const w = await t.api('/api/world');
  assert.equal(w.saveNumber, 3, 'saved as save #3');
  assert.equal(w.changedZones, 0);
  noErrors();
});

test('Plant: a line and a grid place objects, which are saved', async () => {
  await openEditor(undefined, () => { localStorage.setItem('plantChosen', '["Bush01"]'); });
  const before = (await t.api('/api/world')).objects;
  const c = (await records()).find(r => r.name === 'piece_chest_wood');
  await lookAt(page(), c.x, c.z + 20, 0, 45, 30);
  await page().keyboard.press('t'); await sleep(300);
  await page().click('#plModes [data-m="line"]');
  await page().$eval('#plEvery', e => { e.value = 4; e.dispatchEvent(new Event('input')); });
  for (const [dx, dz] of [[-12, 18], [0, 24], [12, 18]]) { const [x, y] = await screenOf(page(), c.x + dx, c.z + dz); await page().mouse.click(x, y); await sleep(150); }
  const shown = await page().$eval('#plPreview', e => +e.textContent.match(/\d+/)[0]);
  assert.ok(shown >= 4, `line preview shows objects (${shown})`);
  await page().keyboard.press('Enter'); await sleep(1000);
  await page().click('#plModes [data-m="grid"]');
  await page().$eval('#plCell', e => { e.value = 4; e.dispatchEvent(new Event('input')); });
  { const [a, b] = await screenOf(page(), c.x - 8, c.z + 28); const [x2, y2] = await screenOf(page(), c.x + 8, c.z + 36); await page().mouse.move(a, b); await page().mouse.down(); await page().mouse.move(x2, y2, { steps: 6 }); await page().mouse.up(); await sleep(400); }
  const grid = await page().$eval('#plPreview', e => +e.textContent.match(/\d+/)[0]);
  assert.ok(grid >= 2, `grid preview shows objects (${grid})`);
  await page().keyboard.press('Enter'); await sleep(1000);
  const added = (await records()).filter(r => r.added).length;
  assert.equal(added, shown + grid, 'placed what the previews showed');
  await page().click('#saveBtn');
  await page().waitForNavigation({ waitUntil: 'networkidle0', timeout: 120000 }).catch(() => {});
  assert.equal((await t.api('/api/world')).objects, before + added);
  noErrors();
});

test('restore: a deleted tree comes back from the backup made when saving', async () => {
  await openEditor();
  const tree = (await records()).find(r => r.name === 'Beech1' && !r.added);
  await page().evaluate(id => { window.__ed.setTool('select'); window.__ed.selectIds([id]); document.activeElement?.blur(); }, tree.id);
  await page().keyboard.press('Delete'); await sleep(800);
  await page().click('#saveBtn');
  await page().waitForNavigation({ waitUntil: 'networkidle0', timeout: 120000 }).catch(() => {});
  await openEditor();
  assert.ok(!(await records()).some(r => r.name === 'Beech1' && Math.abs(r.x - tree.x) < 0.01 && Math.abs(r.z - tree.z) < 0.01), 'the tree is gone after saving');
  // Earlier tests can leave other changes that this save wrote; outside the selection nothing may change.
  const outside = r => Math.abs(r.x - tree.x) > 8 || Math.abs(r.z - tree.z) > 8;
  const others = (await records()).filter(outside).map(r => r.id).sort();
  await page().evaluate(() => window.__ed.setTool('area'));
  await lookAt(page(), tree.x, tree.z, 0, 50, 35);
  const [ax, ay] = await screenOf(page(), tree.x - 6, tree.z - 6), [bx, by] = await screenOf(page(), tree.x + 6, tree.z + 6);
  await page().mouse.move(ax, ay); await page().mouse.down(); await page().mouse.move(bx, by, { steps: 6 }); await page().mouse.up();
  await page().waitForFunction(() => document.querySelectorAll('#aBackup option').length > 2);
  await page().$eval('#aBackup', e => { e.selectedIndex = 1; e.dispatchEvent(new Event('change')); });
  await applyArea('backup'); await sleep(2000);
  const back = (await records()).find(r => r.name === 'Beech1' && Math.abs(r.x - tree.x) < 0.01 && Math.abs(r.z - tree.z) < 0.01);
  assert.ok(back?.added, 'the tree is back, as a restored object');
  assert.deepEqual((await records()).filter(outside).map(r => r.id).sort(), others, 'nothing outside the selection changed');
  noErrors();
});

test('undo takes a change back and the counter clears', async () => {
  await openEditor();
  await page().keyboard.press('Escape');
  const c = (await records()).find(r => r.name === 'piece_chest_wood');
  await lookAt(page(), c.x - 15, c.z - 15);
  // Earlier tests may have left unsaved changes: the counter must come back to what it was.
  const start = await pending();
  await page().keyboard.press('1');
  const [x, y] = await screenOf(page(), c.x - 15, c.z - 15);
  await page().mouse.move(x, y); await page().mouse.down(); await page().mouse.move(x + 3, y + 3, { steps: 20 }); await page().mouse.up(); await sleep(800);
  assert.match(await pending(), /zone/);
  await page().click('#undo'); await sleep(1500);
  assert.equal(await pending(), start);
  noErrors();
});

test('history survives a reload of the page and a move of the work area', async () => {
  await openEditor();
  await page().keyboard.press('Escape');
  // The middle of zone (0, 0): inside both work areas below.
  const c = { x: 15, z: -15 };
  const start = await pending();
  const h = () => page().evaluate(([x, z]) => { const ed = window.__ed; return ed.sampleHeight(x - ed.originX, z - ed.originZ); }, [c.x - 15, c.z + 15]);
  const h0 = await h();
  await lookAt(page(), c.x - 15, c.z + 15);
  await page().keyboard.press('1');
  const [x, y] = await screenOf(page(), c.x - 15, c.z + 15);
  await page().mouse.move(x, y); await page().mouse.down(); await page().mouse.move(x + 3, y + 3, { steps: 20 }); await page().mouse.up(); await sleep(1500);
  assert.ok(await h() > h0 + 0.05, 'raised');
  // Reload: the change is still there, and so is its undo.
  await openEditor();
  assert.ok(await h() > h0 + 0.05, 'still raised after the reload');
  assert.equal(await page().$eval('#undo', e => e.disabled), false, 'undo is available after the reload');
  // Another work area (one zone east, still holding the spot): the change can still be undone.
  await openEditor('zx=1&zz=0&size=3');
  assert.equal(await page().$eval('#undo', e => e.disabled), false, 'undo is available in the moved area');
  await page().click('#undo'); await sleep(1500);
  assert.ok(Math.abs(await h() - h0) < 0.01, 'undone');
  assert.equal(await pending(), start);
  noErrors();
});

test('history panel: redo, back to here, redo to here, remove one change; Discard undoes the rest', async () => {
  await openEditor();
  const c = (await records()).find(r => r.name === 'piece_chest_wood');
  const spots = [[c.x + 20, c.z - 20], [c.x + 40, c.z - 20], [c.x + 60, c.z - 20]];
  await lookAt(page(), c.x + 40, c.z - 20, 0, 70, 40);
  const h = () => page().evaluate(ps => ps.map(([x, z]) => { const ed = window.__ed; return ed.sampleHeight(x - ed.originX, z - ed.originZ); }), spots);
  const h0 = await h();
  await page().keyboard.press('1');
  for (const p of spots) { const [x, y] = await screenOf(page(), ...p); await page().mouse.move(x, y); await page().mouse.down(); await page().mouse.move(x + 2, y + 2, { steps: 20 }); await page().mouse.up(); await sleep(400); }
  const up = (now, i) => now[i] > h0[i] + 0.05;
  assert.ok((await h()).every((v, i) => v > h0[i] + 0.05), 'three raises');
  await page().click('#historyToggle');
  const rows = () => page().$$eval('#histList .hrow', r => r.map(x => x.className));
  assert.equal((await rows()).length, 3);
  // Ctrl+Z, then Redo.
  await page().keyboard.down('Control'); await page().keyboard.press('z'); await page().keyboard.up('Control'); await sleep(400);
  assert.ok(!up(await h(), 2), 'undo took the last one back');
  await page().click('#redo'); await sleep(400);
  assert.ok(up(await h(), 2), 'redo put it back');
  // Back to here on the first change: the two after it are undone, and listed greyed.
  await page().click('#histList .hrow:nth-child(3) [data-act="back"]'); await sleep(500);
  let now = await h();
  assert.ok(up(now, 0) && !up(now, 1) && !up(now, 2), 'back to the first change');
  assert.equal((await rows()).filter(c => c.includes('undone')).length, 2);
  // Redo to here on the newest: everything is back.
  await page().click('#histList .hrow:nth-child(1) [data-act="fwd"]'); await sleep(500);
  now = await h();
  assert.ok(up(now, 0) && up(now, 1) && up(now, 2), 'redone up to the last');
  // Remove the middle one only.
  await page().click('#histList .hrow:nth-child(2) [data-act="rm"]'); await sleep(500);
  now = await h();
  assert.ok(up(now, 0) && !up(now, 1) && up(now, 2), 'only the middle change taken out');
  // Discard: back to the saved world, in place.
  await page().click('#discardBtn'); await sleep(1000);
  now = await h();
  assert.ok(now.every((v, i) => Math.abs(v - h0[i]) < 0.01), 'Discard undid the rest');
  assert.match(await pending(), /saved/i);
  noErrors();
});

});
