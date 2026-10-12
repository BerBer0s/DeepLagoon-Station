const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const { transformSync } = require('esbuild');

const source = fs.readFileSync(path.resolve(__dirname, '../packages/tgui/backend.ts'), 'utf8');
const reducerSource = source.slice(source.indexOf('const initialState'), source.indexOf('export const backendMiddleware'))
  .replace('export const backendReducer', 'const backendReducer');
const context = vm.createContext({});
vm.runInContext(transformSync(reducerSource + '\nglobalThis.reduce = backendReducer;', { loader: 'ts' }).code, context);
const update = (state, payload) => context.reduce(state, { type: 'backend/update', payload });

test('full snapshots remove stale profile fields while preserving static catalogues and partial replies', () => {
  let state = update(undefined, { replaceData: true, config: { interface: 'CharacterEditor' }, data: { available: true, name: 'Old', hairOptions: [1] } });
  state = update(state, { static_data: { recipes: ['recipe'] } });
  state = update(state, { data: { catalog: ['chair'], iconBatch: { icons: [] } } });
  state = update(state, { replaceData: true, data: { available: false } });
  assert.equal(state.data.name, undefined);
  assert.equal(state.data.hairOptions, undefined);
  assert.equal(state.data.recipes[0], 'recipe');
  assert.equal(state.data.catalog[0], 'chair');
  assert.equal(state.data.available, false);
});

test('static replies cannot overwrite newer live state', () => {
  let state = update(undefined, { static_data: { multiplier: 1, recipes: [1] } });
  state = update(state, { replaceData: true, data: { multiplier: 2 } });
  state = update(state, { data: { iconBatch: { icons: [] } } });
  assert.equal(state.data.multiplier, 2);
});

test('changing interface and suspending clear retained catalogues', () => {
  let state = update(undefined, { static_data: { recipes: [1] }, data: { catalog: [1] } });
  state = update(state, { replaceData: true, config: { interface: 'Other' }, data: {} });
  assert.equal(state.data.recipes, undefined);
  assert.equal(state.data.catalog, undefined);
  state = update(state, { static_data: { recipes: [2] } });
  state = context.reduce(state, { type: 'backend/suspendSuccess', payload: { timestamp: 123 } });
  assert.equal(Object.keys(state.staticData).length, 0);
  assert.equal(Object.keys(state.partialData).length, 0);
});
