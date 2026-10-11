export const copyItems = (items) => items.map((item) => ({ ...item }));
export const tileId = (x, y) => `t:${x}:${y}`;
export const cellOf = (item) => ({ x: Math.floor(item.x), y: Math.floor(item.y) });
export const inside = (cell, area) => cell.x >= area.x && cell.y >= area.y &&
  cell.x < area.x + area.width && cell.y < area.y + area.height;

export function cellsBetween(a, b, rectangle = false) {
  const result = [];
  if (rectangle) {
    for (let x = Math.min(a.x, b.x); x <= Math.max(a.x, b.x); x++)
      for (let y = Math.min(a.y, b.y); y <= Math.max(a.y, b.y); y++) result.push({ x, y });
    return result;
  }
  let x = a.x, y = a.y;
  const dx = Math.abs(b.x - x), dy = -Math.abs(b.y - y);
  const sx = x < b.x ? 1 : -1, sy = y < b.y ? 1 : -1;
  let error = dx + dy;
  while (true) {
    result.push({ x, y });
    if (x === b.x && y === b.y) return result;
    const twice = 2 * error;
    if (twice >= dy) { error += dy; x += sx; }
    if (twice <= dx) { error += dx; y += sy; }
  }
}

export function stamp(items, cells, brush, options, nextId) {
  let result = copyItems(items);
  for (const cell of cells) {
    const kind = brush.kind;
    const id = kind === 'tile' ? tileId(cell.x, cell.y) : nextId();
    // Repainting a tile replaces it; walls replace only walls, never unrelated objects in the cell.
    if (kind === 'tile') result = result.filter((item) => item.id !== id);
    if (options.replace && brush.category === 'wall') result = result.filter((item) =>
      !(item.kind === 'entity' && options.isWall(item.prototype) && Math.floor(item.x) === cell.x && Math.floor(item.y) === cell.y));
    if (kind !== 'tile' && result.some((item) => item.kind === kind && item.prototype === brush.id &&
        Math.floor(item.x) === cell.x && Math.floor(item.y) === cell.y)) continue;
    result.push({ id, kind, prototype: brush.id, x: cell.x + (kind === 'entity' ? 0.5 : 0),
      y: cell.y + (kind === 'entity' ? 0.5 : 0), rotation: kind === 'tile' ? 0 : options.rotation,
      color: options.color, z: options.z, cleanable: options.cleanable, anchored: !!brush.anchored,
      variant: 0, flags: 0, mirror: 0 });
  }
  return result;
}

export function validateDraft(items, area) {
  if (items.length > 12000) return 'В области не может быть больше 12000 элементов.';
  const ids = new Set();
  const tiles = new Set(items.filter((i) => i.kind === 'tile').map((i) => i.id));
  for (const item of items) {
    if (ids.has(item.id)) return 'Повторяющийся ID элемента.';
    ids.add(item.id);
    if (item.kind === 'entity' && item.anchored && (item.x !== Math.floor(item.x) + .5 || item.y !== Math.floor(item.y) + .5))
      return 'Закреплённые объекты должны находиться в центре тайла.';
    if (!Number.isFinite(item.x) || !Number.isFinite(item.y) || !inside(item, area)) return 'Элемент вышел за границы области редактирования.';
    if ((item.kind === 'decal' || item.anchored) && !tiles.has(tileId(Math.floor(item.x), Math.floor(item.y))))
      return 'Под декалями и закреплёнными объектами должен быть пол.';
  }
  return '';
}

export const changeCount = (original, draft) => {
  const before = new Map(original.map((i) => [i.id, JSON.stringify(i)]));
  const after = new Map(draft.map((i) => [i.id, JSON.stringify(i)]));
  return [...after].filter(([id, item]) => before.get(id) !== item).length + [...before.keys()].filter((id) => !after.has(id)).length;
};
export function makePatch(original, draft) {
  const before = new Map(original.map(i => [i.id, JSON.stringify(i)]));
  const after = new Set(draft.map(i => i.id));
  return [...draft.filter(i => before.get(i.id) !== JSON.stringify(i)),
    ...original.filter(i => !after.has(i.id)).map(i => ({ id: i.id, kind: 'remove' }))];
}

export function makeTemplate(items) {
  if (!items.length) return [];
  const x = Math.min(...items.map((i) => Math.floor(i.x))), y = Math.min(...items.map((i) => Math.floor(i.y)));
  return items.map((i) => ({ ...i, x: i.x - x, y: i.y - y }));
}

export function projectTemplate(items, target, turns = 0) {
  let result = makeTemplate(items);
  for (let turn = 0; turn < ((turns % 4) + 4) % 4; turn++) {
    const height = Math.max(...result.map((i) => Math.floor(i.y))) + 1;
    result = result.map((i) => {
      const origin = i.kind === 'entity' ? 0 : .5;
      return { ...i, x: height - (i.y + origin) - origin, y: i.x,
        rotation: i.kind === 'tile' ? 0 : (i.rotation + 90) % 360,
        mirror: i.kind === 'tile' ? (i.mirror & 4) | ((i.mirror + 1) % 4) : i.mirror };
    });
  }
  return result.map((i) => ({ ...i, x: i.x + target.x, y: i.y + target.y }));
}

export function insertTemplate(draft, items, target, turns, nextId) {
  let result = copyItems(draft);
  for (const item of projectTemplate(items, target, turns)) {
    if (item.kind === 'tile') {
      item.id = tileId(item.x, item.y); result = result.filter((i) => i.id !== item.id);
    } else {
      if (result.some((i) => i.kind === item.kind && i.prototype === item.prototype && i.x === item.x && i.y === item.y && i.rotation === item.rotation)) continue;
      item.id = nextId();
    }
    result.push(item);
  }
  return result;
}
// Match the rendered sprite, including rotation and transparent pixels, rather than its tile.
export function itemsAtPoint(items, point, layers, visual, opaque = () => true) {
  return items.map((item, index) => ({ item, index })).filter(({ item }) => {
    if (!layers[item.kind]) return false;
    const left = item.x - (item.kind === 'entity' ? .5 : 0);
    const bottom = item.y - (item.kind === 'entity' ? .5 : 0);
    const x = point.x - left - .5, y = point.y - bottom - .5;
    const angle = -(item.rotation || 0) * Math.PI / 180;
    const u = x * Math.cos(angle) - y * Math.sin(angle) + .5;
    const v = 1 - (x * Math.sin(angle) + y * Math.cos(angle) + .5);
    return u >= 0 && u < 1 && v >= 0 && v < 1 && (item.kind === 'tile' || opaque(visual(item)?.images || [], u, v));
  }).sort((a, b) => drawOrder(b.item, visual(b.item)) - drawOrder(a.item, visual(a.item)) || b.index - a.index).map(({ item }) => item);
}

export function drawOrder(item, definition) {
  return item.kind === 'tile' ? 1 : item.kind === 'decal' ? 2 + (item.z || 0) / 1000 : 100 + (definition?.drawDepth || 0);
}

export function editorKey(event) {
  if (/^Key[A-Z]$/.test(event.code || '')) return event.code.slice(3).toLowerCase();
  return ({ Escape: 'escape', Delete: 'delete', Space: ' ', ShiftLeft: 'shift', ShiftRight: 'shift' })[event.code] || (event.key || '').toLowerCase();
}
