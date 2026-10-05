import { launch, editor, look, screen, shot, W, hideView } from './lib.mjs';
const [base, dir] = process.argv.slice(2);
const browser = await launch();
const p = await editor(browser, base, 'zx=0&zz=-5&size=3', { viewFolded: '[]' });
await hideView(p);
const at = async (x, z, l = 0) => { const [a, b] = await screen(p, x, z, l); await p.mouse.move(a, b); await W(500); };
// Select one chest with the arrows
const chest = await p.evaluate(() => { const ed = window.__ed; const r = [...ed.objects.records.values()].find(r => r.name === 'piece_chest_wood'); ed.setTool('select'); ed.selectIds([r.id]); return { x: r.x, z: r.z }; });
await look(p, chest.x, chest.z, 4, 6, 7); await W(1200);
await shot(p, dir, 'select-arrows'); console.error('arrows');
// Zone selection around the boats / docks
await p.evaluate(() => window.__ed.clearSelection());
await look(p, 29, -366, 40, 45, 55); await W(800);
await p.keyboard.down('Alt');
const ring = []; for (let a = 0; a <= 360; a += 30) ring.push([20 + 14 * Math.cos(a * Math.PI / 180), -345 + 10 * Math.sin(a * Math.PI / 180)]);
{ const [a, b] = await screen(p, ...ring[0]); await p.mouse.move(a, b); await p.mouse.down(); for (const q of ring.slice(1)) { const [x, y] = await screen(p, ...q); await p.mouse.move(x, y, { steps: 2 }); } }
await shot(p, dir, 'select-zone-drawing');
await p.mouse.up(); await p.keyboard.up('Alt'); await W(800);
await shot(p, dir, 'select-zone'); console.error('zone');
await p.evaluate(() => window.__ed.clearSelection());
// Measure + slope colours
await p.evaluate(() => { document.getElementById('ovSlope').checked = true; document.getElementById('ovSlope').dispatchEvent(new Event('change')); });
await p.keyboard.press('m'); await W(200);
await look(p, 60, -380, 30, 60, 60); await W(800);
{ const [a, b] = await screen(p, 40, -360); await p.mouse.click(a, b); await W(300); const [c, d] = await screen(p, 85, -400); await p.mouse.click(c, d); await W(500); }
await shot(p, dir, 'measure'); console.error('measure');
await p.evaluate(() => { document.getElementById('ovSlope').checked = false; document.getElementById('ovSlope').dispatchEvent(new Event('change')); });
// History: a few changes, then the panel
await p.keyboard.press('Escape'); await p.keyboard.press('1');
await look(p, 60, -380, 0, 50, 40); await W(500);
for (const [x, z] of [[50, -380], [70, -385]]) { await at(x, z); await p.mouse.down(); await at(x + 1, z + 1); await W(400); await p.mouse.up(); await W(500); }
await p.keyboard.press('7'); await at(60, -370); await p.mouse.down(); await at(62, -371); await W(500); await p.mouse.up(); await W(500);
await p.keyboard.press('l'); await W(500);
await shot(p, dir, 'history'); console.error('history');
// Help
await p.keyboard.press('?'); await W(400); await shot(p, dir, 'help');
console.log(JSON.stringify(p.errors));
await browser.close();
