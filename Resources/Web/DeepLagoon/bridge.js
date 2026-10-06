/* DeepLagoon adapter for TGUI's BYOND API. No shell or arbitrary console access. */
(() => {
  const root = 'res://deeplagoon/Web/DeepLagoon/';
  window.__windowId__ = 'deeplagoon';
  window.__updateQueue__ = [];
  window.update = message => window.__updateQueue__.push(message);
  let sequence = 0;
  const page = Date.now().toString(36) + Math.random().toString(36).slice(2);
  let sending = Promise.resolve();
  const send = message => {
    if (JSON.stringify(message).length > 12000) return;
    // Serialize requests so a ready or action cannot overtake earlier messages.
    // Robust's res scheme is not FetchEnabled. An image request uses the existing
    // resource callback without navigating or requiring engine changes.
    sending = sending.catch(() => {}).then(() => new Promise(resolve => {
      const image = new Image();
      image.onload = image.onerror = resolve;
      image.src = root + 'bridge?' + encodeURIComponent(message.type) + '&' +
        encodeURIComponent(JSON.stringify({ ...message.payload, _sequence: sequence++, _page: page }));
    }));
  };
  window.Byond = {
    parseJson: value => typeof value === 'string' ? JSON.parse(value) : value,
    topic: value => {
      if (value.type === 'panel/state_set' && typeof value.panel_state === 'string') {
        try { send({ type: value.type, payload: JSON.parse(value.panel_state) }); } catch {}
        return;
      }
      let payload = value.payload;
      if (typeof payload === 'string') {
        try { payload = JSON.parse(payload); } catch { return; }
      }
      if (value.type === 'ping') {
        window.update(JSON.stringify({ type: 'pingReply', payload }));
        return;
      }
      // Only the explicitly supported TGUI messages enter the C# bridge.
      if (value.type === 'ready' || value.type === 'close' || value.type === 'suspend' ||
          value.type?.startsWith('act/')) {
        send({ type: value.type === 'suspend' ? 'close' : value.type, payload: payload || {} });
      }
    },
    winset: () => {}, // Window geometry belongs to Robust's native window.
    winget: (_id, key) => Promise.resolve(key === 'size' ? `${innerWidth},${innerHeight}` : '0,0'),
    command: () => {},
    loadCss: () => {},
    loadJs: () => {},
  };
  // Remote links, popups and wiki requests are not supported in local TGUI windows.
  window.open = () => null;
  document.addEventListener('click', event => {
    const link = event.target.closest?.('a');
    if (link && link.href && !link.href.startsWith(root)) event.preventDefault();
  }, true);
  window.addEventListener('load', () => Byond.topic({ type: 'ready' }), { once: true });
})();
