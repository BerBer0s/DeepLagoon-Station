import fs from 'node:fs';
import path from 'node:path';
import http from 'node:http';
import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { build } from 'esbuild';
import * as sass from 'sass';

const require = createRequire(import.meta.url);
const { chromium } = require(process.env.PLAYWRIGHT_PATH || 'playwright');
const root = path.resolve(import.meta.dirname, '..');
const output = path.join(root, 'node_modules/.cache/shuttle-console-variants');
const destination = path.resolve(process.argv[2]);
fs.mkdirSync(output, { recursive: true });
fs.mkdirSync(destination, { recursive: true });

// Reuse the existing GPU fixture, including Clyde's actual colour conversions.
const test = fs.readFileSync(path.join(root, 'tools/shuttle-console-browser.test.mjs'), 'utf8');
const begin = 'const glass = await page.evaluate(({ source, colorConversions }) => {';
const finish = '}, { source: glassSource, colorConversions });';
const start = test.indexOf(begin) + begin.length;
const end = test.indexOf(finish, start);
assert.ok(start >= begin.length && end > start);
let body = test.slice(start, end)
  .replace("document.querySelector('.ShuttleConsole__viewport')", 'element')
  .replace('const base = document.createElement', `
      for (const canvas of viewport.querySelectorAll('canvas')) canvas.__previewGL?.getExtension('WEBGL_lose_context')?.loseContext();
      viewport.replaceChildren();
      const mask = getComputedStyle(viewport.parentElement).backgroundColor.match(/[\\d.]+/g).slice(0,3).map(Number);
      source = source.replace('vec3(0.027451, 0.054902, 0.035294)', 'mask_color').replace('weight * 0.4', 'weight * preview_strength');
      const base = document.createElement`)
  .replace('uniform bool inspectGlass; vec4 glassResult;', 'uniform bool inspectGlass; uniform highp vec3 mask_color; uniform highp float preview_strength; vec4 glassResult;')
  .replace("gl.uniform1i(gl.getUniformLocation(program, 'inspectGlass'), 1);", `
      overlay.__previewGL = gl;
      gl.uniform3f(gl.getUniformLocation(program, 'mask_color'), mask[0]/255, mask[1]/255, mask[2]/255);
      gl.uniform1f(gl.getUniformLocation(program, 'preview_strength'), strength);
      gl.uniform1i(gl.getUniformLocation(program, 'inspectGlass'), 1);`)
  .replace('return result;', 'return {...result, maskMatches: result.corner.slice(0,3).every((value,i)=>Math.abs(value-mask[i])<=1)};');
const radarHelper = path.join(output, 'radar.mjs');
fs.writeFileSync(radarHelper, `export function paintPreviewRadar({element,source,colorConversions,strength=.4}){${body}}`);

const entry = path.join(root, 'tools/shuttle-console-variants.jsx');
const bundle = path.join(output, 'variants.js');
await build({ entryPoints: [entry], outfile: bundle, bundle: true, minify: true, jsx: 'automatic',
  define: { 'process.env.NODE_ENV': '"production"' }, nodePaths: [path.join(root, 'node_modules')],
  plugins: [{ name: 'preview', setup(build) {
    build.onResolve({ filter: /^\.\.\/backend$/ }, () => ({ path: 'preview-backend', namespace: 'preview' }));
    build.onLoad({ filter: /.*/, namespace: 'preview' }, () => ({ contents:
      `export {usePreviewBackend as useBackend} from ${JSON.stringify(entry)};`, resolveDir: root }));
    build.onResolve({ filter: /generated-shuttle-radar\.mjs$/ }, () => ({ path: radarHelper }));
    build.onLoad({ filter: /\.scss$/ }, () => ({ contents: '', loader: 'css' }));
  } }] });
const css = sass.compile(path.join(root, 'packages/tgui/interfaces/ShuttleConsole.scss')).css
  + sass.compile(path.join(root, 'tools/shuttle-console-themes.scss')).css;
const shader = fs.readFileSync(path.join(root, '../Resources/Textures/_DeepLagoon/Shaders/shuttle_radar_glass.swsl'), 'utf8');
const surface = fs.readFileSync(path.join(root, '../Resources/Textures/_DeepLagoon/Shaders/shuttle_radar_surface.swsl'), 'utf8');
const library = fs.readFileSync(path.join(root, '../RobustToolbox/Robust.Client/Graphics/Clyde/Shaders/z-library.glsl'), 'utf8');
const conversions = library.slice(library.indexOf('highp vec4 zFromSrgb'), library.indexOf('// -- uniforms --'));
const fragment = fs.readFileSync(path.join(root, 'tools/shuttle-console-variants.html'), 'utf8')
  .replace('/*__SHUTTLE_CSS__*/', css)
  .replace('/*__SHUTTLE_DATA__*/', `window.shuttleGlassSource=${JSON.stringify(shader)};window.shuttleSurfaceSource=${JSON.stringify(surface)};window.shuttleColorConversions=${JSON.stringify(conversions)};`)
  .replace('/*__SHUTTLE_SCRIPT__*/', fs.readFileSync(bundle, 'utf8').replaceAll('</script', '<\\/script'));
assert.ok(Buffer.byteLength(fragment) < 1_000_000, 'The comparison must fit the inline visualization limit');
const artifact = path.join(destination, 'shuttle-console-designs.html');
fs.writeFileSync(artifact, fragment);
fs.writeFileSync(path.join(output, 'preview.html'), `<html><meta charset="utf-8"><style>body{margin:0}.viz-carousel{width:100%}[hidden]{display:none!important}</style>${fragment}</html>`);

const server = http.createServer((req, res) => { res.setHeader('Content-Type', 'text/html; charset=utf-8'); res.end(fs.readFileSync(path.join(output, 'preview.html'))); });
await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
const browser = await chromium.launch({ channel: 'msedge', headless: true });
try {
  const page = await browser.newPage();
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  const skins = ['quasimorph', 'military', 'graphite', 'field'];
  for (const [width, height] of [[1060, 700], [760, 700], [320, 1140]]) {
    await page.setViewportSize({ width, height });
    await page.goto(`http://127.0.0.1:${server.address().port}`);
    for (const skin of skins) {
      // Host carousel navigation is supplied by Codex. Toggle only for local QA.
      await page.evaluate(skin => {
        for (const panel of document.querySelectorAll('.viz-carousel > section')) panel.hidden = !panel.querySelector(`#shuttle-variant-${skin}`);
      }, skin);
      const panel = page.locator(`#shuttle-variant-${skin}`);
      await panel.locator('.ShuttleConsole').waitFor();
      await page.waitForFunction(skin => document.querySelector(`#shuttle-variant-${skin} .ShuttleConsole`)?.dataset.maskVerified === 'true', skin);
      const shape = await panel.locator('.ShuttleConsole').evaluate(app => {
        const ports = [...app.querySelectorAll('.ShuttleConsole__port-list button')].map(button => {
          const r = button.getBoundingClientRect(); return { x:r.x, y:r.y, right:r.right, orientation:getComputedStyle(button).textOrientation };
        });
        return { overflow: document.documentElement.scrollWidth > innerWidth, ports,
          background:getComputedStyle(app.querySelector('.ShuttleConsole__viewport-area')).backgroundColor };
      });
      assert.equal(shape.overflow, false, `${skin} must fit ${width}px`);
      assert.equal(shape.ports.length, 8);
      for (let i=1;i<shape.ports.length;i++) {
        assert.equal(shape.ports[i].y, shape.ports[0].y);
        assert.ok(shape.ports[i].x >= shape.ports[i-1].right);
        assert.equal(shape.ports[i].orientation, 'upright');
      }
      await panel.getByRole('button', { name:'Карта / FTL', exact:true }).click();
      await panel.getByRole('button', { name:'FTL', exact:true }).click();
      await panel.getByRole('button', { name:'Станция DeepLagoon', exact:true }).click();
      await panel.getByRole('button', { name:'Стыковка', exact:true }).click();
      await panel.getByRole('button', { name:'Блок FTL', exact:true }).click();
      await panel.getByRole('button', { name:'Пилот / орудия', exact:true }).click();
      await page.waitForFunction(skin => document.querySelector(`#shuttle-variant-${skin} .ShuttleConsole`)?.dataset.maskVerified === 'true', skin);
      if (width === 1060) {
        await panel.locator('.ShuttleConsole aside').evaluate(el => { el.scrollTop = 0; });
        await panel.screenshot({ path:path.join(destination, `shuttle-${skin}.png`) });
      }
      console.log(`PASS ${skin} ${width}px: layout, horizontal ports/upright labels, native mask colour, mode actions`);
    }
  }
  assert.deepEqual(errors, []);
} finally { await browser.close(); await new Promise(resolve => server.close(resolve)); }
console.log(`PREVIEW ${artifact} (${Buffer.byteLength(fragment)} bytes)`);
