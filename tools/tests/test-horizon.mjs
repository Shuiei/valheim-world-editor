import puppeteer from 'puppeteer';
const base = process.argv[2], out = process.argv[3], q = process.argv[4], tag = process.argv[5];
const browser = await puppeteer.launch({ headless: true, args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--no-sandbox'] });
const page = await browser.newPage();
await page.setViewport({ width: 1400, height: 850 });
const errors = []; page.on('pageerror', e => errors.push(e.message)); page.on('console', m => { if (['error', 'warning'].includes(m.type())) errors.push(m.text().slice(0, 800)); });
await page.goto(`${base}/editor.html?${q}`, { waitUntil: 'networkidle0' });
await page.waitForFunction(() => document.getElementById('lookState')?.textContent === '', { timeout: 90000 });
await new Promise(r => setTimeout(r, 3000));
// Zoom in, then tilt the camera towards the horizon with a right drag.
await page.mouse.move(800, 450);
for (let i = 0; i < 5; i++) { await page.mouse.wheel({ deltaY: -250 }); await new Promise(r => setTimeout(r, 150)); }
await page.mouse.down({ button: 'right' });
for (let i = 0; i < 20; i++) { await page.mouse.move(800 + i * 6, 450 - i * 12); await new Promise(r => setTimeout(r, 30)); }
await page.mouse.up({ button: 'right' });
await new Promise(r => setTimeout(r, 3000));
await page.screenshot({ path: `${out}/${tag}.png` });
console.log(JSON.stringify({ errors }));
await browser.close();
