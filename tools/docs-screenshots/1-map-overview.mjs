import { launch, editor, look, shot, W, hideView } from './lib.mjs';
const [base, dir] = process.argv.slice(2);
const browser = await launch();
// Map page
const m = await browser.newPage(); await m.setViewport({ width: 1600, height: 950 });
await m.goto(`${base}/index.html`, { waitUntil: 'networkidle0', timeout: 120000 }); await W(3000);
await shot(m, dir, 'map');
// Editor overview at the base
const p = await editor(browser, base, 'zx=0&zz=-5&size=3');
await look(p, 29, -366, 40, 45, 55); await W(1500);
await shot(p, dir, 'overview');
console.log(JSON.stringify(p.errors));
await browser.close();
