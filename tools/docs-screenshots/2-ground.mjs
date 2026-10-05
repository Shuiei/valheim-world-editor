import { launch, editor, look, screen, shot, W, hideView } from './lib.mjs';
const [base, dir] = process.argv.slice(2);
const browser = await launch();
const p = await editor(browser, base, 'zx=-1&zz=-6&size=3', { viewFolded: '["Look"]', editorShow2: JSON.stringify({ trees: false, rocks: true, buildings: true, water: true, borders: true }) });
await hideView(p);
const X = -128, Z = -384;
await look(p, X, Z, 0, 45, 38); await W(800);
const key = k => p.keyboard.press(k);
const stroke = async (pts, ms = 25) => { const [a, b] = await screen(p, ...pts[0]); await p.mouse.move(a, b); await p.mouse.down(); for (const q of pts.slice(1)) { const [x, y] = await screen(p, ...q); await p.mouse.move(x, y, { steps: 3 }); await W(ms); } await p.mouse.up(); await W(400); };
// Sculpt: raise a mound, flatten a pad next to it
await key('1'); await p.$eval('#radius', e => { e.value = 7; e.dispatchEvent(new Event('input')); }); await p.$eval('#strength', e => { e.value = 0.45; e.dispatchEvent(new Event('input')); });
for (let i = 0; i < 1; i++) await stroke([[X - 8, Z], [X - 7, Z + 1], [X - 8, Z + 2], [X - 9, Z + 1], [X - 8, Z]], 60);
await key('3');
await stroke([[X + 8, Z], [X + 12, Z], [X + 12, Z + 4], [X + 8, Z + 4], [X + 8, Z]], 80);
const [hx, hy] = await screen(p, X + 10, Z - 6); await p.mouse.move(hx, hy); await W(400);
await shot(p, dir, 'sculpt'); console.error('sculpt');
// Paint
await key('8'); await p.$eval('#radius', e => { e.value = 3; e.dispatchEvent(new Event('input')); });
await stroke([[X - 14, Z - 10], [X - 6, Z - 10], [X + 2, Z - 9], [X + 10, Z - 10], [X + 18, Z - 9]], 70);
await key('6');
await stroke([[X - 14, Z - 16], [X - 4, Z - 15], [X + 6, Z - 17], [X + 18, Z - 15]], 70);
await key('7');
await stroke([[X - 12, Z + 12], [X - 4, Z + 12], [X - 4, Z + 16], [X - 12, Z + 16], [X - 12, Z + 12]], 70);
const [px, py] = await screen(p, X + 4, Z + 14); await p.mouse.move(px, py); await W(400);
await shot(p, dir, 'paint');
// Path
await key('p'); await W(200);
for (const q of [[X - 20, Z + 22], [X - 5, Z + 30], [X + 10, Z + 24], [X + 22, Z + 32]]) { const [x, y] = await screen(p, ...q); await p.mouse.click(x, y); await W(150); }
await p.$eval('#pAction', e => { e.value = 'paint-paved'; e.dispatchEvent(new Event('change')); e.dispatchEvent(new Event('input')); });
await look(p, X, Z + 25, 0, 40, 36); await W(800);
await shot(p, dir, 'path');
await p.keyboard.press('Escape');
// Mask
await key('1'); await look(p, X, Z, 0, 45, 38); await W(400);
await p.$eval('#mOn', e => { e.checked = true; e.dispatchEvent(new Event('input')); });
await p.click('#mBiomes [data-b="1"]');
await p.$eval('#mHmax', e => { e.value = 64; e.dispatchEvent(new Event('input')); });
await p.$eval('#mSmax', e => { e.value = 25; e.dispatchEvent(new Event('input')); });
const [mx, my] = await screen(p, X, Z); await p.mouse.move(mx, my); await W(400);
await shot(p, dir, 'mask');
await p.$eval('#mOn', e => { e.checked = false; e.dispatchEvent(new Event('input')); });
// Area box + panel
await key('b'); await W(200);
{ const [a, b] = await screen(p, X - 16, Z - 4); const [c, d] = await screen(p, X + 6, Z + 14); await p.mouse.move(a, b); await p.mouse.down(); await p.mouse.move(c, d, { steps: 12 }); await p.mouse.up(); await W(400); }
await shot(p, dir, 'area');
// Copy and paste preview
await p.keyboard.down('Control'); await p.keyboard.press('c'); await p.keyboard.press('v'); await p.keyboard.up('Control'); await W(300);
for (let i = 0; i < 20; i++) await p.keyboard.press('.');
{ const [a, b] = await screen(p, X + 22, Z + 6); await p.mouse.move(a, b); await W(500); }
await shot(p, dir, 'paste');
console.log(JSON.stringify(p.errors));
await browser.close();
