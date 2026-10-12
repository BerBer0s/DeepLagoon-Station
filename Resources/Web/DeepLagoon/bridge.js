/* DeepLagoon adapter for TGUI's BYOND API. No shell or arbitrary console access. */
(() => {
  const root = 'res://deeplagoon/Web/DeepLagoon/';
  window.__windowId__ = 'deeplagoon';
  window.__updateQueue__ = [];
  window.update = message => window.__updateQueue__.push(message);
  let sequence = 0;
  const page = Date.now().toString(36) + Math.random().toString(36).slice(2);
  let sending = Promise.resolve();
  // Same limits and unit as GameWebView: UTF-16 code units of the type and of the payload JSON.
  const maxType = 68;
  const maxPayload = 8192;
  // Page warnings and errors for the client log (GameWebView, sawmill tgui.page): cut to 2 KiB and
  // at most 5 a second, so a broken render loop cannot flood the bridge.
  let logWindow = 0;
  let logCount = 0;
  const sendLog = (level, ns, message) => {
    const now = Date.now();
    if (now - logWindow >= 1000) {
      logWindow = now;
      logCount = 0;
    }
    if (++logCount > 5) return;
    send({ type: 'log', payload: {
      level, ns: String(ns || '').slice(0, 64), message: String(message || '').slice(0, 2048) } });
  };
  // A message the host did not take (too long, its queue full) is reported, never lost silently.
  const dropped = (type, reason) => {
    console.warn('DeepLagoon bridge dropped a message:', type, reason);
    window.dispatchEvent(new CustomEvent('deeplagoon/bridge-dropped', { detail: { type, reason } }));
    if (type !== 'log') sendLog('warn', 'bridge', 'dropped ' + type + ': ' + reason);
  };
  // Errors outside React (handlers, timers, promises); the bundle strips console output.
  window.addEventListener('error', event =>
    sendLog('error', 'window', event.error?.stack || event.message));
  window.addEventListener('unhandledrejection', event =>
    sendLog('error', 'window', 'unhandled rejection: ' + (event.reason?.stack || event.reason)));
  const send = message => {
    const payload = JSON.stringify({ ...message.payload, _sequence: sequence++, _page: page });
    if (message.type.length > maxType || payload.length > maxPayload) {
      dropped(message.type, 'too long');
      return;
    }
    // Serialize requests so a ready or action cannot overtake earlier messages.
    // Robust's res scheme is not FetchEnabled. An image request uses the existing
    // resource callback without navigating or requiring engine changes.
    sending = sending.catch(() => {}).then(() => new Promise(resolve => {
      const image = new Image();
      image.onload = resolve;
      // The host cancels the request when it does not take the message.
      image.onerror = () => {
        dropped(message.type, 'refused');
        resolve();
      };
      image.src = root + 'bridge?' + encodeURIComponent(message.type) + '&' + encodeURIComponent(payload);
    }));
  };
  window.Byond = {
    parseJson: value => typeof value === 'string' ? JSON.parse(value) : value,
    topic: value => {
      if (value.type === 'log') {
        if (value.level === 'warn' || value.level === 'error') sendLog(value.level, value.ns, value.message);
        return;
      }
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
  window.DeepLagoonNativeBackground = {
    publish: payload => send({ type: 'native-background', payload }),
  };
  // The chat viewport leaves gameplay keys alone. Only actual editable fields
  // (e.g. settings search) need CEF keyboard focus; the composer has its own window.
  if (location.pathname.endsWith('/chat.html')) {
    let inputFocused = false;
    const syncInputFocus = () => {
      const node = document.activeElement;
      const active = !!node && (node.isContentEditable ||
        (node.matches?.('input, textarea, select') && !node.disabled && !node.readOnly));
      if (active === inputFocused) return;
      inputFocused = active;
      send({ type: 'chat-input-focus', payload: { active } });
    };
    // Read activeElement after the focus transition, including input-to-input clicks.
    document.addEventListener('focusin', () => queueMicrotask(syncInputFocus));
    document.addEventListener('focusout', () => queueMicrotask(syncInputFocus));
    // CEF can lose native focus while activeElement still points at the input.
    // A later click on that same field must reacquire keyboard focus.
    document.addEventListener('mouseup', () => {
      syncInputFocus();
      // Stock Robust acquires native focus on every mouse click, even when DOM
      // focus did not change. Return it to gameplay after a non-editable click.
      if (!inputFocused) send({ type: 'chat-input-focus', payload: { active: false } });
    });
    window.addEventListener('blur', () => {
      if (!inputFocused) return;
      inputFocused = false;
      send({ type: 'chat-input-focus', payload: { active: false } });
    });
  } else {
    // Interface pages: the host keeps the keyboard for the page while a text field is edited and
    // otherwise gives keys the page does not use (WASD...) back to the game (GameWebView).
    let editing = false;
    const syncEditing = () => {
      const node = document.activeElement;
      const active = !!node && (node.isContentEditable || (!!node.matches?.(
        'textarea, select, input:not([type]), input[type=text], input[type=search], input[type=number], ' +
        'input[type=email], input[type=password], input[type=url], input[type=tel]') &&
        !node.disabled && !node.readOnly));
      if (active === editing) return;
      editing = active;
      send({ type: 'input-focus', payload: { active } });
    };
    // Read activeElement after the focus transition, including field-to-field clicks.
    document.addEventListener('focusin', () => queueMicrotask(syncEditing));
    document.addEventListener('focusout', () => queueMicrotask(syncEditing));
  }
  // Remote links, popups and wiki requests are not supported in local TGUI windows.
  window.open = () => null;
  let dragging = false;
  let suppressClick = false;
  let previousSelection = '';
  window.addEventListener('deeplagoon/window-drag', event => {
    dragging = event.detail === true;
    if (dragging) {
      suppressClick = true;
      previousSelection = document.documentElement.style.userSelect;
      document.documentElement.style.userSelect = 'none';
      window.getSelection()?.removeAllRanges();
    } else {
      document.documentElement.style.userSelect = previousSelection;
    }
  });
  document.addEventListener('mousedown', () => { if (!dragging) suppressClick = false; }, true);
  document.addEventListener('keydown', () => { if (!dragging) suppressClick = false; }, true);
  document.addEventListener('click', event => {
    if (suppressClick) {
      event.preventDefault();
      event.stopImmediatePropagation();
      suppressClick = false;
      return;
    }
    const link = event.target.closest?.('a');
    if (link && link.href && !link.href.startsWith(root)) event.preventDefault();
  }, true);
  window.addEventListener('load', () => Byond.topic({ type: 'ready' }), { once: true });
})();
