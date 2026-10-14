// Production bundle smoke test. PLAYWRIGHT_MODULE may point at an installed Playwright package.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const http = require('node:http');
const path = require('node:path');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const root = path.resolve(__dirname, '../../Resources/Web/DeepLagoon');
const png = 'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/l9sAAAAASUVORK5CYII=';

(async () => {
  const server = http.createServer((request, response) => {
    const file = path.resolve(root, '.' + new URL(request.url, 'http://localhost').pathname);
    if (!file.startsWith(root + path.sep) || !fs.existsSync(file)) { response.writeHead(404).end(); return; }
    const ext = path.extname(file);
    response.setHeader('Content-Type', ({ '.html': 'text/html', '.js': 'application/javascript', '.css': 'text/css', '.woff2': 'font/woff2' })[ext] || 'application/octet-stream');
    response.end(fs.readFileSync(file));
  });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  let browser;
  try {
    browser = await chromium.launch({ channel: process.env.BROWSER_CHANNEL || 'msedge', headless: true });
    const page = await browser.newPage({ viewport: { width: 1100, height: 760 } });
    const errors = [];
    page.on('pageerror', error => errors.push(String(error)));
    const transport = process.env.BRIDGE_TRANSPORT || 'image';
    await page.addInitScript(({ png, transport }) => {
      window.__sent = [];
      window.__fetchBridgeRequests = 0;
      window.__deeplagoonFetchBridge = transport === 'fetch' || transport === 'post';
      window.__deeplagoonPostBridge = transport === 'post';
      const respond = message => {
        if (message.type === 'act/icons') {
          window.update(JSON.stringify({ type: 'update', payload: { data: { iconBatch: { icons:
            message.payload.ids.split(',').map(id => ({ id, layers: [{ url: png, color: '#ffffff' }] })) } } } }));
        }
      };
      const nativeFetch = window.fetch.bind(window);
      window.fetch = async (url, options) => {
        if (!url.startsWith('res://deeplagoon/Web/DeepLagoon/bridge')) return nativeFetch(url, options);
        const posted = options?.method === 'POST';
        const [type, payload] = posted ? [options.body.slice(0, options.body.indexOf('\n')), options.body.slice(options.body.indexOf('\n') + 1)] : url.split('?')[1].split('&');
        const message = { type: posted ? type : decodeURIComponent(type), payload: JSON.parse(posted ? payload : decodeURIComponent(payload)) };
        window.__fetchBridgeRequests++;
        window.__sent.push(message);
        queueMicrotask(() => respond(message));
        return { ok: true, arrayBuffer: async () => new ArrayBuffer(0) };
      };
      const NativeImage = window.Image;
      window.Image = class extends NativeImage {
        set src(value) {
          if (!value.startsWith('res://deeplagoon/Web/DeepLagoon/bridge?')) { super.src = value; return; }
          const [type, payload] = value.split('?')[1].split('&');
          const message = { type: decodeURIComponent(type), payload: JSON.parse(decodeURIComponent(payload)) };
          window.__sent.push(message);
          queueMicrotask(() => {
            this.onload?.(new Event('load'));
            respond(message);
          });
        }
      };
    }, { png, transport });
    await page.goto(`http://127.0.0.1:${server.address().port}/interface.html`);
    await page.waitForFunction(() => window.__tguiAppBooted__);
    const state = { available: true, mode: 'appearance', name: 'Test', age: 25, sex: 0, gender: 0, spawn: 0,
      species: 'Human', height: 1, width: 1, minAge: 18, maxAge: 100, hairColor: '#ffffff', beardColor: '#ffffff',
      skinColor: '#ffffff', eyeColor: '#ffffff', hairStyle: 'Hair0', beardStyle: 'FacialHairShaved',
      appearance: {}, tabs: [], previewSlots: [], speciesOptions: [{ id: 'Human', name: 'Human' }],
      hairOptions: Array.from({ length: 100 }, (_, i) => ({ id: `Hair${i}`, name: `Hair ${i}`, iconId: `marking/Hair${i}` })),
      beardOptions: [] };
    const publish = data => page.evaluate(data => {
      const dynamic = { ...data };
      const catalogue = {};
      for (const key of ['hairOptions', 'beardOptions', 'speciesOptions', 'departments', 'traits', 'markingOptions']) {
        catalogue[key] = dynamic[key];
        delete dynamic[key];
      }
      window.update(JSON.stringify({ type: 'update', payload: {
        replaceData: true, replaceStaticData: true, resetPartialData: true,
        static_data: catalogue,
        config: { interface: 'CharacterEditor', status: 2, window: { key: 'deeplagoon', fancy: false } }, data: dynamic } }));
    }, data);
    await publish(state);
    await page.getByText('Выбор причёски', { exact: true }).waitFor();
    assert.equal(await page.evaluate(() => window.__sent.filter(m => m.type === 'act/icons').length), 0, 'Collapsed hair catalogue must not decode icons');
    await page.getByText('Выбор причёски', { exact: true }).click();
    await page.locator('.HairCarousel__track').first().scrollIntoViewIfNeeded();
    await page.waitForFunction(() => window.__sent.some(m => m.type === 'act/icons'));
    const visibleIds = await page.evaluate(() => window.__sent.filter(m => m.type === 'act/icons').flatMap(m => m.payload.ids.split(',')));
    assert.ok(visibleIds.length > 0 && visibleIds.length <= 8, `Only visible hair icons requested: ${visibleIds.length}`);
    await page.locator('.HairCarousel__track').first().evaluate(track => { track.scrollLeft = track.clientWidth * 2; });
    await page.waitForFunction(count => window.__sent.filter(m => m.type === 'act/icons').flatMap(m => m.payload.ids.split(',')).length > count, visibleIds.length);
    await page.evaluate(() => window.update(JSON.stringify({ type: 'update', payload: { data: { hairColor: '#123456' } } })));
    assert.equal(await page.locator('.HairCarousel').count(), 1, 'Incremental colour update must preserve the static catalogue');
    await page.evaluate(() => window.DeepLagoonHealth.ping());
    await page.waitForFunction(() => window.__sent.some(m => m.type === 'health/pong'));
    await publish({ ...state, mode: 'jobs', departments: [{ id: 'med', name: 'Medical', jobs: [{ id: 'Doctor', name: 'Doctor', allowed: true, priority: 0 }] }], jobState: { Doctor: { allowed: true, priority: 3 } } });
    await page.getByText('Высокий', { exact: true }).waitFor();
    await page.evaluate(() => window.update(JSON.stringify({ type: 'update', payload: { data: { jobState: { Doctor: { allowed: true, priority: 0 } } } } })));
    await page.getByText('Никогда', { exact: true }).waitFor();
    await publish({ ...state, mode: 'traits', traits: [{ id: 'A', name: 'Trait A', category: 'Traits', cost: 1 }, { id: 'B', name: 'Trait B', category: 'Traits', cost: 1 }], traitState: { A: { visible: false, selected: false }, B: { visible: true, selected: false } } });
    await page.getByText('Trait B (1)', { exact: true }).waitFor();
    assert.equal(await page.getByText('Trait A (1)', { exact: true }).count(), 0);
    await page.evaluate(() => window.update(JSON.stringify({ type: 'update', payload: { data: { traitState: { A: { visible: true, selected: true }, B: { visible: false, selected: false } } } } })));
    await page.getByText('Trait A (1)', { exact: true }).waitFor();
    assert.equal(await page.getByText('Trait B (1)', { exact: true }).count(), 0);
    await publish({ available: false, mode: 'appearance' });
    await page.getByText('Выберите персонажа.', { exact: false }).waitFor();
    assert.equal(await page.locator('.HairCarousel').count(), 0);
    assert.deepEqual(errors, []);
    assert.equal(await page.evaluate(() => window.__fetchBridgeRequests > 0), transport === 'fetch' || transport === 'post');
    console.log(`PASS: production boot, collapsed catalogue, ${visibleIds.length} visible icons, carousel lazy loading, colour update, health pong, empty profile, no JS errors`);
  } finally {
    await browser?.close();
    await new Promise(resolve => server.close(resolve));
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
