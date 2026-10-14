const size = 256;
export const makeCanvas = () => { const c = document.createElement('canvas'); c.width = c.height = size; return c; };
export const makeLayer = (name = 'Слой') => ({ name, visible: true, opacity: 1, useBase: false, strokes: [], normal: makeCanvas(), glow: makeCanvas() });
export function strokeWork(stroke) {
  let work = 0; const radius = Math.max(.5, stroke.width * size / 2);
  const bound = value => Math.max(0, Math.min(size, value));
  for (let i = 0; i < Math.max(1, stroke.points.length - 1); i++) {
    const a = stroke.points[i], b = stroke.points[Math.min(i + 1, stroke.points.length - 1)];
    work += (bound(Math.ceil(Math.max(a[0], b[0]) * size + radius + 1)) - bound(Math.floor(Math.min(a[0], b[0]) * size - radius - 1)))
      * (bound(Math.ceil(Math.max(a[1], b[1]) * size + radius + 1)) - bound(Math.floor(Math.min(a[1], b[1]) * size - radius - 1)));
  }
  return work;
}
export function paint(ctx, stroke, luminous) {
  const points = stroke.points;
  ctx.globalCompositeOperation = stroke.erase || stroke.glow !== luminous ? 'destination-out' : 'source-over';
  ctx.strokeStyle = ctx.fillStyle = stroke.color;
  ctx.lineWidth = stroke.width * size; ctx.lineJoin = ctx.lineCap = 'round';
  ctx.beginPath();
  if (points.length === 1) { ctx.arc(points[0][0] * size, points[0][1] * size, ctx.lineWidth / 2, 0, Math.PI * 2); ctx.fill(); }
  else { points.forEach((p, i) => i ? ctx.lineTo(p[0] * size, p[1] * size) : ctx.moveTo(p[0] * size, p[1] * size)); ctx.stroke(); }
  ctx.globalCompositeOperation = 'source-over';
}
export function flatten(layers, active = -1, draft = null) {
  const result = [makeCanvas(), makeCanvas()];
  layers.forEach((layer, index) => {
    if (!layer.visible) return;
    const channels = [layer.normal, layer.glow];
    const source = draft && index === active ? channels.map((c, i) => {
      const copy = makeCanvas(); const ctx = copy.getContext('2d'); ctx.drawImage(c, 0, 0); paint(ctx, draft, !!i); return copy;
    }) : channels;
    result.forEach((c, i) => {
      const ctx = c.getContext('2d'); ctx.globalAlpha = layer.opacity;
      ctx.globalCompositeOperation = 'destination-out'; ctx.drawImage(source[1 - i], 0, 0);
      ctx.globalCompositeOperation = 'source-over'; ctx.drawImage(source[i], 0, 0); ctx.globalAlpha = 1;
    });
  });
  return result;
}
export const cloneLayers = layers => layers.map(l => {
  const copy = makeLayer(l.name); copy.visible = l.visible; copy.opacity = l.opacity;
  copy.useBase = l.useBase; copy.strokes = l.strokes.map(s => ({ ...s, points: s.points.map(p => [...p]) }));
  copy.normal.getContext('2d').drawImage(l.normal, 0, 0); copy.glow.getContext('2d').drawImage(l.glow, 0, 0); return copy;
});
export async function loadBitmap(canvas, png) {
  if (!png) return;
  const image = new Image(); image.src = 'data:image/png;base64,' + png; await image.decode();
  canvas.getContext('2d').drawImage(image, 0, 0, size, size);
}
export function encodeBitmap(canvas) {
  const bytes = canvas.getContext('2d').getImageData(0, 0, size, size).data;
  if (!bytes.some((v, i) => i % 4 === 3 && v)) return '';
  return canvas.toDataURL('image/png').split(',')[1];
}
