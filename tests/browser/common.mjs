// Shared by the browser test files: each file starts its own app on its own copy of the test world
// (so the files run side by side), opens the world unless asked not to, and drives one page.
import { before, after } from 'node:test';
import assert from 'node:assert/strict';
import { startApp, waitPhase, frames, sleep } from './harness.mjs';

const ctx = { t: null };
// The running app (base URL, page, api(), errors...), as `t` in the tests.
export const t = new Proxy({}, { get: (_, k) => ctx.t[k] });
export function setup({ open = true } = {}) {
  before(async () => {
    ctx.t = await startApp();
    if (open) {
      await ctx.t.api('/api/launcher/open', { path: ctx.t.worldDir });
      await waitPhase(ctx.t, 'editor');
    }
  }, { timeout: 180000 });
  after(async () => { await ctx.t?.stop(); });
}

export const page = () => ctx.t.page;
export const noErrors = () => assert.deepEqual(ctx.t.errors, [], 'no JavaScript errors on the page');
export const pending = () => page().$eval('#pending', e => e.textContent);
// prepare: a function run in a page of the app first (to set this browser's saved settings), so the
// editor starts with them without loading it twice.
export async function openEditor(q = 'zx=0&zz=0&size=3', prepare = null) {
  if (prepare) {
    if (!page().url().startsWith(ctx.t.base)) await page().goto(`${ctx.t.base}/icon.png`);
    await page().evaluate(prepare);
  }
  await page().goto(`${ctx.t.base}/editor.html?${q}`, { waitUntil: 'networkidle0', timeout: 120000 });
  await page().waitForFunction(() => window.__ed?.objects.records.size > 0 && !document.getElementById('loading'), { timeout: 120000 });
  await sleep(1000);
  // Every "more" fold open, so the tests reach the settings inside.
  await page().evaluate(() => { document.getElementById('viewPanel').hidden = true; document.activeElement?.blur(); document.querySelectorAll('details.more').forEach(d => { d.open = true; }); window.__ed.plantDrawer(false); });
  await frames(page());
}
export const areaAction = a => page().evaluate(a => window.__ed.areaAction(a), a);
export const applyArea = async a => { await areaAction(a); await page().click('#aApply'); };
export const records = () => page().evaluate(() => [...window.__ed.objects.records.values()].filter(r => !r.deleted).map(r => ({ id: r.id, name: r.name, x: r.x, y: r.y, z: r.z, added: r.added })));
