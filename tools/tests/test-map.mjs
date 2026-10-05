import puppeteer from 'puppeteer';
const base = process.argv[2], out = process.argv[3];
const browser = await puppeteer.launch({ headless: true, args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--no-sandbox'] });
const page = await browser.newPage();
await page.setViewport({ width: 1400, height: 850 });
const errors = [];
page.on('console', m => { if (m.type() === 'error' || m.type() === 'warning') errors.push(`${m.type()}: ${m.text()}`); });
page.on('pageerror', e => errors.push('pageerror: ' + e.message));
const shots = [['world', 0, 0, 14], ['region', 0, -300, 2.5], ['base', 20, -330, 0.4]];
for (const [name, x, z, mpp] of shots) {
  await page.goto(`${base}/`, { waitUntil: 'domcontentloaded' });
  await page.evaluate((v) => localStorage.setItem('mapView', JSON.stringify(v)), { x, z, mpp });
  await page.reload({ waitUntil: 'networkidle0' });
  await page.waitForFunction(() => document.getElementById('loadingMap')?.hidden === true, { timeout: 120000 });
  await new Promise(r => setTimeout(r, mpp < 6 ? 6000 : 1500));
  await page.screenshot({ path: `${out}/map-${name}.png` });
}
console.log(JSON.stringify({ errors: [...new Set(errors)] }, null, 1));
await browser.close();
