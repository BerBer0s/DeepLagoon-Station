import test from 'node:test';
import assert from 'node:assert/strict';
import { cellsBetween, stamp, copyItems, validateDraft, changeCount, makeTemplate, projectTemplate, insertTemplate, makePatch, itemsAtPoint, editorKey } from '../packages/tgui/interfaces/gridEditorModel.mjs';
const options = { rotation: 90, color: '#ffffff', z: 0, cleanable: false, replace: true, isWall: (id) => id === 'Wall' };
let id = 0;
const nextId = () => `new_${id++}`;
const area = { x: -4, y: -4, width: 16, height: 16 };

test('picker hits the opaque sprite above a floor and passes through transparent table items', () => {
  const items = [{ id: 'floor', kind: 'tile', x: 0, y: 0 }, { id: 'table', kind: 'entity', x: .5, y: .5, rotation: 0 }, { id: 'cup', kind: 'entity', x: .5, y: .5, rotation: 0 }];
  const layers = { tile: true, entity: true, decal: true };
  const visual = item => ({ drawDepth: item.id === 'cup' ? 10 : 2, images: [item.id] });
  const opaque = (images, u, v) => images[0] !== 'cup' || u > .4 && u < .6 && v > .4 && v < .6;
  assert.equal(itemsAtPoint(items, { x: .2, y: .2 }, layers, visual, opaque)[0].id, 'table');
  assert.equal(itemsAtPoint(items, { x: .5, y: .5 }, layers, visual, opaque)[0].id, 'cup');
  assert.deepEqual(itemsAtPoint(items, { x: .5, y: .5 }, layers, visual, opaque).map(i => i.id), ['cup', 'table', 'floor']);
  assert.equal(itemsAtPoint(items, { x: .5, y: .5 }, { ...layers, entity: false }, visual, opaque)[0].id, 'floor');
});

test('picker respects fractional sprite positions and rotation', () => {
  const item = { id: 'object', kind: 'entity', x: 1.25, y: .5, rotation: 90 };
  const visual = () => ({ images: ['object'] });
  const opaque = (_, u) => u < .5;
  assert.equal(itemsAtPoint([item], { x: 1.25, y: .25 }, { entity: true }, visual, opaque).length, 1);
  assert.equal(itemsAtPoint([item], { x: 1.25, y: .75 }, { entity: true }, visual, opaque).length, 0);
  assert.equal(itemsAtPoint([item], { x: .5, y: .25 }, { entity: true }, visual, opaque).length, 0);
});

test('shortcuts use physical keys with Russian and native CEF key events', () => {
  assert.equal(editorKey({ key: 'я', code: 'KeyZ', ctrlKey: true }), 'z');
  assert.equal(editorKey({ code: 'KeyI' }), 'i');
  assert.equal(editorKey({ code: 'Escape' }), 'escape');
  assert.equal(editorKey({ code: 'ShiftLeft' }), 'shift');
});

test('line brush has no gaps in either direction, including negative grid coordinates', () => {
  const a = { x: -3, y: -2 }, b = { x: 4, y: 2 };
  const cells = cellsBetween(a, b);
  assert.deepEqual(cells[0], a); assert.deepEqual(cells.at(-1), b);
  assert.equal(cells.length, 8);
  for (let i = 1; i < cells.length; i++) {
    assert.ok(Math.abs(cells[i].x - cells[i - 1].x) <= 1);
    assert.ok(Math.abs(cells[i].y - cells[i - 1].y) <= 1);
  }
  assert.deepEqual(cellsBetween(b, a), cells.toReversed());
});
test('rectangle works when dragged backwards; repaint replaces tiles rather than duplicating them', () => {
  const cells = cellsBetween({ x: 2, y: 2 }, { x: 0, y: 0 }, true);
  assert.equal(cells.length, 9);
  const first = stamp([], cells, { kind: 'tile', id: 'Steel' }, options, nextId);
  const before = copyItems(first);
  const second = stamp(first, cells, { kind: 'tile', id: 'Wood' }, options, nextId);
  assert.equal(second.length, 9); assert.equal(changeCount(first, second), 9);
  assert.deepEqual(first, before);
  assert.equal(validateDraft(second, area), '');
});
test('wall replacement preserves furniture and avoids repeat-stroke duplicates', () => {
  const old = [{ id: 'wall', kind: 'entity', prototype: 'Wall', x: .5, y: .5 },
    { id: 'chair', kind: 'entity', prototype: 'Chair', x: .5, y: .5 }];
  const brush = { id: 'NewWall', kind: 'entity', category: 'wall', anchored: true };
  const first = stamp(old, [{ x: 0, y: 0 }], brush, options, nextId);
  const second = stamp(first, [{ x: 0, y: 0 }], brush, options, nextId);
  assert.equal(first.length, 2); assert.equal(second.length, 2);
  assert.equal(second[0].prototype, 'Chair'); assert.equal(second[1].rotation, 90);
  assert.deepEqual(old.map((i) => i.prototype), ['Wall', 'Chair']);
});
test('draft validation rejects unsupported decals, anchored objects and out-of-area moves', () => {
  const decal = { id: 'new_decal', kind: 'decal', x: -.25, y: .25 };
  assert.match(validateDraft([decal], area), /пол/);
  const tile = { id: 't:-1:0', kind: 'tile', x: -1, y: 0 };
  assert.equal(validateDraft([tile, decal], area), '');
  assert.match(validateDraft([tile, { ...decal, x: 12 }], area), /границ/);
  assert.match(validateDraft([tile, tile], area), /ID/);
});
test('one drag history entry restores fractional entity coordinates', () => {
  const saved = [{ id: 'e:1', kind: 'entity', prototype: 'Chair', x: .625, y: -.125, rotation: 45 }];
  const history = copyItems(saved);
  const moved = saved.map((i) => ({ ...i, x: i.x + 3, y: i.y + 2 }));
  assert.equal(changeCount(saved, moved), 1);
  assert.equal(moved[0].x % 1, .625);
  assert.deepEqual(history, saved);
});
test('a building template retains all layers and can be inserted repeatedly with unique entity IDs', () => {
  const original = [
    { id: 't:-2:3', kind: 'tile', prototype: 'Steel', x: -2, y: 3, rotation: 0, mirror: 0 },
    { id: 'e:1', kind: 'entity', prototype: 'Wall', x: -1.5, y: 3.5, rotation: 90, anchored: true },
    { id: 'd:1', kind: 'decal', prototype: 'Line', x: -2, y: 3, rotation: 0, color: '#ff0000', z: 4 },
  ];
  const template = makeTemplate(original);
  assert.equal(template[0].x, 0); assert.equal(template[1].x, .5);
  let draft = insertTemplate([], template, { x: 1, y: 1 }, 0, nextId);
  draft = insertTemplate(draft, template, { x: 4, y: 1 }, 0, nextId);
  assert.equal(draft.length, 6); assert.equal(new Set(draft.map(i => i.id)).size, 6);
  assert.equal(validateDraft(draft, area), '');
  assert.equal(draft.find(i => i.kind === 'decal').color, '#ff0000');
  assert.equal(original[0].x, -2);
});
test('rotated templates keep anchored centers on tiles and return to the original after four turns', () => {
  const template = [
    { id: 't:0:0', kind: 'tile', x: 0, y: 0, rotation: 0, mirror: 0 },
    { id: 't:1:0', kind: 'tile', x: 1, y: 0, rotation: 0, mirror: 0 },
    { id: 'e:1', kind: 'entity', x: 1.5, y: .5, rotation: 0, anchored: true },
  ];
  const rotated = projectTemplate(template, { x: -1, y: -1 }, 1);
  assert.equal(rotated[2].x, -.5); assert.equal(rotated[2].y, .5); assert.equal(rotated[2].rotation, 90);
  for (const item of rotated.filter(i => i.kind === 'entity')) {
    assert.equal(Math.abs(item.x % 1), .5); assert.equal(Math.abs(item.y % 1), .5);
  }
  assert.equal(validateDraft(insertTemplate([], template, { x: -1, y: -1 }, 1, nextId), area), '');
  assert.deepEqual(projectTemplate(template, { x: 0, y: 0 }, 4), template);
});
test('editing one element sends only its change, while deletions produce explicit tombstones', () => {
  const original = Array.from({ length: 1000 }, (_, index) => ({ id: 'e:' + index, kind: 'entity', prototype: 'Chair', x: index, y: .5 }));
  const draft = original.slice(1).map(i => i.id === 'e:10' ? { ...i, y: 1.5 } : i);
  const patch = makePatch(original, draft);
  assert.equal(patch.length, 2); assert.equal(changeCount(original, draft), 2);
  assert.deepEqual(patch[1], { id: 'e:0', kind: 'remove' });
  const rebuilt = new Map(original.map(i => [i.id, i]));
  for (const item of patch) item.kind === 'remove' ? rebuilt.delete(item.id) : rebuilt.set(item.id, item);
  assert.deepEqual([...rebuilt.values()], draft);
});
