export const copyLayout = (layout) => layout.map((item) => ({ ...item }));

export const footprint = (item, definition) => {
  const width = item.rotation % 2 ? definition.height : definition.width;
  const height = item.rotation % 2 ? definition.width : definition.height;
  const cells = [];
  for (let x = 0; x < width; x++) for (let y = 0; y < height; y++) cells.push([item.x + x, item.y + y]);
  return cells;
};

// A local hint only; the server validates the submitted plan independently.
export const validateLayout = (layout, room) => {
  const errors = {};
  const occupied = new Map();
  const blocked = new Set();
  const counts = {};
  const ids = new Set();
  if (layout.length > room.maxFurniture) errors.general = 'Превышен лимит мебели.';
  for (const item of layout) {
    const def = room.catalog.find((entry) => entry.id === item.furniture);
    if (!def) { errors[item.id] = 'Предмет отсутствует в каталоге.'; continue; }
    if (ids.has(item.id)) errors[item.id] = 'Повторяющийся предмет.';
    ids.add(item.id);
    if (![item.x, item.y, item.rotation].every(Number.isInteger) || item.rotation < 0 || item.rotation > 3) {
      errors[item.id] = 'Некорректное положение.'; continue;
    }
    counts[item.furniture] = (counts[item.furniture] || 0) + 1;
    if (counts[item.furniture] > def.maxCount) errors[item.id] = 'Недостаточно доступных экземпляров.';
    for (const [x, y] of footprint(item, def)) {
      const key = `${x},${y}`;
      if (x < 1 || y < 1 || x >= room.width - 1 || y >= room.height - 1) errors[item.id] = 'Предмет пересекается со стеной.';
      if (x === room.arrivalX && (y === room.arrivalY || y === room.arrivalY + 1)) errors[item.id] = 'Площадка прибытия и терминал должны оставаться свободными.';
      if (occupied.has(key)) {
        errors[item.id] = errors[occupied.get(key)] = 'Предметы пересекаются.';
      }
      occupied.set(key, item.id);
      if (def.blocksMovement) blocked.add(key);
    }
  }
  const reachable = new Set([`${room.arrivalX},${room.arrivalY}`]);
  const queue = [[room.arrivalX, room.arrivalY]];
  for (let index = 0; index < queue.length; index++) {
    const [x, y] = queue[index];
    for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
      const nx = x + dx, ny = y + dy, key = `${nx},${ny}`;
      if (nx < 1 || ny < 1 || nx >= room.width - 1 || ny >= room.height - 1 || blocked.has(key) || reachable.has(key)) continue;
      reachable.add(key); queue.push([nx, ny]);
    }
  }
  for (let x = 1; x < room.width - 1; x++) for (let y = 1; y < room.height - 1; y++) {
    const key = `${x},${y}`;
    if (!blocked.has(key) && !reachable.has(key)) errors.general = 'Мебель перекрывает проход к выходу.';
  }
  return errors;
};
