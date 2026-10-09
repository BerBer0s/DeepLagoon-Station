const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const { test } = require('node:test');

const source = fs.readFileSync(path.resolve(__dirname,
  '../packages/tgui-panel/chat/nativeBackground.js'), 'utf8')
  .replace(/^import .*;$/gm, '').replace(/export const /g, 'const ');

function page(native = true) {
  const frames = [], messages = [], listeners = {}, observers = [];
  const element = (parent = null, background = 'rgb(23, 28, 36)') => {
    const classes = new Set();
    return {
      parentElement: parent, background,
      classList: {
        add: name => classes.add(name), remove: name => classes.delete(name),
        contains: name => classes.has(name),
        toggle: (name, enabled) => enabled ? classes.add(name) : classes.delete(name),
        [Symbol.iterator]: () => classes.values(),
      },
      style: { getPropertyValue: () => '' },
      closest: () => null,
      getBoundingClientRect: () => ({ left: 0, top: 0, width: 800, height: 600 }),
    };
  };
  const body = element(), root = element(body), viewport = element(root, 'rgb(21, 21, 21)');
  const window = {
    __deeplagoonNativeBackground: native,
    DeepLagoonNativeBackground: { publish: payload => messages.push(payload) },
    matchMedia: () => ({ matches: false, addEventListener() {} }),
    addEventListener: (name, callback) => { listeners[name] = callback; },
  };
  class Observer {
    constructor(callback) { this.callback = callback; observers.push(this); }
    observe() {}
    disconnect() { this.disconnected = true; }
  }
  const context = vm.createContext({
    window, document: { body }, innerWidth: 800, innerHeight: 600,
    ResizeObserver: Observer, MutationObserver: Observer,
    requestAnimationFrame: callback => frames.push(callback),
    getComputedStyle: node => ({ backgroundColor: node.background }),
  });
  vm.runInContext(source + '\nglobalThis.configure = configureNativeChatBackground;' +
    '\nglobalThis.clear = clearNativeChatBackground;', context);
  return { root, viewport, body, window, messages, observers,
    configure: context.configure, clear: context.clear,
    ready() { window.__deeplagoonNativeBackground = true; listeners['deeplagoon/native-background'](); },
    flush() { while (frames.length) frames.shift()(); },
  };
}

test('late native handshake publishes settings and suppresses CSS animation', () => {
  const p = page(false);
  assert.equal(p.configure(p.root, p.viewport, 'cosmos', 0.7), false);
  p.flush();
  assert.equal(p.messages.length, 0);
  p.ready(); p.flush();
  assert.equal(p.messages[0].mode, 1);
  assert.ok(p.root.classList.contains('Chat--native-background'));
  assert.ok(p.body.classList.contains('dl-native-background-clear'));
});

test('ready replays unchanged settings after native reset; idle changes are deduplicated', () => {
  const p = page();
  p.configure(p.root, p.viewport, 'rain', 0.5); p.flush();
  p.configure(p.root, p.viewport, 'rain', 0.5); p.flush();
  assert.equal(p.messages.length, 1);
  p.ready(); p.flush();
  assert.equal(p.messages.length, 2);
  assert.equal(p.messages[1].mode, 10);
  p.observers[0].callback(); p.flush();
  assert.equal(p.messages.length, 2);
});

test('React class replacement restores transparency without republishing identical settings', () => {
  const p = page();
  p.configure(p.root, p.viewport, 'nebula', 0.5); p.flush();
  p.root.classList.remove('Chat--native-background');
  p.root.classList.remove('dl-native-background-clear');
  p.observers[1].callback([{ attributeName: 'class' }]); p.flush();
  assert.ok(p.root.classList.contains('dl-native-background-clear'));
  assert.ok(p.root.classList.contains('Chat--native-background'));
  assert.equal(p.messages.length, 1);
});

test('editor uses its own surface color, all presets, and clears the layer on unmount', () => {
  const p = page();
  const modes = ['none', 'cosmos', 'nebula', 'matrix', 'aurora', 'pulse',
    'waves', 'fireflies', 'sakura', 'gradient', 'rain', 'embers'];
  modes.forEach((mode, index) => {
    p.configure(p.root, p.root, mode, 0.8); p.flush();
    assert.equal(p.messages.at(-1).mode, index);
    assert.equal(p.messages.at(-1).background, '#171c24ff');
  });
  p.clear(p.root); p.flush();
  assert.equal(p.messages.at(-1).mode, 0);
  assert.ok(!p.root.classList.contains('dl-native-background-clear'));
  assert.ok(!p.body.classList.contains('dl-native-background-clear'));
  assert.ok(p.observers.every(observer => observer.disconnected));
  const count = p.messages.length;
  p.ready(); p.flush();
  assert.equal(p.messages.length, count);
});
