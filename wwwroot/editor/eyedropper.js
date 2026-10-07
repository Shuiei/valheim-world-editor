// Eyedropper: the next click on an object in the world gives its kind to whoever asked (the Plant
// list, the Replace lists). Esc or a click on empty ground cancels. Only drawn objects can be picked.

export function createEyedropper(ed) {
  let waiting = null;   // { done(name, event), label }
  let swallowUp = false;   // the pointerup of the picking click is not for the tools either
  function pick(label, done) {
    waiting = { label, done };
    ed.el.style.cursor = 'crosshair';
    ed.msg(`Click an object to pick its kind for ${label}. Esc cancels.`);
  }
  function stop(text) {
    waiting = null;
    ed.el.style.cursor = '';
    if (text) ed.msg(text);
  }
  // Before any tool sees the click.
  ed.el.addEventListener('pointerdown', e => {
    if (!waiting || e.button !== 0) return;
    e.stopImmediatePropagation(); e.preventDefault();
    swallowUp = true;
    const ent = ed.pickEntity(e), w = waiting;
    if (!ent) { stop('Nothing picked: click right on an object (only things that are shown can be picked).'); return; }
    stop();
    w.done(ent.name, e);
  }, true);
  ed.el.addEventListener('pointerup', e => { if (swallowUp && e.button === 0) { swallowUp = false; e.stopImmediatePropagation(); } }, true);
  addEventListener('keydown', e => { if (waiting && e.key === 'Escape') { e.stopImmediatePropagation(); stop('Picking cancelled.'); } }, true);
  ed.pickKind = pick;
  return { pick, get active() { return !!waiting; } };
}
