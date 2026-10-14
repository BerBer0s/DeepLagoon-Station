const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const React = require('react');
const { renderToStaticMarkup } = require('react-dom/server');
const { transformSync } = require('esbuild');

// Render the real JSX with server-provided snapshots; no game/client startup.
const source = fs.readFileSync(path.resolve(__dirname, '../packages/tgui/interfaces/ApartmentTravel.jsx'), 'utf8')
  .replace(/^import .*;\r?$/gm, '')
  .replace(/export const /g, 'const ');
const code = transformSync(source + '\nglobalThis.Panel = ApartmentTravel;', { loader: 'jsx' }).code;
function render(data) {
  const context = vm.createContext({ React, useState: React.useState, useBackend: () => ({ data, act() {} }) });
  vm.runInContext(code, context);
  return renderToStaticMarkup(React.createElement(context.Panel));
}

test('entrance menu lets a first-time owner invite nearby players and accept an invitation', () => {
  const html = render({ canManage: true, canDepart: true, entryOpen: true,
    people: [{ user: 'guest', name: 'Сосед' }], invitations: [{ user: 'owner', name: 'Хозяин' }] });
  assert.match(html, /Вход в квартиру на жилой остановке/);
  assert.match(html, /Сдать снаряжение и отправиться домой/);
  assert.match(html, /Пригласить/);
  assert.match(html, /Принять и отправиться/);
  assert.doesNotMatch(html, /Вернуться на остановку/);
});

test('home terminal exposes return to guests and reserves editing for the owner', () => {
  const guest = render({ atHome: true, canManage: false });
  const owner = render({ atHome: true, canManage: true, entryOpen: true });
  assert.match(guest, /Терминал квартиры/);
  assert.match(guest, /Вернуться на остановку/);
  assert.doesNotMatch(guest, /Обустройство квартиры/);
  assert.match(owner, /Обустройство квартиры/);
  assert.doesNotMatch(owner, /Сдать снаряжение и отправиться домой/);
});

test('pending boarding disables departure, acceptance and invitations', () => {
  const html = render({ pending: true, canDepart: true, canManage: true, entryOpen: true,
    people: [{ user: 'guest', name: 'Сосед' }], invitations: [{ user: 'owner', name: 'Хозяин' }] });
  for (const text of ['Сдать снаряжение и отправиться домой', 'Принять и отправиться', 'Пригласить'])
    assert.match(html, new RegExp('<button disabled="">' + text + '</button>'));
});
