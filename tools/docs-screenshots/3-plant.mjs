import { launch, editor, look, screen, shot, W, hideView } from './lib.mjs';
const [base, dir] = process.argv.slice(2);
const browser = await launch();
const X = -128, Z = -384;
const prefs = { viewFolded: '["Look"]', editorShow2: JSON.stringify({ trees: false, rocks: true, buildings: true, water: true, borders: true }), plantChosen: '["RaspberryBush","BlueberryBush","Bush01"]', plantOpenKinds: '["bushes"]' };
const p = await editor(browser, base, 'zx=-1&zz=-6&size=3', prefs);
await hideView(p);
const at = async (x, z) => { const [a, b] = await screen(p, x, z); await p.mouse.move(a, b); await W(500); };
const click = async (x, z) => { const [a, b] = await screen(p, x, z); await p.mouse.click(a, b); await W(250); };
const set = (id, v) => p.$eval('#' + id, (e, v) => { e.value = v; e.dispatchEvent(new Event('input')); }, v);
await look(p, X, Z, 0, 34, 30); await W(800);
await p.keyboard.press('t'); await W(300);
await p.$eval('#radius', e => { e.value = 9; e.dispatchEvent(new Event('input')); }); await set('plDensity', 2.4);
await p.click('#plKindsBtn'); await W(300);
await at(X, Z); await shot(p, dir, 'plant-brush'); console.error('brush');
await p.click('#plDrawerClose');
// Line
await p.click('#plModes [data-m="line"]'); await set('plEvery', 2.5); await p.click('#plAlong');
for (const q of [[X - 20, Z + 6], [X - 7, Z - 3], [X + 7, Z + 5], [X + 20, Z - 4]]) await click(...q);
await at(X + 22, Z + 14); await shot(p, dir, 'plant-line'); console.error('line');
await p.keyboard.press('Escape');
// Grid
await p.click('#plModes [data-m="grid"]'); await set('plCell', 3);
{ const [a, b] = await screen(p, X - 14, Z - 8); const [c, d] = await screen(p, X + 14, Z + 10); await p.mouse.move(a, b); await W(200); await p.mouse.down(); await W(100); await p.mouse.move((a + c) / 2, (b + d) / 2, { steps: 3 }); await p.mouse.move(c, d, { steps: 3 }); await W(100); await p.mouse.up(); await W(400); }
await p.keyboard.down('Shift'); await p.keyboard.press('>'); await p.keyboard.up('Shift'); await W(300);
await at(X + 22, Z + 14); await shot(p, dir, 'plant-grid'); console.error('grid');
await p.keyboard.press('Escape');
// Zone
await p.click('#plModes [data-m="zone"]');
for (let a = 0; a < 360; a += 30) await click(X + Math.cos(a * Math.PI / 180) * (16 + (a % 60 ? 4 : 0)), Z + Math.sin(a * Math.PI / 180) * 11);
await at(X + 22, Z + 14); await shot(p, dir, 'plant-zone'); console.error('zone');
console.log(JSON.stringify(p.errors));
await browser.close();
