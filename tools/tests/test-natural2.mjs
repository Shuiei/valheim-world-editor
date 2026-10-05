import puppeteer from 'puppeteer';
const base = process.argv[2], out = process.argv[3];
const browser = await puppeteer.launch({ headless: true, args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--no-sandbox'] });
const page = await browser.newPage();
await page.setViewport({ width: 1400, height: 850 });
const errors = []; page.on('pageerror', e => errors.push(e.message));
async function open() {
  await page.goto(`${base}/editor.html?zx=0&zz=-5&size=3`, { waitUntil: 'networkidle0' });
  await page.waitForFunction(() => !document.getElementById('loading'), { timeout: 60000 });
  await new Promise(r => setTimeout(r, 600));
}
await open();
// Plain path first (for comparison), then the same line with natural look, side by side.
await page.keyboard.press('p'); await page.select('#pAction', 'flatten');
await page.evaluate(() => { const h = document.getElementById('pHeight'); h.value = 32; h.dispatchEvent(new Event('input')); });
for (const [x, y] of [[470, 640], [500, 720], [560, 790]]) { await page.mouse.click(x, y); await new Promise(r => setTimeout(r, 100)); }
await page.keyboard.press('Enter'); await new Promise(r => setTimeout(r, 800)); await page.keyboard.press('Escape');
await page.evaluate(() => { document.getElementById('pNatural').checked = true; });
for (const [x, y] of [[760, 700], [800, 760], [880, 800]]) { await page.mouse.click(x, y); await new Promise(r => setTimeout(r, 100)); }
await page.keyboard.press('Enter'); await new Promise(r => setTimeout(r, 800)); await page.keyboard.press('Escape');
await page.keyboard.press('h');
await page.mouse.move(700, 720);
for (let i = 0; i < 6; i++) { await page.mouse.wheel({ deltaY: -300 }); await new Promise(r => setTimeout(r, 120)); }
await new Promise(r => setTimeout(r, 600));
await page.screenshot({ path: `${out}/natural-paths.png` });
console.log(JSON.stringify({ errors }));
await browser.close();
