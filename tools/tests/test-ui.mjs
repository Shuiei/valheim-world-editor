import puppeteer from 'puppeteer';
const [base, out] = process.argv.slice(2);
const browser = await puppeteer.launch({ headless: true, args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--no-sandbox'] });
const page = await browser.newPage();
await page.setViewport({ width: 1500, height: 900 });
const errors = []; page.on('pageerror', e => errors.push(e.message)); page.on('console', m => { if (['error', 'warning'].includes(m.type())) errors.push(m.text().slice(0, 400)); });
page.on('dialog', d => d.dismiss());
await page.goto(`${base}/editor.html?zx=-9&zz=-6&size=3`, { waitUntil: 'networkidle0' });
await page.waitForFunction(() => document.getElementById('lookState')?.textContent === '' && document.querySelector('[data-count="trees"]').textContent !== '', { timeout: 120000 });
await page.evaluate(() => window.__view(0, 105, 60, 0, 66, 0));
await new Promise(r => setTimeout(r, 2500));
await page.screenshot({ path: `${out}/ui-1.png` });
await page.keyboard.press('3');
await new Promise(r => setTimeout(r, 300));
await page.screenshot({ path: `${out}/ui-flatten.png`, clip: { x: 0, y: 0, width: 420, height: 500 } });
await page.keyboard.press('e');
// Click around the centre until something is selected.
let info = '';
for (const [x, y] of [[750, 450], [700, 420], [800, 500], [650, 480], [850, 400], [750, 550]]) {
  await page.mouse.click(x, y);
  await new Promise(r => setTimeout(r, 400));
  info = await page.$eval('#selInfo', e => e.textContent);
  if (!info.startsWith('Nothing')) break;
}
await page.screenshot({ path: `${out}/ui-select.png` });
await page.keyboard.press('Delete');
await new Promise(r => setTimeout(r, 800));
const pending = await page.$eval('#pending', e => e.textContent);
await page.screenshot({ path: `${out}/ui-deleted.png` });
await page.keyboard.down('Control'); await page.keyboard.press('z'); await page.keyboard.up('Control');
await new Promise(r => setTimeout(r, 800));
const pendingAfterUndo = await page.$eval('#pending', e => e.textContent);
await page.keyboard.press('?');
await new Promise(r => setTimeout(r, 300));
await page.screenshot({ path: `${out}/ui-help.png` });
console.log(JSON.stringify({ info, pending, pendingAfterUndo, errors }));
await browser.close();
