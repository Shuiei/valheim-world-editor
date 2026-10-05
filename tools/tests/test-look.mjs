import puppeteer from 'puppeteer';
const base = process.argv[2], out = process.argv[3], q = process.argv[4] ?? 'zx=0&zz=-5&size=3', tag = process.argv[5] ?? 'look';
const browser = await puppeteer.launch({ headless: true, args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--no-sandbox'] });
const page = await browser.newPage();
await page.setViewport({ width: 1400, height: 850 });
const errors = []; page.on('pageerror', e => errors.push(e.message)); page.on('console', m => { if (['error', 'warning'].includes(m.type())) errors.push(m.text().slice(0, 1500)); });
await page.goto(`${base}/editor.html?${q}`, { waitUntil: 'networkidle0' });
await page.waitForFunction(() => !document.getElementById('loading'), { timeout: 90000 });
await page.waitForFunction(() => document.getElementById('lookState').textContent === '' || document.getElementById('lookState').textContent.includes('could'), { timeout: 90000 });
await new Promise(r => setTimeout(r, 4000));
await page.screenshot({ path: `${out}/${tag}-wide.png` });
await page.mouse.move(700, 450);
for (let i = 0; i < 6; i++) { await page.mouse.wheel({ deltaY: -250 }); await new Promise(r => setTimeout(r, 150)); }
await new Promise(r => setTimeout(r, 3000));
await page.screenshot({ path: `${out}/${tag}-close.png` });
console.log(JSON.stringify({ state: await page.$eval('#lookState', e => e.textContent), errors }));
await browser.close();
