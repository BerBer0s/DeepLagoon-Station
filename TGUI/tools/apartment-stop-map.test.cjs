const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const yaml = require('js-yaml');
const folder = path.resolve(__dirname, '../../Resources/Maps/_DeepLagoon/Apartments');
const original = yaml.load(fs.readFileSync(path.join(folder, 'residential_stop.authored.yml'), 'utf8'));
const prepared = yaml.load(fs.readFileSync(path.join(folder, 'residential_stop.yml'), 'utf8'));
const entries = doc => doc.entities.flatMap(group => group.entities.map(entity => ({ proto: group.proto, entity })));
const comp = (entity, type) => entity.components.find(value => value.type === type);
const root = doc => entries(doc).find(entry => entry.entity.uid === doc.grids[0]).entity;
function floorCells(doc) {
  const cells = new Set();
  for (const [key, chunk] of Object.entries(comp(root(doc), 'MapGrid').chunks)) {
    const [cx, cy] = key.split(',').map(Number), bytes = Buffer.from(chunk.tiles, 'base64');
    for (let y = 0; y < 16; y++) for (let x = 0; x < 16; x++)
      if (doc.tilemap[bytes.readInt32LE((y * 16 + x) * 7)] !== 'Space') cells.add(`${cx * 16 + x},${cy * 16 + y}`);
  }
  return cells;
}
const all = entries(prepared);
const cell = entity => comp(entity, 'Transform').pos.split(',').map(Number).map(Math.floor).join(',');
test('frozen map preserves every authored position, rotation, floor and decal', () => {
  assert.deepEqual(comp(root(original), 'MapGrid'), comp(root(prepared), 'MapGrid'));
  assert.deepEqual(comp(root(original), 'DecalGrid'), comp(root(prepared), 'DecalGrid'));
  for (const entry of entries(original).filter(entry => entry.proto)) {
    const next = all.find(value => value.entity.uid === entry.entity.uid);
    assert.equal(next.proto, entry.proto === 'DebugGenerator' ? 'DLApartmentStopGenerator' : entry.proto);
    const before = comp(entry.entity, 'Transform'), after = comp(next.entity, 'Transform');
    assert.equal(after.pos, before.pos); assert.equal(after.rot, before.rot); assert.equal(after.parent, before.parent);
  }
  assert.equal(new Set(all.map(entry => entry.entity.uid)).size, prepared.meta.entityCount);
});
test('power networks stay on floor and connect their sources and endpoints', () => {
  const floors = floorCells(prepared);
  for (const proto of ['CableHV', 'CableMV', 'CableApcExtension']) {
    const wires = new Set(all.filter(entry => entry.proto === proto).map(entry => cell(entry.entity)));
    const reached = new Set(), queue = [wires.values().next().value];
    for (let index = 0; index < queue.length; index++) {
      const key = queue[index]; if (reached.has(key)) continue; reached.add(key);
      assert.ok(floors.has(key));
      const [x, y] = key.split(',').map(Number);
      for (const next of [`${x + 1},${y}`, `${x - 1},${y}`, `${x},${y + 1}`, `${x},${y - 1}`])
        if (wires.has(next) && !reached.has(next)) queue.push(next);
    }
    assert.equal(reached.size, wires.size);
    const endpoints = proto === 'CableHV' ? ['DLApartmentStopGenerator', 'SubstationBasic', 'SMESAdvanced']
      : proto === 'CableMV' ? ['SubstationBasic', 'APCSuperCapacity'] : ['APCSuperCapacity'];
    for (const endpoint of endpoints) for (const entry of all.filter(value => value.proto === endpoint)) {
      assert.ok(comp(entry.entity, 'Transform').anchored); assert.ok(wires.has(cell(entry.entity)));
    }
  }
});
test('native atmosphere masks contain exactly all floor cells at standard pressure', () => {
  const atmos = comp(root(prepared), 'GridAtmosphere'), cells = new Set();
  assert.equal(atmos.version, 2); assert.equal(atmos.data.chunkSize, 4);
  for (const [key, masks] of Object.entries(atmos.data.tiles)) {
    const [cx, cy] = key.split(',').map(Number);
    for (let y = 0; y < 4; y++) for (let x = 0; x < 4; x++)
      if (masks[0] & (1 << (x + 4 * y))) cells.add(`${4 * cx + x},${4 * cy + y}`);
  }
  assert.deepEqual([...cells].sort(), [...floorCells(prepared)].sort());
  const air = atmos.data.uniqueMixes[0];
  const pressure = (air.moles.Oxygen + air.moles.Nitrogen) * 8.314462618 * air.temperature / air.volume;
  assert.ok(Math.abs(pressure - 101.325) < .1); assert.equal(air.immutable, undefined);
});
test('gravity and safe-zone settings persist and both docking ports retain their directions', () => {
  assert.deepEqual(comp(root(prepared), 'Gravity'), { type: 'Gravity', enabled: true, inherent: true });
  assert.equal(comp(root(prepared), 'PacifiedZoneGenerator').wholeGrid, true);
  assert.ok(comp(root(prepared), 'ApartmentProtectedGrid'));
  for (const entry of all.filter(value => value.proto === 'AirlockExternalGlassShuttleTransit')) {
    assert.equal(comp(entry.entity, 'Transform').rot, 270);
    assert.equal(comp(entry.entity, 'Transform').anchored, true);
    assert.equal(comp(entry.entity, 'ApartmentEntrance'), undefined);
  }
});
