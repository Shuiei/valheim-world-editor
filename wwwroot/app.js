// Shared by the start page, the map and the 3D editor: the app window's helpers (folder picker,
// links that open in the browser) and the "game look" banner that follows the background setup.

const inWindow = !!(window.external && window.external.sendMessage);
const waiting = new Map();
if (inWindow && window.external.receiveMessage) {
  window.external.receiveMessage(msg => {
    const i = msg.indexOf(':'), id = msg.slice(0, i), value = msg.slice(i + 1);
    waiting.get(id)?.(value || null); waiting.delete(id);
  });
}

export const isWindow = inWindow;

// A native "choose folder" dialog in the app window; null in a browser (type the path instead).
export function pickFolder(title) {
  if (!inWindow) return Promise.resolve(null);
  const id = 'p' + Math.random().toString(36).slice(2);
  return new Promise(resolve => { waiting.set(id, resolve); window.external.sendMessage(`pick:${id}:${title}`); });
}

// A native "choose file" dialog in the app window; null in a browser.
export function pickFile(title) {
  if (!inWindow) return Promise.resolve(null);
  const id = 'p' + Math.random().toString(36).slice(2);
  return new Promise(resolve => { waiting.set(id, resolve); window.external.sendMessage(`pickfile:${id}:${title}`); });
}

// Links that leave the editor open in the user's browser, not inside the app window.
export function openExternal(url) {
  if (inWindow) window.external.sendMessage('open:' + url); else window.open(url, '_blank', 'noopener');
}
document.addEventListener('click', e => {
  const a = e.target.closest('a[href^="http"]');
  if (a && inWindow) { e.preventDefault(); openExternal(a.href); }
});

// Back to the start page to open another world (asks first when changes are not saved yet).
export async function backToWorlds() {
  try {
    const w = await (await fetch('/api/world')).json();
    const n = (w.changedZones ?? 0) + (w.deletedObjects ?? 0) + (w.addedObjects ?? 0) + (w.resetZones ?? 0);
    if (n && !confirm(w.live ? 'Some changes are not applied to the game yet. Leave this world anyway (they are lost)?' : 'Some changes are not saved yet. Leave this world anyway (they are lost)?')) return;
  } catch { }
  await fetch('/api/app/worlds', { method: 'POST' }).catch(() => {});
  for (let i = 0; i < 100; i++) {
    await new Promise(r => setTimeout(r, 300));
    try { const s = await (await fetch('/api/launcher/state', { cache: 'no-store' })).json(); if (s.phase === 'start') { location.href = '/'; return; } } catch { }
  }
}

const css = `
#gl { position: fixed; left: 50%; transform: translateX(-50%); bottom: 18px; z-index: 50; width: min(560px, calc(100vw - 32px));
  background: rgba(24,28,34,.97); border: 1px solid #3c4552; border-radius: 10px; padding: 10px 14px; color: #e6e9ee;
  font: 13px/1.45 system-ui, sans-serif; box-shadow: 0 8px 30px rgba(0,0,0,.45); }
#gl[hidden] { display: none; }
#gl.inline { position: static; transform: none; width: auto; box-shadow: none; margin: 0 0 16px; }
#gl b { color: #e0a64b; }
#gl .bar { height: 6px; background: #2e353f; border-radius: 3px; margin: 8px 0 4px; overflow: hidden; }
#gl .bar i { display: block; height: 100%; background: #e0a64b; width: 0; transition: width .6s; }
#gl .row { display: flex; gap: 6px; margin-top: 8px; }
#gl input { flex: 1; min-width: 0; background: #1e232b; color: #e6e9ee; border: 1px solid #3c4552; border-radius: 7px; padding: 6px 8px; font: inherit; }
#gl button { background: #262b34; color: #e6e9ee; border: 1px solid #3c4552; border-radius: 7px; padding: 6px 12px; font: inherit; cursor: pointer; }
#gl button.primary { background: #e0a64b; color: #1b1508; border-color: #e0a64b; font-weight: 600; }
#gl .muted { color: #8e97a6; font-size: 12px; }
#gl .x { float: right; background: none; border: 0; color: #8e97a6; padding: 0 2px; }
#gl .err { color: #e0604b; }`;

// The banner. mode: 'start' (shown inline at the top of the start page), 'map' or 'editor'.
// onReady: called once when a setup that was running here finishes.
export function gameLookBanner({ inline = null, onReady = null } = {}) {
  const st = document.createElement('style'); st.textContent = css; document.head.appendChild(st);
  const el = document.createElement('div'); el.id = 'gl'; el.hidden = true;
  if (inline) { el.classList.add('inline'); inline.prepend(el); } else document.body.appendChild(el);
  let sawRunning = false, dismissed = false, timer = null, lastHtml = '';
  const set = html => { if (html !== lastHtml) { el.innerHTML = html; lastHtml = html; wire(); } el.hidden = dismissed && !inline; };
  function wire() {
    el.querySelector('.x')?.addEventListener('click', () => { dismissed = true; el.hidden = true; });
    el.querySelector('#glBrowse')?.addEventListener('click', async () => { const p = await pickFolder('Choose the Valheim game folder'); if (p) el.querySelector('#glPath').value = p; });
    el.querySelector('#glUse')?.addEventListener('click', () => start(el.querySelector('#glPath')?.value));
    el.querySelector('#glRetry')?.addEventListener('click', () => start(null));
    el.querySelector('#glReload')?.addEventListener('click', () => location.reload());
  }
  async function start(path) {
    const r = await (await fetch('/api/gamelook/start', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ path: path || null }) })).json();
    if (!r.ok) { const e = el.querySelector('.err'); if (e) e.textContent = r.error; else alert(r.error); return; }
    poll();
  }
  async function poll() {
    clearTimeout(timer);
    let s;
    try { s = await (await fetch('/api/gamelook')).json(); } catch { timer = setTimeout(poll, 3000); return; }
    const close = inline ? '' : '<button class="x" title="Hide">✕</button>';
    if (s.state === 'running') {
      sawRunning = true; dismissed = false;
      set(`${close}<b>Preparing the game's look</b><div>${s.message ?? ''}</div><div class="bar"><i style="width:${Math.round((s.progress ?? 0) * 100)}%"></i></div>
        <div class="muted">${s.log?.at(-1)?.replace(/^\d\d:\d\d:\d\d /, '') ?? ''}</div><div class="muted">You can already pick a world and edit; the look switches on when this is done.</div>`);
      timer = setTimeout(poll, 1500);
    } else if (s.state === 'missing') {
      set(`${close}<b>Get the game's look</b><div>Valheim was not found on this computer. Choose the folder Steam installed it into (the one with <code>valheim_Data</code>), and the editor copies the game's textures and models from it, once.</div>
        <div class="row"><input id="glPath" placeholder="…/steamapps/common/Valheim" value="${s.valheim ?? ''}">${isWindow ? '<button id="glBrowse">Browse…</button>' : ''}<button id="glUse" class="primary">Use</button></div><div class="err"></div>
        <div class="muted">Without it the editor works with plain colours and no models.</div>`);
    } else if (s.state === 'failed') {
      set(`${close}<b>The game's look could not be copied</b><div class="err">${s.message ?? ''}</div><div class="row"><button id="glRetry" class="primary">Try again</button></div>`);
    } else if (s.state === 'ready' && sawRunning) {
      sawRunning = false;
      if (onReady) onReady();
      else set(`${close}<b>The game's look is ready.</b> <div class="row"><button id="glReload" class="primary">Reload to see it</button></div>`);
    } else if (!el.hidden && !lastHtml.includes('glReload')) el.hidden = true;
  }
  poll();
  return { poll };
}
