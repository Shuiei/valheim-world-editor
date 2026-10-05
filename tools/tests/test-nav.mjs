import puppeteer from 'puppeteer';
const base = process.argv[2], out = process.argv[3];
const browser = await puppeteer.launch({ headless: true, args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--no-sandbox'] });
const page = await browser.newPage();
await page.setViewport({ width: 1400, height: 850 });
const errors = [];
page.on('pageerror', e => errors.push('pageerror: ' + e.message));
page.on('console', m => { if (m.type() === 'error') errors.push(m.text()); });
for (const [name, x, z, mpp] of [['bld-close', 20, -330, 0.35], ['bld-mid', 20, -330, 3]]) {
  await page.goto(`${base}/`, { waitUntil: 'domcontentloaded' });
  await page.evaluate((v) => localStorage.setItem('mapView', JSON.stringify(v)), { x, z, mpp });
  await page.reload({ waitUntil: 'networkidle0' });
  await page.waitForFunction(() => document.getElementById('loadingMap')?.hidden === true, { timeout: 120000 });
  await new Promise(r => setTimeout(r, 6000));
  await page.screenshot({ path: `${out}/map-${name}.png` });
}
const label = await page.$eval('#showBuildings', e => e.parentElement.textContent.trim());
// 3D editor navigation.
await page.goto(`${base}/editor.html?zx=0&zz=-5&size=3`, { waitUntil: 'networkidle0' });
await page.waitForFunction(() => !document.getElementById('loading'), { timeout: 60000 });
await new Promise(r => setTimeout(r, 800));
const status = async () => { await page.mouse.move(700, 470); await new Promise(r => setTimeout(r, 250)); return page.$eval('#sPos', e => e.textContent); };
const s0 = await status();
await page.keyboard.press('h');
await page.mouse.move(700, 470); await page.mouse.down();
for (let i = 1; i <= 10; i++) { await page.mouse.move(700 - i * 25, 470 + i * 5); await new Promise(r => setTimeout(r, 30)); }
await page.mouse.up(); await new Promise(r => setTimeout(r, 400));
const s1 = await status();
await page.keyboard.down('KeyW'); await new Promise(r => setTimeout(r, 700)); await page.keyboard.up('KeyW');
await new Promise(r => setTimeout(r, 300));
const s2 = await status();
const changed = (await (await fetch(`${base}/api/world`)).json()).changedZones;
await page.screenshot({ path: `${out}/editor-moved.png` });
await Promise.all([page.waitForNavigation({ waitUntil: 'networkidle0' }), page.click('[data-shift="1,0"]')]);
await page.waitForFunction(() => !document.getElementById('loading'), { timeout: 60000 });
const title = await page.$eval('#title', e => e.textContent);
console.log(JSON.stringify({ label, before: s0, afterMoveTool: s1, afterW: s2, terrainChangedByPanning: changed, afterShiftEast: { url: page.url().split('?')[1], title }, errors }, null, 1));
await browser.close();
