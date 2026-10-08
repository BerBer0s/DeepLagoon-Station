const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const { test } = require('node:test');

const bridge = fs.readFileSync(path.resolve(__dirname,
  '../../Resources/Web/DeepLagoon/bridge.js'), 'utf8');

function createPage(name = 'chat.html') {
  const listeners = {};
  const messages = [];
  const document = {
    activeElement: null,
    addEventListener: (type, callback) => (listeners[type] ??= []).push(callback),
  };
  class Image {
    set src(url) {
      const [type, payload] = url.split('?')[1].split('&');
      messages.push({ type: decodeURIComponent(type), ...JSON.parse(decodeURIComponent(payload)) });
      this.onload();
    }
  }
  const windowListeners = {};
  const window = {
    addEventListener: (type, callback) => (windowListeners[type] ??= []).push(callback),
  };
  vm.runInNewContext(bridge, {
    window, document, Image, location: { pathname: `/Web/DeepLagoon/${name}` }, queueMicrotask,
  });
  return {
    messages,
    async blurWindowAndClickSameInput() {
      for (const callback of windowListeners.blur ?? []) callback();
      await new Promise(resolve => setImmediate(resolve));
      for (const callback of listeners.mouseup ?? []) callback();
      await new Promise(resolve => setImmediate(resolve));
    },
    async focus(node) {
      document.activeElement = node;
      for (const callback of listeners.focusout ?? []) callback();
      for (const callback of listeners.focusin ?? []) callback();
      // Flush the focus microtask and serialized bridge transport.
      await new Promise(resolve => setImmediate(resolve));
    },
  };
}

const input = (options = {}) => ({ matches: () => true, ...options });
const background = { matches: () => false };

test('reading chat never requests keyboard focus', async () => {
  const page = createPage();
  await page.focus(background);
  assert.deepEqual(page.messages, []);
});

test('editable fields acquire focus and leaving them returns gameplay keys', async () => {
  const page = createPage();
  await page.focus(input());
  await page.focus(input());
  await page.focus(background);
  assert.deepEqual(page.messages.map(m => [m.type, m.active]), [
    ['chat-input-focus', true], ['chat-input-focus', false],
  ]);
});

test('disabled and readonly fields leave gameplay keys available', async () => {
  const page = createPage();
  await page.focus(input({ disabled: true }));
  await page.focus(input({ readOnly: true }));
  assert.deepEqual(page.messages, []);
});

test('contenteditable receives keyboard focus', async () => {
  const page = createPage();
  await page.focus({ isContentEditable: true });
  await page.focus(background);
  assert.deepEqual(page.messages.map(m => m.active), [true, false]);
});

test('returning to the same input after clicking the game reacquires focus', async () => {
  const page = createPage();
  await page.focus(input());
  await page.blurWindowAndClickSameInput();
  assert.deepEqual(page.messages.map(m => m.active), [true, false, true]);
});

test('interface and composer retain their existing focus policy', async () => {
  const page = createPage('interface.html');
  await page.focus(input());
  await page.focus(background);
  assert.deepEqual(page.messages, []);
});
