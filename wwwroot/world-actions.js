// Save / discard actions shared by the map page and the 3D editor.

// changes: a number of changed zones, or { zones, deleted, added, resets } from the editor.
function describe(changes) {
  const { zones = 0, deleted = 0, added = 0, resets = 0 } = typeof changes === 'number' ? { zones: changes } : (changes ?? {});
  return [zones ? `${zones} changed zone(s)` : '', deleted ? `${deleted} deleted object(s)` : '', added ? `${added} new object(s)` : '', resets ? `${resets} zone reset(s)` : ''].filter(Boolean).join(', ');
}

export async function saveWorld(world, changes, onDone) {
  const what = describe(changes);
  if (!what) { alert('There are no unsaved changes.'); return; }
  const ok = confirm(
    `Write ${what} into the world files?\n\n` +
    `World folder: ${world.directory}\n\n` +
    `• A full backup of the folder is made first, next to it.\n` +
    `• Valheim (server or game) must NOT be running with this world, or it will overwrite these changes when it saves.\n` +
    `• Test on a copy first: open it as a local world, or upload it to a test server.`);
  if (!ok) return;
  const r = await fetch('/api/save', { method: 'POST' });
  const res = await r.json();
  let msg = res.message;
  if (res.backupDirectory) msg += `\n\nBackup: ${res.backupDirectory}`;
  if (res.skipped?.length) msg += `\n\nNot saved:\n• ${res.skipped.join('\n• ')}`;
  alert(msg);
  onDone?.(res);
}

export async function discardChanges(changes, onDone) {
  const what = describe(changes);
  if (!what) { alert('There are no unsaved changes.'); return; }
  if (!confirm(`Discard the unsaved changes (${what})? The world is reloaded from disk.`)) return;
  await fetch('/api/discard', { method: 'POST' });
  onDone?.();
}
