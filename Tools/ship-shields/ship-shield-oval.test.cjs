// Offscreen rendering of the production shield fragment and prototype parameters.
// PLAYWRIGHT_PATH can point to an existing Playwright installation.
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const { chromium } = require(process.env.PLAYWRIGHT_PATH || 'playwright');
const source = fs.readFileSync(path.join(__dirname, '../../Resources/Textures/_DeepLagoon/Shaders/ship_shield.swsl'), 'utf8');
const prototype = fs.readFileSync(path.join(__dirname, '../../Resources/Prototypes/_DeepLagoon/Shaders/ship_shield.yml'), 'utf8');
const names = ['hex_size', 'fill_opacity', 'line_opacity', 'rim_opacity', 'animation_speed', 'inner_width_ratio', 'inner_height_ratio'];
const params = Object.fromEntries(names.map(name => [name, Number(prototype.match(new RegExp(name + ':\\s*([\\d.]+)'))?.[1])]));
assert(names.every(name => Number.isFinite(params[name])), 'Every shader parameter must come from the shipped prototype');
assert(Math.abs(58 * params.inner_width_ratio - 38) < .00001, 'Selected preview cut-out width');
assert(Math.abs(44 * params.inner_height_ratio - 26) < .00001, 'Selected preview cut-out height');

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    const page = await browser.newPage();
    const results = await page.evaluate(({ source, params }) => {
      const canvas = document.createElement('canvas');
      canvas.width = canvas.height = 256;
      const gl = canvas.getContext('webgl', { antialias: false, premultipliedAlpha: false });
      if (!gl) throw Error('WebGL unavailable');
      function compile(type, text) {
        const shader = gl.createShader(type);
        gl.shaderSource(shader, text); gl.compileShader(shader);
        if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) throw Error(gl.getShaderInfoLog(shader));
        return shader;
      }
      const program = gl.createProgram();
      gl.attachShader(program, compile(gl.VERTEX_SHADER,
        'attribute vec2 pos; varying vec2 UV; void main(){ UV=pos*.5+.5; gl_Position=vec4(pos,0.,1.); }'));
      const fragment = 'precision highp float; precision highp int; varying vec2 UV; uniform float TIME; ' +
        'uniform sampler2D mainTexture; vec4 zTexture(vec2 uv){return texture2D(mainTexture,uv);}\n' +
        source.replace('light_mode unshaded;', '').replace('void fragment()', 'void main()').replace(/\bCOLOR\b/g, 'gl_FragColor');
      gl.attachShader(program, compile(gl.FRAGMENT_SHADER, fragment)); gl.linkProgram(program);
      if (!gl.getProgramParameter(program, gl.LINK_STATUS)) throw Error(gl.getProgramInfoLog(program));
      gl.useProgram(program);
      const buffer = gl.createBuffer(); gl.bindBuffer(gl.ARRAY_BUFFER, buffer);
      gl.bufferData(gl.ARRAY_BUFFER, new Float32Array([-1,-1,1,-1,-1,1,1,1]), gl.STATIC_DRAW);
      const position = gl.getAttribLocation(program, 'pos');
      gl.enableVertexAttribArray(position); gl.vertexAttribPointer(position, 2, gl.FLOAT, false, 0, 0);
      gl.bindTexture(gl.TEXTURE_2D, gl.createTexture());
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.NEAREST);
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.NEAREST);
      gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, 1, 1, 0, gl.RGBA, gl.UNSIGNED_BYTE, new Uint8Array([255,255,255,255]));
      gl.uniform1i(gl.getUniformLocation(program, 'mainTexture'), 0);
      for (const [name, value] of Object.entries(params)) gl.uniform1f(gl.getUniformLocation(program, name), value);
      return [[58,44], [116,88], [30,100], [300,300]].map(([width,height]) => {
        gl.uniform2f(gl.getUniformLocation(program, 'shield_size'), width, height);
        gl.clearColor(0,0,0,0); gl.clear(gl.COLOR_BUFFER_BIT);
        gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
        function alpha(x,y) {
          const pixel = new Uint8Array(4);
          // x/y are fractions of the inner ellipse's semi-axes.
          const px = Math.floor((x * params.inner_width_ratio + 1) * 128);
          const py = Math.floor((y * params.inner_height_ratio + 1) * 128);
          gl.readPixels(px, py, 1, 1, gl.RGBA, gl.UNSIGNED_BYTE, pixel);
          return pixel[3];
        }
        return { width, height, error: gl.getError(), center: alpha(0,0),
          innerX: alpha(.9,0), innerY: alpha(0,.9), innerDiagonal: alpha(.6,.6),
          outerX: alpha(1.1,0), outerY: alpha(0,1.1), outerDiagonal: alpha(.8,.8) };
      });
    }, { source, params });
    for (const result of results) {
      assert.equal(result.error, 0);
      for (const name of ['center', 'innerX', 'innerY', 'innerDiagonal']) assert.equal(result[name], 0, name);
      for (const name of ['outerX', 'outerY', 'outerDiagonal']) assert(result[name] > 0, name);
    }
    console.log('PASS variant 8: production oval proportions, transparent interior, visible shield on axes and diagonal, four shield sizes');
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exit(1); });
