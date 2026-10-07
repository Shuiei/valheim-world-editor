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
