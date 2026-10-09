import test from 'node:test';
import assert from 'node:assert/strict';
import { copyLayout, footprint, validateLayout } from '../packages/tgui/interfaces/apartmentLayout.mjs';

const room = { width: 8, height: 8, maxFurniture: 12, arrivalX: 1, arrivalY: 1,
  catalog: [{ id: 'table', width: 2, height: 1, maxCount: 10, blocksMovement: true }, { id: 'chair', width: 1, height: 1, maxCount: 2, blocksMovement: false }] };
const item = (id, furniture, x, y, rotation = 0) => ({ id, furniture, x, y, rotation });

test('draft can move repeatedly without changing the committed layout', () => {
  const saved = [item('base_chair', 'chair', 4, 4)];
  const draft = copyLayout(saved);
  for (let x = 2; x <= 5; x++) draft[0].x = x;
  assert.equal(saved[0].x, 4);
  assert.equal(draft[0].x, 5);
  assert.deepEqual(validateLayout(draft, room), {});
});
test('rotation swaps rectangular footprint', () => {
  assert.deepEqual(footprint(item('a', 'table', 2, 2, 1), room.catalog[0]), [[2, 2], [2, 3]]);
});
test('reserved exit, walls, collisions and stock give actionable errors', () => {
  assert.match(validateLayout([item('a', 'chair', 1, 1)], room).a, /прибытия/);
  assert.match(validateLayout([item('a', 'table', 6, 4)], room).a, /стеной/);
  const overlap = validateLayout([item('a', 'chair', 3, 3), item('b', 'chair', 3, 3)], room);
  assert.match(overlap.a, /пересекаются/); assert.match(overlap.b, /пересекаются/);
  assert.match(validateLayout([item('a', 'chair', 2, 2), item('b', 'chair', 3, 2), item('c', 'chair', 4, 2)], room).c, /экземпляров/);
});
test('preview detects a barrier cutting the room off from exit', () => {
  const wall = Array.from({ length: 6 }, (_, i) => item(`a${i}`, 'table', 3, i + 1));
  assert.match(validateLayout(wall, room).general, /проход/);
});
