// End-to-end, selecting, moving and inspecting objects: the real app on its own copy of the test world, as a user drives it.
import { describe, test } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { waitPhase, screenOf, lookAt, frames, sleep, stableHash } from './harness.mjs';
import { t, setup, page, noErrors, pending, openEditor, areaAction, applyArea, records } from './common.mjs';

setup({ open: true });

describe('Selecting, moving and inspecting objects', () => {

test('Select: an arrow moves along one axis, End drops onto the ground', async () => {
  await openEditor();
  const chest = (await records()).find(r => r.name === 'piece_chest_wood');
  await page().evaluate(id => { window.__ed.setTool('select'); window.__ed.selectIds([id]); }, chest.id);
  await lookAt(page(), chest.x, chest.z, 6, 8, 10); await sleep(600);
  const tip = (axis, f) => page().evaluate((axis, f) => {
    const ed = window.__ed, g = ed.scene.children.find(c => c.isGroup && c.children.length >= 3 && c.renderOrder === 30);
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
    const ed = window.__ed, g = ed.scene.children.find(c => c.isGroup && c.children.length >= 3 && c.renderOrder === 30);
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

test('transform: typed place and turn, snapping walls together, and the turning ring', async () => {
  await openEditor();
  const c = (await records()).find(r => r.name === 'piece_chest_wood');
  const y0 = await page().evaluate(([x, z]) => { const ed = window.__ed; return ed.sampleHeight(x - ed.originX, z - ed.originZ); }, [c.x + 12, c.z - 30]);
  // Two walls (2 m between their snap points), the second 0.3 m too far east.
  const [a, b] = await page().evaluate(async (c, y) => window.__ed.objects.add([0, 2.3].map(dx => ({ name: 'woodwall', x: c.x + 12 + dx, y: y + 1, z: c.z - 30, rx: 0, ry: 0, rz: 0, scale: 0 }))), c, y0);
  const rec = id => page().evaluate(id => { const r = window.__ed.objects.records.get(id); return r && { id: r.id, x: r.x, y: r.y, z: r.z, ry: r.ry }; }, id);
  const sel = () => page().evaluate(() => [...window.__ed.selection][0]);
  await page().evaluate(id => { window.__ed.setTool('select'); window.__ed.selectIds([id]); document.activeElement?.blur(); }, b);
  await lookAt(page(), c.x + 13, c.z - 30, 4, 7, 9); await sleep(600);
  // Drag the red arrow a little west: within 0.75 m the walls' snap points meet.
  const tip = f => page().evaluate(f => {
    const ed = window.__ed, g = ed.scene.children.find(o => o.isGroup && o.children.length >= 3 && o.renderOrder === 30);
    const v = g.position.clone().add(new ed.THREE.Vector3(1, 0, 0).multiplyScalar(g.scale.x * f)).project(ed.camera);
    const r = ed.el.getBoundingClientRect(); return [r.left + (v.x + 1) / 2 * r.width, r.top + (1 - v.y) / 2 * r.height];
  }, f);
  const [p0x, p0y] = await tip(1.0), [p1x, p1y] = await tip(0.85);
  await page().mouse.move(p0x, p0y); await page().mouse.down(); await page().mouse.move(p1x, p1y, { steps: 5 }); await page().mouse.up();
  await sleep(1500);
  const A = await rec(a), B = await rec(await sel());
  assert.ok(Math.abs(B.x - A.x - 2) < 0.01, `the walls' ends meet (${(B.x - A.x).toFixed(3)} m apart)`);
  assert.ok(Math.abs(B.y - A.y) < 0.01, 'at the same height');
  // Typed: 3 m north and a quarter turn.
  await page().evaluate(() => { const s = (id, v) => { const e = document.getElementById(id); e.value = v; }; s('nZ', (+document.getElementById('nZ').value + 3).toFixed(2)); s('nT', 90); });
  await page().click('#nApply'); await sleep(1500);
  const T = await rec(await sel());
  assert.ok(Math.abs(T.z - B.z - 3) < 0.01, `moved 3 m north (${(T.z - B.z).toFixed(3)})`);
  assert.ok(Math.abs(((T.ry - 90) % 360 + 360) % 360) < 0.6, `turned to 90° (${T.ry})`);
  // The ring, dragged a quarter round with Ctrl: a multiple of 15°.
  await frames(page());
  const ringAt = deg => page().evaluate(deg => {
    const ed = window.__ed, g = ed.scene.children.find(o => o.isGroup && o.children.length >= 3 && o.renderOrder === 30), t = deg * Math.PI / 180;
    const v = g.position.clone().add(new ed.THREE.Vector3(Math.sin(t), 0, -Math.cos(t)).multiplyScalar(g.scale.x * 1.45)).project(ed.camera);
    const r = ed.el.getBoundingClientRect(); return [r.left + (v.x + 1) / 2 * r.width, r.top + (1 - v.y) / 2 * r.height];
  }, deg);
  const [r0x, r0y] = await ringAt(180), [r1x, r1y] = await ringAt(250);
  await page().keyboard.down('Control');
  await page().mouse.move(r0x, r0y); await page().mouse.down(); await page().mouse.move(r1x, r1y, { steps: 10 }); await page().mouse.up();
  await page().keyboard.up('Control');
  await sleep(1500);
  const R = await rec(await sel());
  const turned = ((R.ry - T.ry) % 360 + 360) % 360;
  assert.ok(turned > 10 && Math.abs(turned / 15 - Math.round(turned / 15)) < 0.05, `turned by the ring in 15° steps (${turned.toFixed(1)}°)`);
  await page().evaluate(ids => window.__ed.setDeleted(ids, true), [a, R.id]);
  noErrors();
});

test('select: a moved building keeps its shape, its base on the ground; a tree lands on its own', async () => {
  await openEditor();
  const c = (await records()).find(r => r.name === 'piece_chest_wood');
  const P = [c.x - 30, c.z - 34];
  const y0 = await page().evaluate(([x, z]) => { const ed = window.__ed; return ed.sampleHeight(x - ed.originX, z - ed.originZ) + 0.5; }, P);
  // A floor, a wall on it and a second wall on top (walls: origin 1 m above their bottom), and a tree 4 m away.
  const ids = await page().evaluate(async (P, y) => window.__ed.objects.add([
    { name: 'wood_floor', x: P[0], y, z: P[1] }, { name: 'woodwall', x: P[0], y: y + 1, z: P[1] + 1 }, { name: 'woodwall', x: P[0], y: y + 3, z: P[1] + 1 },
    { name: 'Beech1', x: P[0] + 4, y: y + 2, z: P[1] }].map(o => ({ ...o, rx: 0, ry: 0, rz: 0, scale: 0 }))), P, y0);
  await page().evaluate(ids => { const ed = window.__ed; ed.setTool('select'); ed.selectIds(ids); document.activeElement?.blur();
    const sn = document.getElementById('selSnapTo'); sn.checked = false; sn.dispatchEvent(new Event('change'));
    const g = document.getElementById('selGround'); g.checked = true; g.dispatchEvent(new Event('change')); }, ids);
  await lookAt(page(), P[0] + 2, P[1], 6, 12, 16); await sleep(600);
  const tip = f => page().evaluate(f => {
    const ed = window.__ed, g = ed.scene.children.find(o => o.isGroup && o.children.length >= 3 && o.renderOrder === 30);
    const v = g.position.clone().add(new ed.THREE.Vector3(1, 0, 0).multiplyScalar(g.scale.x * f)).project(ed.camera);
    const r = ed.el.getBoundingClientRect(); return [r.left + (v.x + 1) / 2 * r.width, r.top + (1 - v.y) / 2 * r.height];
  }, f);
  const [ax, ay] = await tip(0.8), [bx, by] = await tip(2.2);
  await page().mouse.move(ax, ay); await page().mouse.down(); await page().mouse.move(bx, by, { steps: 8 }); await page().mouse.up();
  await sleep(1500);
  const moved = await page().evaluate(() => { const ed = window.__ed; return [...ed.selection].map(id => { const r = ed.objects.records.get(id); return { name: r.name, x: r.x, y: r.y, z: r.z, ground: ed.sampleHeight(r.x - ed.originX, r.z - ed.originZ) }; }); });
  const floor = moved.find(m => m.name === 'wood_floor'), walls = moved.filter(m => m.name === 'woodwall').sort((a, b) => a.y - b.y), tree = moved.find(m => m.name === 'Beech1');
  assert.ok(floor.x - P[0] > 0.3, 'it moved');
  assert.ok(Math.abs(walls[1].y - walls[0].y - 2) < 0.01 && Math.abs(walls[0].y - floor.y - 1) < 0.01, 'the building keeps its shape');
  // Bottom layer: the floor (bottom at its origin) and the lower wall (bottom 1 m below its origin).
  const gaps = [floor.y - floor.ground, walls[0].y - 1 - walls[0].ground];
  assert.ok(Math.max(...gaps) < 0.02 && Math.max(...gaps) > -2, `its base sits on the ground, nothing floats (${gaps.map(g => g.toFixed(2))})`);
  assert.ok(Math.abs(tree.y - tree.ground) < 0.02, 'the tree lands on the ground by itself');
  await page().evaluate(() => { const ed = window.__ed; ed.setDeleted([...ed.selection], true); const sn = document.getElementById('selSnapTo'); sn.checked = true; sn.dispatchEvent(new Event('change')); });
  noErrors();
});

test('placed pieces are player built, and Make player built fixes old ones', async () => {
  await openEditor();
  await page().evaluate(() => { localStorage.setItem('plantChosen', '["woodwall"]'); localStorage.removeItem('builder:' + window.__ed.world.name); });
  await openEditor();
  const creator = async id => (await t.api(`/api/object/${id}`)).fields.find(f => f.name === 'creator')?.value ?? '0';
  // Built by: chosen at the start, here a player who built in the test world or this computer's character.
  await page().waitForFunction(() => document.querySelectorAll('#builder option').length > 1);
  const builder = await page().$eval('#builder', e => e.value);
  assert.match(builder, /^-?[1-9]\d*$/, `a builder is chosen (${builder})`);
  // A wall placed with the Plant tool gets it.
  const c = (await records()).find(r => r.name === 'piece_chest_wood');
  await lookAt(page(), c.x, c.z - 25, 0, 40, 25);
  await page().keyboard.press('t'); await sleep(300);
  await page().click('#plModes [data-m="line"]');
  await page().click('#plLineShape [data-ls="points"]');
  for (const [dx, dz] of [[-3, -25], [3, -25]]) { const [x, y] = await screenOf(page(), c.x + dx, c.z + dz); await page().mouse.click(x, y); await sleep(150); }
  const known = new Set((await records()).map(r => r.id));
  await page().keyboard.press('Enter'); await sleep(1000);
  const walls = (await records()).filter(r => !known.has(r.id) && r.name === 'woodwall');
  assert.ok(walls.length >= 1, 'walls placed');
  assert.equal(await creator(walls[0].id), builder);
  // Another id for the builder; the test chest has none, Make player built gives it the new one.
  assert.equal(await creator(c.id), '0');
  await page().keyboard.press('Escape');
  await page().evaluate(() => { window.__ed.setTool('select'); });
  await page().evaluate(id => window.__ed.selectIds([id]), c.id);
  await page().evaluate(() => { window.prompt = () => '4242424242'; const s = document.getElementById('builder'); s.value = 'other'; s.dispatchEvent(new Event('change')); });
  await page().waitForFunction(() => document.getElementById('builder').value === '4242424242');
  await page().click('#selClaim'); await sleep(800);
  const sel = await page().evaluate(() => [...window.__ed.selection]);
  assert.equal(sel.length, 1); assert.ok(sel[0] < 0, 'the chest was replaced by a changed copy');
  assert.equal(await creator(sel[0]), '4242424242');
  await page().click('#undo'); await sleep(800);
  await page().click('#undo'); await sleep(800);
  // Nobody: new pieces get no builder.
  await page().evaluate(() => { const s = document.getElementById('builder'); s.value = '0'; s.dispatchEvent(new Event('change')); });
  await sleep(300);
  assert.equal((await t.api('/api/builders')).builder, '0');
  await page().evaluate(() => localStorage.removeItem('builder:' + window.__ed.world.name));
  noErrors();
});

});
