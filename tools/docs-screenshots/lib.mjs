import puppeteer from 'puppeteer';
export const W = ms => new Promise(r => setTimeout(r, ms));
export async function launch() {
  const browser = await puppeteer.launch({ headless: true, args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--no-sandbox'] });
  return browser;
}
export async function editor(browser, base, q, prefs = {}) {
  const page = await browser.newPage();
  await page.setViewport({ width: 1440, height: 900 });
  page.errors = []; page.on('pageerror', e => page.errors.push(e.message)); page.on('dialog', d => d.accept());
  await page.evaluateOnNewDocument(p => { localStorage.clear(); for (const [k, v] of Object.entries(p)) localStorage.setItem(k, v); }, prefs);
  await page.goto(`${base}/editor.html?${q}`, { waitUntil: 'networkidle0', timeout: 120000 });
  await page.waitForFunction(() => document.querySelector('[data-count="trees"]').textContent !== '', { timeout: 120000 });
  await W(1500);
  return page;
}
// Camera looking at world point (x, z) (Unity coords) from an offset (dx, up, dz) in metres.
export const look = (page, x, z, dx, up, dz) => page.evaluate((x, z, dx, up, dz) => {
  const ed = window.__ed, gx = x - ed.originX, gz = z - ed.originZ, h = ed.sampleHeight(gx, gz), tx = gx - ed.cx, tz = -(gz - ed.cz);
  window.__view(tx + dx, h + up, tz + dz, tx, h, tz);
}, x, z, dx, up, dz);
// Screen position of world point (x, z) on the ground.
export const screen = (page, x, z, lift = 0) => page.evaluate((x, z, lift) => {
  const ed = window.__ed, gx = x - ed.originX, gz = z - ed.originZ;
  const v = new ed.THREE.Vector3(gx - ed.cx, ed.sampleHeight(gx, gz) + lift, -(gz - ed.cz)).project(ed.camera);
  const r = ed.el.getBoundingClientRect(); return [r.left + (v.x + 1) / 2 * r.width, r.top + (1 - v.y) / 2 * r.height];
}, x, z, lift);
export const shot = (page, dir, name) => page.screenshot({ path: `${dir}/${name}.jpg`, type: 'jpeg', quality: 82 });
export const hideView = page => page.evaluate(() => { document.getElementById('viewPanel').hidden = true; document.getElementById('viewToggle')?.classList.remove('on'); });
