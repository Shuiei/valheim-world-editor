import puppeteer from 'puppeteer';
const [base, out, q, tag, ...views] = process.argv.slice(2);
const browser = await puppeteer.launch({ headless: true, args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--no-sandbox'] });
const page = await browser.newPage();
await page.setViewport({ width: 1400, height: 850 });
const errors = []; page.on('response', r => { if (r.status() >= 400) errors.push(r.url()); }); page.on('pageerror', e => errors.push(e.message)); page.on('console', m => { if (['error', 'warning'].includes(m.type())) errors.push(m.text().slice(0, 600)); });
await page.goto(`${base}/editor.html?${q}`, { waitUntil: 'networkidle0' });
await page.waitForFunction(() => document.getElementById('lookState')?.textContent === '' && document.getElementById('vCount')?.textContent !== '', { timeout: 120000 });
await page.evaluate(() => document.querySelectorAll('.bar, #help').forEach(e => e.style.display = 'none'));
let n = 0;
for (const v of views) {
  await page.evaluate(a => window.__view(...a), v.split(',').map(Number));
  await new Promise(r => setTimeout(r, 2500));
  await page.screenshot({ path: `${out}/${tag}-${n++}.png` });
}
console.log(JSON.stringify({ v: await page.$eval('#vCount', e => e.textContent), errors }));
await browser.close();
