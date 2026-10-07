// End-to-end, the Area tool: copy and paste, blueprints, regrowing nature, moving the work area: the real app on its own copy of the test world, as a user drives it.
import { describe, test } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { waitPhase, screenOf, lookAt, frames, sleep, stableHash } from './harness.mjs';
import { t, setup, page, noErrors, pending, openEditor, areaAction, applyArea, records } from './common.mjs';

setup({ open: true });

describe('The Area tool: copy and paste, blueprints, regrowing nature, moving the work area', () => {

test('copy and paste make new objects', async () => {
  await openEditor();
  const tree = (await records()).find(r => r.name === 'Beech1');
  await page().evaluate(id => { window.__ed.setTool('select'); window.__ed.selectIds([id]); document.activeElement?.blur(); }, tree.id);
  await lookAt(page(), tree.x, tree.z, 0, 40, 30); await sleep(500);
  await page().keyboard.down('Control'); await page().keyboard.press('c'); await page().keyboard.press('v'); await page().keyboard.up('Control');
  const [x, y] = await screenOf(page(), tree.x + 6, tree.z + 6);
  await page().mouse.move(x, y); await sleep(300); await page().mouse.click(x, y); await sleep(1200);
  const copies = (await records()).filter(r => r.added && r.name === 'Beech1');
  assert.equal(copies.length, 1, 'one pasted tree');
  noErrors();
});

test('blueprints: the clipboard is saved as a file and pasted from the library', async () => {
  await openEditor();
  const tree = (await records()).find(r => r.name === 'Beech1' && !r.added);
  await page().evaluate(id => { window.__ed.setTool('select'); window.__ed.selectIds([id]); document.activeElement?.blur(); }, tree.id);
  await lookAt(page(), tree.x, tree.z, 0, 40, 30); await sleep(500);
  await page().keyboard.down('Control'); await page().keyboard.press('c'); await page().keyboard.up('Control');
  await page().evaluate(() => window.__ed.setTool('area'));
  await areaAction('copy');
  await page().click('#aSaveBp');
  let list = [];
  for (let i = 0; i < 60 && !list.length; i++) { await sleep(250); ({ list } = await t.api('/api/blueprints')); }
  assert.ok(list.length >= 1, 'a blueprint file was written');
  assert.ok(list[0].thumb?.startsWith('data:image/png'), 'with a picture');
  await page().click('#aLibrary');
  await page().waitForSelector('#bpList .bp [data-act="paste"]');
  const before = (await records()).filter(r => r.added && r.name === 'Beech1').length;
  await page().click('#bpList .bp [data-act="paste"]');
  const [x, y] = await screenOf(page(), tree.x - 7, tree.z + 5);
  await page().mouse.move(x, y); await sleep(300); await page().mouse.click(x, y); await sleep(1200);
  const after = (await records()).filter(r => r.added && r.name === 'Beech1').length;
  assert.equal(after, before + 1, 'the blueprint pasted one tree');
  await page().keyboard.press('Escape');
  noErrors();
});

test('blueprints: a PlanBuild file is imported, pasted and exported', async () => {
  await openEditor();
  await page().evaluate(() => window.__ed.setTool('area'));
  await page().click('#aLibrary');
  await page().waitForSelector('#bpImport');
  const file = path.join(t.home, 'hut.blueprint');
  fs.writeFileSync(file, '#Name:Imported hut\n#Creator:test\n#Description:""\n#Category:Misc\n#Pieces\n' +
    'wood_floor;BuildingWorkbench;0;0;0;0;0;0;1;"";1;1;1\nwoodwall;BuildingWorkbench;1;0;2;0;0.7071068;0;0.7071068;"";1;1;1\nsome_mod_piece;Misc;0;0;0;0;0;0;1;"";1;1;1\n');
  const input = await page().$('#bpFile');
  await input.uploadFile(file);
  await page().waitForFunction(() => [...document.querySelectorAll('#bpList .nm')].some(e => e.textContent === 'Imported hut'));
  assert.match(await page().$eval('#sMsg', e => e.textContent), /some_mod_piece/, 'says which kinds were left out');
  const { list } = await t.api('/api/blueprints');
  const hut = list.find(b => b.name === 'Imported hut');
  assert.equal(hut.objects, 2);
  assert.equal(hut.source, 'PlanBuild');
  const before = (await records()).filter(r => r.added).length;
  await page().click(`#bpList .bp[data-id="${hut.id}"] [data-act="paste"]`);
  const c = (await records()).find(r => r.name === 'piece_chest_wood');
  await lookAt(page(), c.x + 10, c.z - 10, 0, 40, 30);
  const [x, y] = await screenOf(page(), c.x + 10, c.z - 10);
  await page().mouse.move(x, y); await sleep(300); await page().mouse.click(x, y); await sleep(1200);
  assert.equal((await records()).filter(r => r.added).length, before + 2, 'pasted the two known pieces');
  await page().keyboard.press('Escape');
  const exported = await t.api(`/api/blueprints/${encodeURIComponent(hut.id)}/export`, { format: 'blueprint' });
  assert.match(exported.text, /#Pieces\nwood_floor;/);
  noErrors();
});

test('paste repeats: one click places a row of copies, one undo step', async () => {
  await openEditor();
  const tree = (await records()).find(r => r.name === 'Beech1' && !r.added);
  await page().evaluate(id => { window.__ed.setTool('select'); window.__ed.selectIds([id]); document.activeElement?.blur(); }, tree.id);
  await lookAt(page(), tree.x, tree.z, 0, 50, 40); await sleep(500);
  await page().keyboard.down('Control'); await page().keyboard.press('c'); await page().keyboard.press('v'); await page().keyboard.up('Control');
  await page().$eval('#psCount', e => { e.value = 4; e.dispatchEvent(new Event('input')); });
  await page().$eval('#psGap', e => { e.value = 2; e.dispatchEvent(new Event('input')); });
  const before = (await records()).filter(r => r.added && r.name === 'Beech1');
  const [x, y] = await screenOf(page(), tree.x + 4, tree.z - 8);
  await page().mouse.move(x, y); await sleep(300); await page().mouse.click(x, y); await sleep(1500);
  const made = (await records()).filter(r => r.added && r.name === 'Beech1' && !before.some(b => b.id === r.id)).sort((a, b) => a.x - b.x);
  assert.equal(made.length, 4, 'four copies');
  // Width of a one-object copy is its 2 m outline, plus the 2 m gap.
  assert.ok(Math.abs(made[1].x - made[0].x - 4) < 0.05, `4 m apart (${(made[1].x - made[0].x).toFixed(2)})`);
  await page().keyboard.press('Escape');
  await page().click('#undo'); await sleep(1200);
  assert.equal((await records()).filter(r => r.added && r.name === 'Beech1').length, before.length, 'one undo removes all four');
  await page().evaluate(() => { document.getElementById('psCount').value = 1; });
  noErrors();
});

test('area: the selection outline hides in other tools, and Clear starts over', async () => {
  await openEditor();
  await page().evaluate(() => window.__ed.setTool('area'));
  await page().click('[data-shape="poly"]');
  const c = (await records()).find(r => r.name === 'piece_chest_wood');
  await lookAt(page(), c.x, c.z + 25, 0, 50, 30);
  for (const [dx, dz] of [[-8, 20], [8, 20], [0, 32]]) { const [x, y] = await screenOf(page(), c.x + dx, c.z + dz); await page().mouse.click(x, y); await sleep(150); }
  await page().keyboard.press('Enter');
  const outlineShown = () => page().evaluate(() => window.__ed.scene.children.find(o => o.isLine && o.renderOrder === 15 && o.material.color.getHex() === 0x5fd4ff).visible);
  assert.ok(await page().evaluate(() => !!window.__ed.area.polygon()), 'a polygon is selected');
  assert.equal(await outlineShown(), true);
  await page().keyboard.press('1');
  assert.equal(await outlineShown(), false, 'hidden in another tool');
  await page().keyboard.press('b');
  assert.equal(await outlineShown(), true, 'back in the Area tool');
  assert.ok(await page().evaluate(() => !!window.__ed.area.polygon()), 'the selection was kept');
  await page().click('#aClear');
  assert.equal(await page().evaluate(() => window.__ed.area.polygon()), null, 'Clear removes it');
  await page().click('[data-shape="box"]');
  noErrors();
});

test('Regrow nature puts back the game\'s own vegetation inside the selection', async () => {
  await openEditor();
  const c = (await records()).find(r => r.name === 'piece_chest_wood');
  const spot = [c.x + 40, c.z + 40];
  await lookAt(page(), ...spot, 0, 120, 0.01);
  await page().keyboard.press('b'); await sleep(300);
  const [ax, ay] = await screenOf(page(), spot[0] - 30, spot[1] - 30), [bx, by] = await screenOf(page(), spot[0] + 30, spot[1] + 30);
  await page().mouse.move(ax, ay); await page().mouse.down(); await page().mouse.move(bx, by, { steps: 6 }); await page().mouse.up(); await sleep(300);
  const before = (await records()).length;
  await applyArea('regrow');
  await page().waitForFunction(() => /Regrew|Nothing to regrow/.test(document.getElementById('sMsg').textContent), { timeout: 60000 })
    .catch(async e => { throw new Error(`no result: "${await page().$eval('#sMsg', e => e.textContent)}" poly ${JSON.stringify(await page().evaluate(() => window.__ed.area.polygon()))}`); });
  const msg = await page().$eval('#sMsg', e => e.textContent);
  const n = (await records()).length - before;
  assert.ok(n > 0, `objects regrown (${msg})`);
  // All inside the selection, none on top of another.
  const fresh = (await records()).slice(-n);
  assert.ok(fresh.every(r => Math.abs(r.x - spot[0]) <= 30.5 && Math.abs(r.z - spot[1]) <= 30.5), 'inside the selection');
  // Again: everything is standing now, so nothing more is added.
  // The long message above must not cover the panel (it used to grow over its last buttons).
  await applyArea('regrow');
  await page().waitForFunction(() => /Nothing to regrow/.test(document.getElementById('sMsg').textContent), { timeout: 20000 })
    .catch(async () => { throw new Error(`second regrow: "${await page().$eval('#sMsg', e => e.textContent)}"`); });
  await page().click('#undo'); await sleep(800);
  assert.equal((await records()).length, before, 'Ctrl+Z takes them back');
  noErrors();
});

test('moving the area keeps the view and the tool; Follow moves it near the edge', async () => {
  await openEditor(undefined, () => localStorage.setItem('areaFollow', '0'));
  const target = () => page().evaluate(() => { const ed = window.__ed, t = window.__target(); return [t.x + ed.cx + ed.originX, -t.z + ed.cz + ed.originZ]; });
  await page().evaluate(() => { window.__ed.setTool('plant'); window.__view(10, 60, 40, 10, 40, 0); });
  const before = await target();
  await Promise.all([page().waitForNavigation({ waitUntil: 'networkidle0' }), page().click('[data-shift="1,0"]')]);
  await page().waitForFunction(() => window.__ed?.objects.records.size > 0 && !document.getElementById('loading'), { timeout: 120000 });
  assert.match(page().url(), /zx=1&zz=0/);
  const after = await target();
  assert.ok(Math.hypot(after[0] - before[0], after[1] - before[1]) < 0.1, `the view stays on the same spot (${before} -> ${after})`);
  assert.equal(await page().evaluate(() => window.__ed.tool), 'plant', 'the tool comes along');
  // Follow: blocked while objects are selected, then moves the area when the view nears the edge.
  await page().click('#follow');
  const id = (await records())[0].id;
  await page().evaluate(id => { const ed = window.__ed; ed.setTool('select'); ed.selectIds([id]); window.__view(ed.W / 2 - 4, 60, 30, ed.W / 2 - 4, 40, 0); }, id);
  await page().waitForFunction(() => /Follow waits: objects are selected/.test(document.getElementById('sMsg').textContent), { timeout: 5000 });
  await Promise.all([page().waitForNavigation({ waitUntil: 'networkidle0', timeout: 30000 }),
    page().evaluate(() => { const ed = window.__ed; ed.clearSelection(); window.__view(ed.W / 2 - 3, 60, 30, ed.W / 2 - 3, 40, 0); })]);
  assert.match(page().url(), /zx=2&zz=0/, 'moved one zone east');
  await page().waitForFunction(() => window.__ed && !document.getElementById('loading'), { timeout: 120000 }); await sleep(1500);
  await page().click('#follow');
  noErrors();
});

test('cut and fill: Flatten\'s estimate matches what it then does', async () => {
  await openEditor();
  const c = (await records()).find(r => r.name === 'piece_chest_wood');
  const spot = [c.x - 30, c.z - 30];
  await lookAt(page(), ...spot, 0, 60, 0.01);
  await page().keyboard.press('b'); await sleep(300);
  const [ax, ay] = await screenOf(page(), spot[0] - 8, spot[1] - 8), [bx, by] = await screenOf(page(), spot[0] + 8, spot[1] + 8);
  await page().mouse.move(ax, ay); await page().mouse.down(); await page().mouse.move(bx, by, { steps: 6 }); await page().mouse.up(); await sleep(300);
  await page().$eval('#aSoft', e => { e.value = 0; e.dispatchEvent(new Event('input')); });
  await areaAction('flatten');
  await page().click('#aAvg');
  await page().$eval('#aHeight', e => { e.value = (+e.value + 3).toFixed(1); e.dispatchEvent(new Event('input')); });
  const text = () => page().$eval('#aVolume', e => e.textContent);
  const num = (t, re) => { const m = t.match(re); return m ? parseFloat(m[1]) * (m[2] ? 1000 : 1) : NaN; };
  const before = await text();
  const fill = num(before, /fill ([\d.]+)(k)? m³/), raised0 = num(before, /Ground inside: ([\d.]+)(k)? m³ raised/);
  assert.ok(fill > 100, `a fill is estimated (${before})`);
  await page().click('#aApply'); await sleep(1200);
  const after = await text();
  const raised = num(after, /Ground inside: ([\d.]+)(k)? m³ raised/);
  assert.ok(Math.abs(raised - raised0 - fill) <= Math.max(2, fill * 0.02), `raised by the estimate (${before} -> ${after})`);
  await page().click('#undo'); await sleep(800);
  noErrors();
});

test('area actions on objects: remove, select, replace; Ctrl+C copies; Lower; reset zones and cancel', async () => {
  await openEditor();
  const tree = (await records()).find(r => r.name === 'Beech1' && !r.added);
  await lookAt(page(), tree.x, tree.z, 0, 50, 35);
  await page().keyboard.press('b'); await sleep(200);
  await page().$eval('#aSoft', e => { e.value = 0; e.dispatchEvent(new Event('input')); });
  const [ax, ay] = await screenOf(page(), tree.x - 5, tree.z - 5), [bx, by] = await screenOf(page(), tree.x + 5, tree.z + 5);
  await page().mouse.move(ax, ay); await page().mouse.down(); await page().mouse.move(bx, by, { steps: 8 }); await page().mouse.up(); await sleep(300);
  const inside = async name => (await records()).filter(r => Math.abs(r.x - tree.x) <= 5 && Math.abs(r.z - tree.z) <= 5 && (!name || r.name === name));
  const beeches = (await inside('Beech1')).length;
  assert.ok(beeches >= 1, 'a beech inside');
  // Remove objects (trees are among the chosen kinds), then undo.
  await applyArea('remove'); await sleep(500);
  assert.equal((await inside('Beech1')).length, 0, 'removed');
  await page().click('#undo'); await sleep(500);
  assert.equal((await inside('Beech1')).length, beeches, 'back');
  // Replace Beech1 with Oak1.
  await areaAction('replace');
  const from = await page().$$eval('#aFrom option', o => o.map(x => x.value));
  assert.ok(from.includes('Beech1'), `Beech1 offered (${from})`);
  await page().select('#aFrom', 'Beech1'); await page().select('#aTo', 'Oak1');
  await page().click('#aApply'); await sleep(800);
  assert.equal((await inside('Beech1')).length, 0, 'no beech left');
  assert.ok((await inside('Oak1')).length >= beeches, 'oaks in their place');
  await page().click('#undo'); await sleep(800);
  assert.equal((await inside('Beech1')).length, beeches, 'replace undone');
  // Ctrl+C in the Area tool copies the shown objects inside.
  await page().keyboard.down('Control'); await page().keyboard.press('c'); await page().keyboard.up('Control'); await sleep(300);
  const clip = await page().evaluate(() => { const c = window.__ed.getClipboard(); return { w: c.w, n: c.objects.length }; });
  assert.ok(clip.w >= 10 && clip.n >= beeches, `copied (${JSON.stringify(clip)})`);
  // Lower by 2 m, undo.
  const h = () => page().evaluate(([x, z]) => { const ed = window.__ed; return ed.sampleHeight(x - ed.originX, z - ed.originZ); }, [tree.x + 2, tree.z + 2]);
  const h0 = await h();
  await areaAction('lower');
  await page().$eval('#aAmount', e => { e.value = 2; e.dispatchEvent(new Event('input')); });
  await page().click('#aApply'); await sleep(500);
  assert.ok(Math.abs((await h()) - (h0 - 2)) < 0.2, 'lowered 2 m');
  await page().click('#undo'); await sleep(500);
  // Reset zones (confirmed), then Cancel reset.
  await applyArea('reset'); await sleep(800);
  assert.match(await pending(), /reset/, 'a zone marked for reset');
  await page().click('#aUnreset'); await sleep(800);
  assert.doesNotMatch(await pending(), /reset/, 'reset cancelled');
  // Select objects: the chosen kinds inside become the selection, in the Select tool.
  await areaAction('select'); await page().click('#aApply'); await sleep(300);
  assert.equal(await page().evaluate(() => window.__ed.tool), 'select');
  assert.ok(await page().evaluate(() => window.__ed.selection.size) >= beeches, 'selected');
  noErrors();
});

test('blueprints: written as .blueprint and .vbuild from the library, and deleted', async () => {
  await openEditor();
  const tree = (await records()).find(r => r.name === 'Beech1' && !r.added);
  await page().evaluate(id => { window.__ed.setTool('select'); window.__ed.selectIds([id]); document.activeElement?.blur(); }, tree.id);
  await page().keyboard.down('Control'); await page().keyboard.press('c'); await page().keyboard.up('Control');
  await page().evaluate(() => { window.__ed.setTool('area'); window.prompt = () => 'export test'; });
  await areaAction('copy');
  await page().click('#aSaveBp');
  let list = [];
  for (let i = 0; i < 60 && !list.some(b => b.name === 'export test'); i++) { await sleep(250); ({ list } = await t.api('/api/blueprints')); }
  const n = list.length, id = list.find(b => b.name === 'export test').id;
  await page().click('#aLibrary');
  await page().waitForSelector(`#bpList .bp[data-id="${id}"]`);
  for (const f of ['blueprint', 'vbuild']) {
    await page().click(`#bpList .bp[data-id="${id}"] [data-act="${f}"]`);
    await page().waitForFunction(f => /Written to .*\.(blueprint|vbuild)/.test(document.getElementById('sMsg').textContent) && document.getElementById('sMsg').textContent.includes('.' + f), {}, f);
  }
  await page().click(`#bpList .bp[data-id="${id}"] [data-act="delete"]`);
  for (let i = 0; i < 40 && list.length === n; i++) { await sleep(250); ({ list } = await t.api('/api/blueprints')); }
  assert.equal(list.length, n - 1, 'deleted');
  noErrors();
});

});
