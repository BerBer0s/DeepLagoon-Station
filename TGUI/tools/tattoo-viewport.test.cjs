const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

(async () => {
  const source = fs.readFileSync(path.join(__dirname, '../packages/tgui/interfaces/tattooViewportUtils.js'), 'utf8');
  const { canvasPoint, zoomAt, initialView } = await import('data:text/javascript,' + encodeURIComponent(source));
  assert.deepEqual(canvasPoint([.75, .5], { zoom: 2, x: 0, y: 0 }), [.625, .5]);
  const p = [.63, .28], view = { zoom: 2.5, x: .17, y: -.2 };
  const before = canvasPoint(p, view), next = zoomAt(view, p, 1.8), after = canvasPoint(p, next);
  before.forEach((value, i) => assert.ok(Math.abs(value - after[i]) < 1e-12, 'Cursor anchor must stay on the same drawing point'));
  assert.equal(zoomAt(initialView, [.5, .5], 100).zoom, 8);
  assert.equal(zoomAt(initialView, [.5, .5], .001).zoom, .5);
  assert.deepEqual(initialView, { zoom: 1, x: 0, y: 0 });
  console.log('PASS: inverse drawing coordinates, cursor-anchored zoom, bounds and reset');
})().catch(error => { console.error(error); process.exitCode = 1; });
