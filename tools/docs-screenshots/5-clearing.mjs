import { launch, editor, look, screen, shot, W, hideView } from './lib.mjs';
const [base, dir] = process.argv.slice(2);
const browser = await launch();
const X = -128, Z = -384;
const p = await editor(browser, base, 'zx=-1&zz=-6&size=3', { viewFolded: '["Look"]', editorShow2: JSON.stringify({ trees: false, rocks: true, buildings: true, water: true, borders: true }) });
await hideView(p);
// Arrows on a rock near the clearing
const rock = await p.evaluate((X, Z) => { const ed = window.__ed; const r = [...ed.objects.records.values()].filter(r => r.kind === 'rocks' && !r.deleted && ed.entityOf(r.id)?.inst.length).sort((a, b) => Math.hypot(a.x - X - 6, a.z - Z) - Math.hypot(b.x - X - 6, b.z - Z))[0]; ed.setTool('select'); ed.selectIds([r.id]); return { x: r.x, z: r.z, name: r.name }; }, X, Z);
await look(p, rock.x, rock.z, 5, 6, 8); await W(1200);
const [gx, gy] = await screen(p, rock.x + 2.5, rock.z, 0.6); await p.mouse.move(gx, gy); await W(500);
await shot(p, dir, 'select-arrows'); console.error('arrows', rock.name);
await p.evaluate(() => window.__ed.clearSelection());
// Measure across the mound with slope colours
await p.evaluate(() => { const c = document.getElementById('ovSlope'); c.checked = true; c.dispatchEvent(new Event('change')); });
await p.keyboard.press('m'); await W(200);
await look(p, X - 8, Z, 6, 30, 30); await W(800);
{ const [a, b] = await screen(p, X - 22, Z + 4); await p.mouse.click(a, b); await W(400); const [c, d] = await screen(p, X - 8, Z + 1); await p.mouse.click(c, d); await W(600); }
await shot(p, dir, 'measure'); console.error('measure');
console.log(JSON.stringify(p.errors));
await browser.close();
