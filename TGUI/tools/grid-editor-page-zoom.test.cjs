const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const source = fs.readFileSync(path.resolve(__dirname, '../packages/tgui/drag.js'), 'utf8');
const zoomSource = source.slice(source.indexOf('const applyBodyZoom'), source.indexOf('const installZoomControls'));
function fixture(editor) {
  const document = { querySelector: () => editor ? {} : null, body: { style: {} } };
  const context = vm.createContext({ document, userZoom: 1.2, appScale: 1.25,
    resolveAppScale: value => value, clampUserZoom: value => value, USER_ZOOM_STEP: .1,
    persistUserZoom() {}, showZoomIndicator() {} });
  vm.runInContext(zoomSource + '\nglobalThis.apply = applyBodyZoom; globalThis.wheel = handleZoomWheel;', context);
  return context;
}
test('saved page zoom and DPI compensation cannot enlarge the grid editor viewport', () => {
  const context = fixture(true);
  context.apply();
  assert.equal(context.document.body.style.zoom, '100%');
});
test('Ctrl-wheel in the editor does not change or persist page zoom', () => {
  const context = fixture(true);
  context.wheel({ ctrlKey: true, deltaY: -120, preventDefault() { assert.fail('page zoom intercepted editor input'); } });
  assert.equal(context.userZoom, 1.2);
});
test('other TGUI windows retain their page zoom and DPI compensation', () => {
  const context = fixture(false);
  let prevented = false;
  context.wheel({ ctrlKey: true, deltaY: -120, preventDefault() { prevented = true; } });
  assert.equal(context.userZoom, 1.3);
  assert.equal(context.document.body.style.zoom, '104%');
  assert.ok(prevented);
});
test('a wheel event already handled by a viewport cannot also zoom the page', () => {
  const context = fixture(false);
  context.wheel({ ctrlKey: true, defaultPrevented: true, deltaY: -120 });
  assert.equal(context.userZoom, 1.2);
});
