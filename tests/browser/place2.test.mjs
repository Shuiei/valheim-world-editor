// End-to-end, the Place tool and panels: picking kinds, pieces, View, the tool panels: the real app on its own copy of the test world, as a user drives it.
import { describe, test } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { waitPhase, screenOf, lookAt, frames, sleep, stableHash } from './harness.mjs';
import { t, setup, page, noErrors, pending, openEditor, areaAction, applyArea, records } from './common.mjs';

setup({ open: true });

describe('The Place tool and panels: picking kinds, pieces, View, the tool panels', () => {

test('eyedropper, favourite and recent kinds', async () => {
  await openEditor(undefined, () => { localStorage.setItem('plantChosen', '["Bush01"]'); localStorage.removeItem('plantFavourites'); localStorage.removeItem('plantRecent'); });
  const chosen = () => page().evaluate(() => JSON.parse(localStorage.getItem('plantChosen')));
  // An object that is drawn (shown) and of a kind that can be placed.
  const c = await page().evaluate(() => {
    const ed = window.__ed, ok = new Set(ed.objects.creatableTypes().map(t => t.name));
    const r = [...ed.objects.records.values()].find(r => !r.deleted && r.name === 'Beech1' && ed.entityOf(r.id)?.inst.some(({ im }) => im.visible && im.parent?.visible) && ok.has(r.name));
    return { id: r.id, name: r.name, x: r.x, z: r.z };
  });
  // Screen point of the middle of its drawn box.
  const onChest = () => page().evaluate(id => {
    const ed = window.__ed, v = ed.boxOf(ed.entityOf(id)).getCenter(new ed.THREE.Vector3()).project(ed.camera), r = ed.el.getBoundingClientRect();
    return [r.left + (v.x + 1) / 2 * r.width, r.top + (1 - v.y) / 2 * r.height];
  }, c.id);
  await lookAt(page(), c.x, c.z, 0, 10, 4);
  await page().keyboard.press('t'); await sleep(300);
  await page().evaluate(() => window.__ed.plantDrawer(true));
  const before = (await records()).length;
  // Pick: the clicked object's kind becomes the only one ticked, and nothing is planted.
  await page().click('#plPick');
  { const [x, y] = await onChest(); await page().mouse.click(x, y); await sleep(300); }
  assert.deepEqual(await chosen(), [c.name], await page().$eval('#sMsg', e => e.textContent));
  assert.equal((await records()).length, before, 'the picking click places nothing');
  // A star adds a favourite chip (without ticking the kind); a click on the chip ticks it, a second
  // one unticks it, Shift + click places only it.
  await page().$eval('#plSearch', e => { e.value = 'Bush01'; e.dispatchEvent(new Event('input')); });
  await page().click('[data-star="Bush01"]'); await sleep(100);
  assert.deepEqual(await chosen(), [c.name], 'the star does not tick the box');
  assert.ok(await page().$('#plFav [data-chip="Bush01"]'), 'favourite chip shown');
  await page().click('#plFav [data-chip="Bush01"]');
  assert.deepEqual((await chosen()).sort(), ['Bush01', c.name].sort());
  await page().click('#plFav [data-chip="Bush01"]');
  assert.deepEqual(await chosen(), [c.name]);
  await page().keyboard.down('Shift'); await page().click('#plFav [data-chip="Bush01"]'); await page().keyboard.up('Shift');
  assert.deepEqual(await chosen(), ['Bush01']);
  // Untick all, then all / none on a chip row.
  await page().click('#plNone');
  assert.deepEqual(await chosen(), []);
  await page().click('[data-all="plFav"]');
  assert.deepEqual(await chosen(), ['Bush01']);
  await page().click('[data-none="plFav"]');
  assert.deepEqual(await chosen(), []);
  await page().click('#plFav [data-chip="Bush01"]');
  // Shift + click with Pick adds the kind to the ticked ones.
  await page().keyboard.down('Shift'); await page().click('#plPick');
  { const [x, y] = await onChest(); await page().mouse.click(x, y); await sleep(300); }
  await page().keyboard.up('Shift');
  assert.deepEqual((await chosen()).sort(), ['Bush01', c.name].sort());
  // Placing remembers the kinds as recent.
  await page().keyboard.down('Shift'); await page().click('#plFav [data-chip="Bush01"]'); await page().keyboard.up('Shift');
  await page().click('#plModes [data-m="line"]');
  await lookAt(page(), c.x, c.z + 20, 0, 45, 30);
  for (const [dx, dz] of [[-6, 20], [6, 20]]) { const [x, y] = await screenOf(page(), c.x + dx, c.z + dz); await page().mouse.click(x, y); await sleep(150); }
  await page().keyboard.press('Enter'); await sleep(1000);
  assert.ok(await page().$('#plRecent [data-chip="Bush01"]'), 'recent chip shown');
  // Select tool: pick fills the Replace list and leaves the selection alone.
  await page().keyboard.press('Escape');
  await page().evaluate(() => window.__ed.setTool('select'));
  await lookAt(page(), c.x, c.z, 0, 10, 4);
  await page().click('#selToPick');
  { const [x, y] = await onChest(); await page().mouse.click(x, y); await sleep(300); }
  assert.equal(await page().$eval('#selTo', e => e.value), c.name);
  assert.equal(await page().evaluate(() => window.__ed.selection.size), 0, 'the picking click selects nothing');
  await page().click('#undo'); await sleep(1000);
  noErrors();
});

test('placed objects follow the View switches', async () => {
  await openEditor(undefined, () => localStorage.setItem('plantChosen', '["Beech1"]'));
  const c = (await records()).find(r => r.name === 'piece_chest_wood');
  await lookAt(page(), c.x + 30, c.z + 30, 0, 40, 25);
  await page().keyboard.press('t'); await sleep(300);
  await page().click('#plModes [data-m="brush"]');
  await page().click('#plSingle').catch(() => {});
  { const [x, y] = await screenOf(page(), c.x + 30, c.z + 30); await page().mouse.move(x, y); await sleep(200); await page().mouse.click(x, y); await sleep(800); }
  const id = await page().evaluate(() => Math.min(...[...window.__ed.objects.records.values()].filter(r => r.added && !r.deleted && r.name === 'Beech1').map(r => r.id)));
  assert.ok(id < 0, 'a tree was placed');
  const shown = () => page().evaluate(id => { const e = window.__ed.entityOf(id); let o = e.inst[0].im; while (o) { if (!o.visible) return false; o = o.parent; } return true; }, id);
  assert.ok(await shown(), 'shown');
  await page().$eval('[data-show="trees"]', e => { e.checked = false; e.dispatchEvent(new Event('change')); });
  assert.equal(await shown(), false, 'hidden with Trees & logs off');
  await page().$eval('[data-show="trees"]', e => { e.checked = true; e.dispatchEvent(new Event('change')); });
  assert.ok(await shown(), 'shown again');
  await page().click('#undo'); await sleep(800);
  noErrors();
});

test('tool panels: help behind ?, one Area action at a time, Place kinds in a drawer, folds remembered', async () => {
  await openEditor();
  await page().evaluate(() => { localStorage.removeItem('toolHelp'); localStorage.removeItem('toolMoreOpen'); localStorage.setItem('plantChosen', '[]'); localStorage.removeItem('plantDrawer'); localStorage.setItem('areaAction', 'raise'); });
  await page().goto(`${t.base}/editor.html?zx=0&zz=0&size=3`, { waitUntil: 'networkidle0', timeout: 120000 });
  await page().waitForFunction(() => window.__ed?.objects.records.size > 0 && !document.getElementById('loading'), { timeout: 120000 });
  await sleep(800);
  const shown = id => page().$eval(id, e => e.getClientRects().length > 0);
  // Help: hidden until ? is clicked.
  assert.equal(await shown('#toolDesc'), false, 'how-to text hidden at first');
  await page().click('#toolHelpBtn');
  assert.equal(await shown('#toolDesc'), true, '? shows it');
  await page().click('#toolHelpBtn');
  assert.equal(await shown('#toolDesc'), false, '? again hides it');
  // Area: only the chosen action's settings; Enter applies it.
  await page().keyboard.press('b'); await sleep(300);
  assert.equal(await page().$eval('#aAction', e => e.value), 'raise', 'the action is remembered');
  assert.equal(await shown('#aAmount'), true, 'Raise shows Amount');
  assert.equal(await shown('#aHeight'), false, 'and not Height');
  assert.equal(await shown('#aBackup'), false, 'nor the backup list');
  assert.match(await page().$eval('#aApply', e => e.textContent), /^Raise/);
  const c = (await records()).find(r => r.name === 'piece_chest_wood');
  const spot = [c.x - 30, c.z - 30];
  await lookAt(page(), ...spot, 0, 60, 0.01);
  const [ax, ay] = await screenOf(page(), spot[0] - 6, spot[1] - 6), [bx, by] = await screenOf(page(), spot[0] + 6, spot[1] + 6);
  await page().mouse.move(ax, ay); await page().mouse.down(); await page().mouse.move(bx, by, { steps: 6 }); await page().mouse.up(); await sleep(300);
  const h0 = await page().evaluate(([x, z]) => { const ed = window.__ed; return ed.sampleHeight(x - ed.originX, z - ed.originZ); }, spot);
  await page().keyboard.press('Enter'); await sleep(800);
  const h1 = await page().evaluate(([x, z]) => { const ed = window.__ed; return ed.sampleHeight(x - ed.originX, z - ed.originZ); }, spot);
  assert.ok(h1 - h0 > 1.5, `Enter raised the selection (${h0.toFixed(2)} -> ${h1.toFixed(2)})`);
  await page().click('#undo'); await sleep(600);
  await page().select('#aAction', 'backup');
  assert.equal(await shown('#aBackup'), true, 'Restore from a backup shows its list');
  assert.equal(await shown('#aAmount'), false);
  // The Mask sits in the Area fold, which says when it is on, and the fold stays open once opened.
  assert.ok(await page().$eval('[data-more="area"]', d => d.contains(document.getElementById('maskBox'))), 'mask in the Area fold');
  await page().click('[data-more="area"] > summary');
  await page().click('#mOn');
  assert.match(await page().$eval('[data-more="area"] > summary', e => e.textContent), /mask on/);
  await page().click('#mOn');
  // Place: nothing chosen, so the drawer opens; a kind from it shows in the panel with a ✕.
  await page().keyboard.press('t'); await sleep(300);
  assert.equal(await shown('#plDrawer'), true, 'the kind drawer opens when nothing is chosen');
  assert.ok(await page().$eval('[data-more="plant"]', d => d.contains(document.getElementById('maskBox'))), 'the mask follows the tool');
  await page().$eval('#plSearch', e => { e.value = 'Beech1'; e.dispatchEvent(new Event('input')); });
  await page().click('#plList input[value="Beech1"]');
  assert.ok(await page().$('#plMix [data-w="Beech1"]'), 'chosen kind listed in the panel');
  await page().click('#plDrawerClose');
  assert.equal(await shown('#plDrawer'), false, '✕ closes the drawer');
  await page().click('#plMix [data-w="Beech1"] .x');
  assert.equal(await page().$('#plMix [data-w="Beech1"]'), null, 'its ✕ drops the kind');
  // Select: Exact place waits for a selection; the folds keep their state across a reload.
  await page().keyboard.press('e'); await sleep(300);
  assert.equal(await shown('#selNum'), false, 'no Exact place without a selection');
  await page().reload({ waitUntil: 'networkidle0' });
  await page().waitForFunction(() => window.__ed?.objects.records.size > 0 && !document.getElementById('loading'), { timeout: 120000 });
  assert.ok(await page().$eval('[data-more="area"]', d => d.open), 'the opened fold is remembered');
  noErrors();
});

});
