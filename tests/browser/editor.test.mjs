// End-to-end: the start page, the map and the 3D editor on the test world, as a user drives them.
import { describe, test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { startApp, waitPhase, screenOf, lookAt, frames, sleep, stableHash } from './harness.mjs';

let t;
before(async () => { t = await startApp(); }, { timeout: 120000 });
after(async () => { await t?.stop(); });

const page = () => t.page;
const noErrors = () => assert.deepEqual(t.errors, [], 'no JavaScript errors on the page');
const pending = () => page().$eval('#pending', e => e.textContent);
async function openEditor() {
  await page().goto(`${t.base}/editor.html?zx=0&zz=0&size=3`, { waitUntil: 'networkidle0', timeout: 120000 });
  await page().waitForFunction(() => window.__ed?.objects.records.size > 0 && !document.getElementById('loading'), { timeout: 120000 });
  await sleep(1000);
  await page().evaluate(() => { document.getElementById('viewPanel').hidden = true; document.activeElement?.blur(); });
  await frames(page());
}
const records = () => page().evaluate(() => [...window.__ed.objects.records.values()].filter(r => !r.deleted).map(r => ({ id: r.id, name: r.name, x: r.x, y: r.y, z: r.z, added: r.added })));

describe('Valheim World Editor in the browser', () => {

test('start page lists the test world and opens it', async () => {
  await page().goto(t.base + '/', { waitUntil: 'networkidle0' });
  await page().click('.mode[data-mode="offline"]');
  await page().waitForSelector('.world');
  const names = await page().$$eval('.world .name', e => e.map(x => x.textContent));
  assert.ok(names.includes('CITest'), `CITest listed (got ${names})`);
  await page().click('.world');
  await page().waitForFunction(() => location.pathname === '/index.html', { timeout: 60000 });
  await waitPhase(t, 'editor');
  noErrors();
});

test('world map draws', async () => {
  // Building the world map takes a while on a slow machine: wait for it rather than for the network.
  await page().goto(t.base + '/index.html', { waitUntil: 'domcontentloaded' });
  await page().waitForFunction(() => document.getElementById('loadingMap')?.hidden || document.getElementById('loadingMap')?.textContent.includes('failed'), { timeout: 180000 });
  const failed = await page().$eval('#loadingMap', e => !e.hidden && e.textContent.includes('failed'));
  assert.equal(failed, false, 'the map loads (plain colours without the game textures)');
  const w = await t.api('/api/world');
  assert.equal(w.name, 'CITest');
  assert.equal(w.saveNumber, 2);
  noErrors();
});

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
  await openEditor();
  await page().evaluate(() => { localStorage.setItem('plantChosen', '["Bush01"]'); });
  await openEditor();
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

test('Select: an arrow moves along one axis, End drops onto the ground', async () => {
  await openEditor();
  const chest = (await records()).find(r => r.name === 'piece_chest_wood');
  await page().evaluate(id => { window.__ed.setTool('select'); window.__ed.selectIds([id]); }, chest.id);
  await lookAt(page(), chest.x, chest.z, 6, 8, 10); await sleep(600);
  const tip = (axis, f) => page().evaluate((axis, f) => {
    const ed = window.__ed, g = ed.scene.children.find(c => c.isGroup && c.children.length === 3 && c.renderOrder === 30);
    const d = { x: [1, 0, 0], y: [0, 1, 0], z: [0, 0, -1] }[axis];
    const v = g.position.clone().add(new ed.THREE.Vector3(...d).multiplyScalar(g.scale.x * f)).project(ed.camera);
    const r = ed.el.getBoundingClientRect(); return [r.left + (v.x + 1) / 2 * r.width, r.top + (1 - v.y) / 2 * r.height];
  }, axis, f);
  const [ax, ay] = await tip('x', 0.8), [bx, by] = await tip('x', 1.8);
  await page().mouse.move(ax, ay); await page().mouse.down(); await page().mouse.move(bx, by, { steps: 8 }); await page().mouse.up();
  await sleep(1500);
  const moved = (await records()).find(r => r.added && r.name === 'piece_chest_wood');
  assert.ok(moved, 'the moved chest is a new record');
  assert.ok(moved.x - chest.x > 0.3, `moved east (${(moved.x - chest.x).toFixed(2)} m)`);
  assert.ok(Math.abs(moved.z - chest.z) < 0.05, 'not moved north or south');
  // Lift it, then End: back onto the ground.
  await page().evaluate(id => window.__ed.selectIds([id]), moved.id);
  for (let i = 0; i < 8; i++) await page().keyboard.press('PageUp');
  await sleep(1500);
  await page().keyboard.press('End'); await sleep(2500);
  const dropped = await page().evaluate(() => { const ed = window.__ed, r = ed.objects.records.get([...ed.selection][0]); return { y: r.y, ground: ed.sampleHeight(r.x - ed.originX, r.z - ed.originZ) }; });
  assert.ok(Math.abs(dropped.y - dropped.ground) < 0.3, `on the ground after End (${dropped.y.toFixed(2)} vs ${dropped.ground.toFixed(2)})`);
  noErrors();
});

test('Select: a drawn zone selects what is inside', async () => {
  await openEditor();
  await page().evaluate(() => window.__ed.setTool('select'));
  const c = (await records()).find(r => r.name === 'piece_chest_wood');
  await lookAt(page(), c.x, c.z, 0, 70, 30); await sleep(500);
  await page().keyboard.down('Alt');
  const ring = []; for (let a = 0; a <= 360; a += 30) ring.push([c.x + 20 * Math.cos(a * Math.PI / 180), c.z + 20 * Math.sin(a * Math.PI / 180)]);
  { const [x, y] = await screenOf(page(), ...ring[0]); await page().mouse.move(x, y); await page().mouse.down(); for (const q of ring.slice(1)) { const [px, py] = await screenOf(page(), ...q); await page().mouse.move(px, py, { steps: 2 }); } await page().mouse.up(); }
  await page().keyboard.up('Alt'); await sleep(500);
  const n = await page().evaluate(() => window.__ed.selection.size);
  assert.ok(n >= 3, `selected ${n} objects`);
  noErrors();
});

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
  await page().click('#aSaveBp'); await sleep(1000);
  const { list } = await t.api('/api/blueprints');
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

test('inspector: a chest gets new contents and a text, undo puts the old one back', async () => {
  await openEditor();
  const chest = (await records()).find(r => r.name === 'piece_chest_wood' && !r.added);
  await page().evaluate(id => { window.__ed.setTool('select'); window.__ed.selectIds([id]); document.activeElement?.blur(); }, chest.id);
  await page().keyboard.press('i');
  await page().waitForFunction(() => !document.getElementById('inspPanel').hidden && document.getElementById('inTitle').textContent === 'piece_chest_wood');
  assert.match(await page().$eval('#inInv', e => e.textContent), /5 × 2 slots/);
  await page().click('#inAddItem');
  await page().$eval('#inInv .inItem[data-i] input[data-f="name"]', e => { e.value = 'Wood'; e.dispatchEvent(new Event('input')); });
  await page().$eval('#inInv .inItem[data-i] input[data-f="stack"]', e => { e.value = '30'; e.dispatchEvent(new Event('input')); });
  await page().select('#inAddSec', 'strings');
  await page().type('#inAddKey', 'text'); await page().type('#inAddVal', 'Hello');
  await page().click('#inAddBtn');
  await page().click('#inApply'); await sleep(1200);
  const newId = await page().evaluate(() => [...window.__ed.selection][0]);
  assert.ok(newId < 0, 'the edited chest is a new object');
  const d = await t.api(`/api/object/${newId}`);
  assert.deepEqual(d.inventory.items.map(i => [i.name, i.stack]), [['Wood', 30]]);
  assert.ok(d.fields.some(f => f.name === 'text' && f.value === 'Hello'));
  assert.ok(Math.abs(d.x - chest.x) < 0.01, 'same place');
  await page().click('#undo'); await sleep(1200);
  const back = (await records()).filter(r => r.name === 'piece_chest_wood');
  assert.ok(back.some(r => r.id === chest.id), 'the old chest is back');
  assert.ok(!back.some(r => r.id === newId), 'the edited copy is gone');
  noErrors();
});

test('search: the map finds a kind, and its link selects the object in 3D', async () => {
  await page().goto(t.base + '/index.html', { waitUntil: 'domcontentloaded' });
  await page().waitForSelector('#sQuery');
  await page().type('#sQuery', 'beech1');
  await page().click('#sGo');
  await page().waitForSelector('#sResults .hit');
  const count = await page().$eval('#sResults .counts b', e => +e.textContent.replace(/[^0-9]/g, ''));
  const api = await t.api('/api/search?q=beech1&what=kinds');
  assert.equal(count, api.counts.Beech1, 'the count matches the world');
  await page().click('#sResults .hit');
  const href = await page().$eval('#editLink', e => e.href);
  assert.match(href, /select=-?\d+/);
  const id = +href.match(/select=(-?\d+)/)[1];
  await page().goto(href, { waitUntil: 'networkidle0' });
  await page().waitForFunction(id => window.__ed?.selection?.has(id), {}, id);
  noErrors();
});

test('zone filter: zones across the world are marked for reset and unmarked', async () => {
  await page().goto(t.base + '/index.html', { waitUntil: 'domcontentloaded' });
  await page().waitForSelector('#zShow');
  // The test world's zones were filled by the editor (not generated by the game), next to its buildings.
  for (const id of ['#zOnlyGen', '#zNoBuild', '#zNoEdit']) await page().click(id);
  await page().click('#zShow');
  await page().waitForFunction(() => /zones match/.test(document.getElementById('zInfo').textContent));
  const matching = await page().$eval('#zInfo', e => +e.textContent.match(/^([\d,]+)/)[1].replace(/,/g, ''));
  const stats = await t.api('/api/zones/stats');
  let expected = 0;
  for (let i = 0; i < stats.data.length; i += stats.stride) if (stats.data[i + 3] === 0) expected++;
  assert.ok(matching > 0, `zones match (${matching})`);
  assert.equal(matching, expected, 'every zone without buildings, and only those');
  await page().click('#zMark');
  await page().waitForFunction(() => /marked for reset/.test(document.getElementById('zInfo').textContent));
  assert.equal((await t.api('/api/world')).resetZones, matching);
  assert.match(await page().$eval('#changesText', e => e.textContent), /zone\(s\) to reset/);
  await page().click('#zUnmark');
  await page().waitForFunction(() => !/marked for reset/.test(document.getElementById('zInfo').textContent));
  assert.equal((await t.api('/api/world')).resetZones, 0);
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
  await page().click('#aBkRestore'); await sleep(2000);
  const back = (await records()).find(r => r.name === 'Beech1' && Math.abs(r.x - tree.x) < 0.01 && Math.abs(r.z - tree.z) < 0.01);
  assert.ok(back?.added, 'the tree is back, as a restored object');
  assert.deepEqual((await records()).filter(outside).map(r => r.id).sort(), others, 'nothing outside the selection changed');
  noErrors();
});

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
  await page().evaluate(() => { const e = document.getElementById('bShape'); e.value = 'circle'; e.dispatchEvent(new Event('input')); });
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
  await page().click('[data-act="erode"]'); await sleep(1500);
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

test('magic select: a double click selects every connected piece, not the others', async () => {
  await openEditor();
  const c = (await records()).find(r => r.name === 'piece_chest_wood');
  const y = await page().evaluate(([x, z]) => { const ed = window.__ed; return ed.sampleHeight(x - ed.originX, z - ed.originZ); }, [c.x - 8, c.z - 12]);
  // A chain of three touching floors (stand-in boxes are 1.2 m wide), and one apart.
  const ids = await page().evaluate(async (c, y) => window.__ed.objects.add([0, 1, 2, 5].map(i => ({ name: 'wood_floor', x: c.x - 8 + i, y, z: c.z - 12, rx: 0, ry: 0, rz: 0, scale: 0 }))), c, y);
  await page().evaluate(() => window.__ed.setTool('select'));
  await lookAt(page(), c.x - 6, c.z - 12, 0, 12, 10); await sleep(500);
  const [x, yy] = await screenOf(page(), c.x - 8, c.z - 12, 0.6);
  await page().mouse.click(x, yy, { clickCount: 1 }); await page().mouse.click(x, yy, { clickCount: 2 }); await sleep(600);
  const sel = await page().evaluate(() => [...window.__ed.selection]);
  assert.deepEqual(sel.sort(), ids.slice(0, 3).sort(), 'the three touching floors, not the one apart');
  await page().evaluate(ids => window.__ed.setDeleted(ids, true), ids);
  noErrors();
});

test('selecting: same kind, invert, and a kept selection picked again', async () => {
  await openEditor();
  const tree = (await records()).find(r => r.name === 'Beech1');
  await page().evaluate(id => { window.__ed.setTool('select'); window.__ed.selectIds([id]); }, tree.id);
  await page().click('#selSame');
  const shownBeeches = await page().evaluate(() => [...window.__ed.objects.records.values()].filter(r => !r.deleted && r.name === 'Beech1' && window.__ed.entityOf(r.id)?.inst.some(({ im }) => im.visible && im.parent?.visible)).map(r => r.id).sort());
  const same = await page().evaluate(() => [...window.__ed.selection].sort());
  assert.deepEqual(same, shownBeeches, 'every shown beech');
  await page().click('#selInvert');
  const inverted = await page().evaluate(() => [...window.__ed.selection].map(id => window.__ed.objects.records.get(id).name));
  assert.ok(inverted.length > 0 && !inverted.includes('Beech1'), 'everything else');
  await page().evaluate(ids => window.__ed.selectIds(ids), same);
  await page().click('#selSaveBtn'); await sleep(300);
  await page().evaluate(() => window.__ed.clearSelection());
  await page().$eval('#selSaved', e => { e.value = '0'; e.dispatchEvent(new Event('change')); });
  await page().click('#selLoad');
  assert.deepEqual(await page().evaluate(() => [...window.__ed.selection].sort()), same, 'the kept selection comes back');
  await page().evaluate(() => window.__ed.clearSelection());
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

test('select: with "on the ground", a moved object lands on the ground', async () => {
  await openEditor();
  const tree = (await records()).find(r => r.name === 'Beech1' && !r.added);
  await page().evaluate(id => { window.__ed.setTool('select'); window.__ed.selectIds([id]); document.activeElement?.blur(); }, tree.id);
  await lookAt(page(), tree.x, tree.z, 6, 9, 12); await sleep(500);
  // Lifted 2 m with the switch off: it keeps that height.
  await page().$eval('#selGround', e => { e.checked = false; e.dispatchEvent(new Event('change')); });
  for (let i = 0; i < 8; i++) await page().keyboard.press('PageUp');
  await sleep(1500);
  const lifted = await page().evaluate(() => { const ed = window.__ed, r = ed.objects.records.get([...ed.selection][0]); return r.y - ed.sampleHeight(r.x - ed.originX, r.z - ed.originZ); });
  assert.ok(Math.abs(lifted - 2) < 0.1, `lifted 2 m (${lifted.toFixed(2)})`);
  // Switch on, then move it east with the red arrow: it lands on the ground.
  await page().$eval('#selGround', e => { e.checked = true; e.dispatchEvent(new Event('change')); });
  await frames(page());
  const tip = f => page().evaluate(f => {
    const ed = window.__ed, g = ed.scene.children.find(c => c.isGroup && c.children.length === 3 && c.renderOrder === 30);
    const v = g.position.clone().add(new ed.THREE.Vector3(1, 0, 0).multiplyScalar(g.scale.x * f)).project(ed.camera);
    const r = ed.el.getBoundingClientRect(); return [r.left + (v.x + 1) / 2 * r.width, r.top + (1 - v.y) / 2 * r.height];
  }, f);
  const [ax, ay] = await tip(0.8), [bx, by] = await tip(2);
  await page().mouse.move(ax, ay); await page().mouse.down(); await page().mouse.move(bx, by, { steps: 8 }); await page().mouse.up();
  await sleep(1500);
  const after = await page().evaluate(() => { const ed = window.__ed, r = ed.objects.records.get([...ed.selection][0]); return { y: r.y, ground: ed.sampleHeight(r.x - ed.originX, r.z - ed.originZ), x: r.x }; });
  assert.ok(after.x - tree.x > 0.2, 'it moved');
  assert.ok(Math.abs(after.y - after.ground) < 0.05, `on the ground (${after.y.toFixed(2)} vs ${after.ground.toFixed(2)})`);
  await page().evaluate(() => window.__ed.clearSelection());
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

test('Worlds goes back to the start page', async () => {
  await page().goto(t.base + '/index.html', { waitUntil: 'networkidle0' });
  await page().click('#worldsBtn');
  await page().waitForFunction(() => location.pathname === '/' && document.querySelector('.mode'), { timeout: 60000 });
  await waitPhase(t, 'start');
  noErrors();
});

test('placed kinds have stable prefab ids', () => {
  assert.equal(stableHash('_TerrainCompiler'), -367065113);
});

});
