// Local visual smoke test: the real packaged chat over the native shader in WebGL.
// This checks composition/bridge behavior, not in-game CEF timings.
const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');
const resources = path.resolve(__dirname, '../../Resources/Web/DeepLagoon');
const shader = fs.readFileSync(path.resolve(__dirname,
  '../../Resources/Textures/_DeepLagoon/Shaders/chat_background.swsl'), 'utf8');
const init = `window.addEventListener('error', e=>{document.documentElement.dataset.previewError=e.error?.stack||e.message;});window.__deeplagoonNativeBackground=true;
window.DeepLagoonNativeBackground={publish: payload=>parent.postMessage({nativeBackground:payload},location.origin)};`;
const html = `<!doctype html><meta charset="utf-8"><title>Native chat background preview</title>
<style>body{margin:0;background:#10151c;color:#ddd;font:14px sans-serif}header{height:48px;display:flex;gap:20px;align-items:center;padding:0 16px}#stage{position:relative;width:800px;height:600px;margin:16px;resize:both;overflow:hidden}canvas,iframe{position:absolute;width:100%;height:100%;border:0}canvas{background:#202020}#status{white-space:pre}</style>
<header><label>Фон <select id="mode"></select></label><label>Тема <select id="theme"><option value="dark">Тёмная</option><option value="light">Светлая</option></select></label><span id="status">Loading</span></header>
<div id="stage"><canvas id="canvas"></canvas><iframe id="chat" src="/chat.html"></iframe></div>
<script>
const modes=['none','cosmos','nebula','matrix','aurora','pulse','waves','fireflies','sakura','gradient','rain','embers'];
const mode=document.querySelector('#mode'),theme=document.querySelector('#theme'),frame=document.querySelector('#chat'),canvas=document.querySelector('canvas');
const editor=new URLSearchParams(location.search).has('editor');
if(editor)frame.src='/interface.html';
mode.innerHTML=modes.map(m=>'<option>'+m+'</option>').join('');mode.value='cosmos';
let payload,calls=0;window.previewStats={calls:0,shaderCompiled:false};
const gl=canvas.getContext('webgl',{alpha:true,premultipliedAlpha:false});
function compile(type,src){const s=gl.createShader(type);gl.shaderSource(s,src);gl.compileShader(s);if(!gl.getShaderParameter(s,gl.COMPILE_STATUS))throw Error(gl.getShaderInfoLog(s));return s;}
const program=gl.createProgram();
gl.attachShader(program,compile(gl.VERTEX_SHADER,'attribute vec2 pos;void main(){gl_Position=vec4(pos,0.,1.);}'));
const shader=${JSON.stringify(shader)}.replace('light_mode unshaded;','').replace('void fragment()', 'void main()');
gl.attachShader(program,compile(gl.FRAGMENT_SHADER,${JSON.stringify('precision highp float;precision mediump int;uniform float TIME;uniform vec2 SCREEN_PIXEL_SIZE;\n#define FRAGCOORD gl_FragCoord\n#define COLOR gl_FragColor\n')}+shader));
gl.linkProgram(program);if(!gl.getProgramParameter(program,gl.LINK_STATUS))throw Error(gl.getProgramInfoLog(program));window.previewStats.shaderCompiled=true;gl.useProgram(program);
const buffer=gl.createBuffer();gl.bindBuffer(gl.ARRAY_BUFFER,buffer);gl.bufferData(gl.ARRAY_BUFFER,new Float32Array([-1,-1,1,-1,-1,1,1,1]),gl.STATIC_DRAW);
const pos=gl.getAttribLocation(program,'pos');gl.enableVertexAttribArray(pos);gl.vertexAttribPointer(pos,2,gl.FLOAT,false,0,0);
const uniforms={};for(const n of ['TIME','SCREEN_PIXEL_SIZE','mode','intensity','reduced','origin','size','ui_scale','background'])uniforms[n]=gl.getUniformLocation(program,n);
const rgba=hex=>[1,3,5,7].map(i=>parseInt(hex.slice(i,i+2),16)/255);
window.addEventListener('message',event=>{if(event.source!==frame.contentWindow||!event.data.nativeBackground)return;payload=event.data.nativeBackground;calls++;window.previewStats.calls=calls;window.previewStats.payload=payload;});
function state(){const settings={theme:theme.value,chatBgAnimation:mode.value,chatBgAnimOpacity:.8};frame.contentWindow.update(JSON.stringify(editor?{type:'update',payload:{config:{interface:'CharacterEditor',title:'Редактор персонажа',status:2,window:{key:'preview',fancy:false}},data:{available:true,mode:'settings',appearance:settings,tabs:[],showClothes:true}}}:{type:'panel/state',payload:{state:JSON.stringify({v:1,settings})}}));}
mode.onchange=theme.onchange=state;frame.onload=()=>{state();frame.contentWindow.update(JSON.stringify({type:'chat/message',payload:{text:'Нативный фон движется под прозрачным браузером. Сообщения и настройки остаются в TGUI.',type:'ooc'}}));};
function draw(time){const dpr=devicePixelRatio,w=Math.round(canvas.clientWidth*dpr),h=Math.round(canvas.clientHeight*dpr);if(canvas.width!==w||canvas.height!==h){canvas.width=w;canvas.height=h;}gl.viewport(0,0,w,h);gl.disable(gl.SCISSOR_TEST);const base=rgba(payload?.base||'#202020ff');gl.clearColor(...base);gl.clear(gl.COLOR_BUFFER_BIT);if(payload?.mode){const x=payload.left*w,y=payload.top*h,sw=payload.width*w,sh=payload.height*h;gl.enable(gl.SCISSOR_TEST);gl.scissor(Math.round(x),Math.round(h-y-sh),Math.round(sw),Math.round(sh));gl.uniform1f(uniforms.TIME,time*.001);gl.uniform2f(uniforms.SCREEN_PIXEL_SIZE,1/w,1/h);gl.uniform1i(uniforms.mode,payload.mode);gl.uniform1f(uniforms.intensity,payload.opacity);gl.uniform1f(uniforms.reduced,payload.reduced);gl.uniform2f(uniforms.origin,x,y);gl.uniform2f(uniforms.size,sw/dpr,sh/dpr);gl.uniform1f(uniforms.ui_scale,dpr);gl.uniform4f(uniforms.background,...rgba(payload.background));gl.drawArrays(gl.TRIANGLE_STRIP,0,4);}document.querySelector('#status').textContent='Bridge updates: '+calls+' | GPU shader: OK | Native preset: '+modes[payload?.mode||0];requestAnimationFrame(draw);}requestAnimationFrame(draw);
</script>`;

http.createServer((req, res) => {
  const url = new URL(req.url, 'http://127.0.0.1');
  if (url.pathname === '/') { res.setHeader('Content-Type', 'text/html'); return res.end(url.searchParams.has('fallback') ? html.replace('src="/chat.html"', 'src="/chat.html?native=0"') : html); }
  if (url.pathname === '/native-preview-init.js') { res.setHeader('Content-Type', 'text/javascript'); return res.end(init); }
  const file = path.resolve(resources, '.' + decodeURIComponent(url.pathname));
  if (!file.startsWith(resources + path.sep) || !fs.existsSync(file) || !fs.statSync(file).isFile()) { res.statusCode = 404; return res.end(); }
  if (url.pathname === '/chat.html' || url.pathname === '/interface.html') {
    res.setHeader('Content-Type', 'text/html');
    return res.end(fs.readFileSync(file, 'utf8').replace(/<meta http-equiv="Content-Security-Policy"[^>]*>/, '')
      .replace('<script src="bridge.js"></script>', url.searchParams.get('native') === '0' ? '<script src="bridge.js"></script>' : '<script src="bridge.js"></script><script src="native-preview-init.js"></script>'));
  }
  res.setHeader('Content-Type', { '.js': 'text/javascript', '.css': 'text/css', '.woff2': 'font/woff2' }[path.extname(file)] || 'application/octet-stream');
  fs.createReadStream(file).pipe(res);
}).listen(8176, '127.0.0.1', () => console.log('Native background preview: http://127.0.0.1:8176'));
