import puppeteer from 'puppeteer';
const base = process.argv[2], out = process.argv[3];
const browser = await puppeteer.launch({ headless: true, args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--no-sandbox'] });
const page = await browser.newPage();
await page.setViewport({ width: 1400, height: 850 });
const errors = []; page.on('pageerror', e => errors.push(e.message)); page.on('console', m => { if (m.type() === 'error') errors.push(m.text()); });
await page.goto(`${base}/editor.html?zx=0&zz=-5&size=3`, { waitUntil: 'networkidle0' });
await page.waitForFunction(() => !document.getElementById('loading'), { timeout: 60000 });
await new Promise(r => setTimeout(r, 800));
// Natural breakwater.
await page.keyboard.press('p');
await page.select('#pAction', 'flatten');
await page.evaluate(() => { const h = document.getElementById('pHeight'); h.value = 32; h.dispatchEvent(new Event('input')); document.getElementById('pNatural').checked = true; });
for (const [x, y] of [[560, 600], [600, 680], [680, 740], [790, 760]]) { await page.mouse.click(x, y); await new Promise(r => setTimeout(r, 120)); }
await page.keyboard.press('Enter'); await new Promise(r => setTimeout(r, 1200));
const msg1 = await page.$eval('#sMsg', e => e.textContent);
await page.keyboard.press('Escape');
// A plain flattened square, then Naturalize over half of it.
await page.keyboard.press('3');
await page.evaluate(() => { const t = document.getElementById('target'); t.value = 38; t.dispatchEvent(new Event('input')); const r = document.getElementById('radius'); r.value = 14; r.dispatchEvent(new Event('input')); const s = document.getElementById('strength'); s.value = 1; s.dispatchEvent(new Event('input')); });
await page.mouse.move(930, 330); await page.mouse.down(); await new Promise(r => setTimeout(r, 1500)); await page.mouse.up();
await page.keyboard.press('0');
await page.mouse.move(900, 330); await page.mouse.down();
for (let i = 0; i < 30; i++) { await page.mouse.move(900 + (i % 10) * 6, 330 + Math.floor(i / 10) * 8); await new Promise(r => setTimeout(r, 60)); }
await page.mouse.up(); await new Promise(r => setTimeout(r, 800));
await page.mouse.move(10, 10);
await new Promise(r => setTimeout(r, 400));
await page.screenshot({ path: `${out}/natural.png` });
console.log(JSON.stringify({ msg1, errors }, null, 1));
await browser.close();
