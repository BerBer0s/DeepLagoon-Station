// Native animation changes no DOM pixels. Publish only settings/viewport changes.
import '../styles/components/NativeBackground.scss';
const modes = ['none', 'cosmos', 'nebula', 'matrix', 'aurora', 'pulse',
  'waves', 'fireflies', 'sakura', 'gradient', 'rain', 'embers'];
let root, viewport, animation = 'none', opacity = 0.5;
let cleared = [], pending = false, lastPayload = '';
let resizeObserver, mutationObserver;
const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)');

const color = value => {
  const channels = value.match(/[\d.]+/g)?.map(Number);
  if (!channels || channels.length < 3) return '#202020ff';
  return '#' + [...channels.slice(0, 3), (channels[3] ?? 1) * 255]
    .map(n => Math.round(Math.min(255, Math.max(0, n))).toString(16).padStart(2, '0')).join('');
};

const schedule = () => {
  if (pending) return;
  pending = true;
  requestAnimationFrame(() => { pending = false; publish(); });
};

const publish = () => {
  if (!root || !viewport || !window.__deeplagoonNativeBackground) return;
  // Restore backgrounds while reading the current theme. Both changes happen
  // before paint, so CEF never sees an opaque frame between them.
  for (const node of cleared) node.classList.remove('dl-native-background-clear');
  cleared = [];
  const enabled = animation !== 'none';
  root.classList.toggle('Chat--native-background', enabled);
  const layout = root.closest('.Layout');
  let background = getComputedStyle(viewport === root ? root : viewport.closest('.Section') || viewport).backgroundColor;
  const custom = root.style.getPropertyValue('--chat-bg');
  if (custom) {
    const probe = document.createElement('span');
    probe.style.color = custom;
    root.appendChild(probe);
    background = getComputedStyle(probe).color;
    probe.remove();
  }
  const rect = viewport.getBoundingClientRect();
  const payload = {
    mode: enabled ? modes.indexOf(animation) : 0,
    opacity, reduced: reducedMotion.matches ? 1 : 0,
    base: color(getComputedStyle(layout || document.body).backgroundColor),
    background: color(background),
    left: rect.left / innerWidth, top: rect.top / innerHeight,
    width: rect.width / innerWidth, height: rect.height / innerHeight,
  };
  if (enabled) {
    for (let node = root; node; node = node.parentElement) {
      node.classList.add('dl-native-background-clear');
      cleared.push(node);
    }
  }
  const serialized = JSON.stringify(payload);
  if (serialized === lastPayload) return;
  lastPayload = serialized;
  window.DeepLagoonNativeBackground?.publish(payload);
};

export const configureNativeChatBackground = (node, scrollNode, mode, intensity) => {
  if (node !== root || scrollNode !== viewport) {
    resizeObserver?.disconnect();
    mutationObserver?.disconnect();
    for (const previous of cleared) previous.classList.remove('dl-native-background-clear');
    cleared = [];
    root = node; viewport = scrollNode;
    lastPayload = '';
    if (root && viewport) {
      resizeObserver = new ResizeObserver(schedule);
      resizeObserver.observe(viewport);
      resizeObserver.observe(root);
      const themeKey = () => {
        const themes = [];
        for (let node = root; node; node = node.parentElement)
          themes.push(...[...node.classList].filter(name => name.startsWith('theme-')));
        return themes.join(' ');
      };
      let lastTheme = themeKey();
      mutationObserver = new MutationObserver(records => {
        const theme = themeKey();
        // React can replace className when tabs/themes change, removing the
        // native transparency classes even when the preset stays the same.
        const lostTransparency = cleared.some(node => !node.classList.contains('dl-native-background-clear'));
        if (theme !== lastTheme || lostTransparency ||
            (animation !== 'none' && window.__deeplagoonNativeBackground && !root.classList.contains('Chat--native-background')) ||
            records.some(record => record.attributeName === 'style')) schedule();
        lastTheme = theme;
      });
      for (let node = root; node; node = node.parentElement)
        mutationObserver.observe(node, { attributes: true,
          attributeFilter: node === root ? ['style', 'class'] : ['class'] });
    }
  }
  animation = modes.includes(mode) ? mode : 'none';
  opacity = Number.isFinite(intensity) ? Math.min(1, Math.max(0, intensity)) : 0.5;
  schedule();
  return window.__deeplagoonNativeBackground === true;
};

export const clearNativeChatBackground = node => {
  if (!node || node !== root) return;
  animation = 'none';
  publish();
  root?.classList.remove('Chat--native-background');
  resizeObserver?.disconnect();
  mutationObserver?.disconnect();
  root = viewport = null;
  lastPayload = '';
};

window.addEventListener('deeplagoon/native-background', () => {
  // The host resets its layer when the browser leaves the tree or reloads.
  // Replay even if the document and its appearance have not changed.
  lastPayload = '';
  schedule();
});
window.addEventListener('resize', schedule);
reducedMotion.addEventListener('change', schedule);
