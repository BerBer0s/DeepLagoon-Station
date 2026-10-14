// Offscreen GLSL check of the production SWSL fragment: direction/frame atlas selection,
// data-texture orientation, source texels, contour lighting, and transparent padded borders.
// PLAYWRIGHT_PATH may point to an existing Playwright installation.
const fs = require('node:fs');
const path = require('node:path');
const { chromium } = require(process.env.PLAYWRIGHT_PATH || 'playwright');
const source = fs.readFileSync(path.join(__dirname, '../../Resources/Textures/_DeepLagoon/Shaders/clothing_body_fit.swsl'), 'utf8');

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    const page = await browser.newPage();
    const result = await page.evaluate((source) => {
      const canvas = document.createElement('canvas');
      canvas.width = canvas.height = 64;
      const gl = canvas.getContext('webgl', { antialias: false, preserveDrawingBuffer: true });
      if (!gl) throw new Error('WebGL unavailable');
      const fragment = 'precision highp float; precision highp int; uniform sampler2D sourceTexture; ' +
        'vec4 zTexture(vec2 uv) { return texture2D(sourceTexture, uv); }\n' + source
          .replace(/uniform highp vec2\[64\] (\w+);/g, 'uniform highp vec2 $1[64];')
          .replace(/void vertex\(\)\s*\{[^}]*\}/, '')
          .replace('void fragment()', 'void main()').replace(/\bCOLOR\b/g, 'gl_FragColor');
      function compile(type, code) {
        const shader = gl.createShader(type);
        gl.shaderSource(shader, code); gl.compileShader(shader);
        if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) throw new Error(gl.getShaderInfoLog(shader));
        return shader;
      }
      const program = gl.createProgram();
      gl.attachShader(program, compile(gl.VERTEX_SHADER, 'attribute vec2 position; varying highp vec2 fitUV; void main(){fitUV=position*.5+.5;gl_Position=vec4(position,0.,1.);}'));
      gl.attachShader(program, compile(gl.FRAGMENT_SHADER, fragment)); gl.linkProgram(program);
      if (!gl.getProgramParameter(program, gl.LINK_STATUS)) throw new Error(gl.getProgramInfoLog(program));
      gl.useProgram(program);
      const buffer = gl.createBuffer(); gl.bindBuffer(gl.ARRAY_BUFFER, buffer);
      gl.bufferData(gl.ARRAY_BUFFER, new Float32Array([-1,-1,1,-1,-1,1,1,1]), gl.STATIC_DRAW);
      const position = gl.getAttribLocation(program, 'position');
      gl.enableVertexAttribArray(position); gl.vertexAttribPointer(position, 2, gl.FLOAT, false, 0, 0);
      const original = new Uint8Array(128 * 64 * 4);
      const warp = new Uint8Array(64 * 256 * 4);
      const mins = new Float32Array(128), maxs = new Float32Array(128);
      for (let d = 0; d < 4; d++) {
        for (let f = 0; f < 2; f++) {
          const tile = d * 2 + f, tx = tile % 4 * 32, ty = Math.floor(tile / 4) * 32;
          mins[tile*2] = tx/128; mins[tile*2+1] = 1-(ty+32)/64;
          maxs[tile*2] = (tx+32)/128; maxs[tile*2+1] = 1-ty/64;
          for (let y = 0; y < 32; y++) for (let x = 0; x < 32; x++) {
            const i = ((63-ty-y)*128+tx+x)*4;
            original.set([x*7,y*7,d*40+f*20,255], i);
          }
        }
        for (let y = 0; y < 32; y++) for (let x = 0; x < 32; x++) {
          const i = ((255-d*64-y-16)*64+x+16)*4;
          warp.set([d === 1 ? 31-x : x, d === 2 ? 31-y : y, d < 2 ? 110 + y : 128, 255], i);
        }
      }
      function texture(unit, name, width, height, data) {
        gl.activeTexture(gl.TEXTURE0+unit); gl.bindTexture(gl.TEXTURE_2D, gl.createTexture());
        for (const parameter of [gl.TEXTURE_MIN_FILTER, gl.TEXTURE_MAG_FILTER]) gl.texParameteri(gl.TEXTURE_2D, parameter, gl.NEAREST);
        for (const parameter of [gl.TEXTURE_WRAP_S, gl.TEXTURE_WRAP_T]) gl.texParameteri(gl.TEXTURE_2D, parameter, gl.CLAMP_TO_EDGE);
        gl.texImage2D(gl.TEXTURE_2D,0,gl.RGBA,width,height,0,gl.RGBA,gl.UNSIGNED_BYTE,data);
        gl.uniform1i(gl.getUniformLocation(program,name),unit);
      }
      texture(0,'sourceTexture',128,64,original); texture(1,'fitMap',64,256,warp);
      gl.uniform2fv(gl.getUniformLocation(program,'directionMin[0]'),mins);
      gl.uniform2fv(gl.getUniformLocation(program,'directionMax[0]'),maxs);
      gl.uniform2f(gl.getUniformLocation(program,'frameSize'),32,32);
      gl.uniform1i(gl.getUniformLocation(program,'frameCount'),2);
      gl.uniform1i(gl.getUniformLocation(program,'directionCount'),4);
      gl.disable(gl.DITHER);
      const output = new Uint8Array(64*64*4);
      for (let d = 0; d < 4; d++) for (let f = 0; f < 2; f++) {
        const tile = d*2+f;
        gl.uniform4f(gl.getUniformLocation(program,'sourceUV'),mins[tile*2],mins[tile*2+1],maxs[tile*2],maxs[tile*2+1]);
        gl.drawArrays(gl.TRIANGLE_STRIP,0,4); gl.readPixels(0,0,64,64,gl.RGBA,gl.UNSIGNED_BYTE,output);
        if (gl.getError() !== gl.NO_ERROR) throw new Error('WebGL render error');
        for (let y = 0; y < 64; y++) for (let x = 0; x < 64; x++) {
          const i = ((63-y)*64+x)*4, inside = x>=16 && x<48 && y>=16 && y<48;
          if (output[i+3] !== (inside ? 255 : 0)) throw new Error(`Alpha mismatch: ${d}/${f} at ${x},${y}`);
          if (!inside) continue;
          const sx = d===1 ? 47-x : x-16, sy = d===2 ? 47-y : y-16;
          const light = (d < 2 ? 110 + y - 16 : 128) / 128;
          const expected = [sx*7, sy*7, d*40+f*20].map(c => Math.round(c*light));
          if (expected.some((c, channel) => Math.abs(output[i+channel]-c) > 1))
            throw new Error(`Atlas/direction/lighting mismatch: ${d}/${f} at ${x},${y}`);
        }
      }
      return { directions: 4, framesPerDirection: 2, pixelsChecked: 32768 };
    }, source);
    console.log(JSON.stringify(result));
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
