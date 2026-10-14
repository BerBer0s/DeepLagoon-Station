import { memo, useEffect, useRef } from 'react';
import { drawOrder, itemsInRect } from './gridEditorModel.mjs';

// Rasterize only the viewport: map dimensions never allocate a map-sized GPU texture.
// Cached image sources and the spatial index are reused while panning and zooming.
export const GridEditorScene = memo(({ index, area, layers, visuals, view, cellSize }) => {
  const canvas = useRef(null), images = useRef(new Map());
  useEffect(() => {
    let cancelled = false;
    const visible = itemsInRect(index, { x: area.x + view.left / cellSize,
      y: area.y + area.height - (view.top + view.height) / cellSize,
      width: view.width / cellSize, height: view.height / cellSize }).filter(item => layers[item.kind]);
    const visual = item => visuals.get(`${item.kind}:${item.prototype}`);
    const load = url => {
      if (!images.current.has(url)) images.current.set(url, new Promise(resolve => {
        const image = new Image();
        image.onload = () => resolve(image); image.onerror = () => resolve(null); image.src = url;
      }));
      return images.current.get(url);
    };
    const urls = [...new Set(visible.flatMap(item => (visual(item)?.images || []).map(image => image.url)))];
    Promise.all(urls.map(load)).then(loaded => {
      if (cancelled) return;
      const sources = new Map(urls.map((url, index) => [url, loaded[index]]));
      const context = canvas.current.getContext('2d');
      context.clearRect(0, 0, canvas.current.width, canvas.current.height);
      context.imageSmoothingEnabled = false;
      const tint = document.createElement('canvas'); tint.width = tint.height = 32;
      const tinted = tint.getContext('2d');
      // Stable sorting preserves the picker tie-break for overlapping sprites.
      visible.sort((a, b) => drawOrder(a, visual(a)) - drawOrder(b, visual(b)));
      for (const item of visible) {
        const offset = item.kind === 'entity' ? .5 : 0;
        const x = (item.x - offset - area.x) * cellSize - view.left;
        const y = (area.y + area.height - 1 - item.y + offset) * cellSize - view.top;
        context.save();
        context.translate(x + cellSize / 2, y + cellSize / 2);
        const rotateTile = item.kind === 'tile' && visual(item)?.allowRotationMirror;
        context.rotate(-(item.kind === 'tile' ? rotateTile ? (item.mirror & 3) * 90 : 0 : item.rotation) * Math.PI / 180);
        if (rotateTile && (item.mirror & 4)) context.scale(-1, 1);
        if (item.kind === 'tile' && !(visual(item)?.images || []).some(image => sources.get(image.url))) {
          context.fillStyle = '#3b454e'; context.fillRect(-cellSize / 2, -cellSize / 2, cellSize, cellSize);
        }
        context.globalAlpha = item.kind === 'decal' ? .85 : 1;
        for (const { url } of visual(item)?.images || []) {
          const image = sources.get(url); if (!image) continue;
          const scale = cellSize / Math.max(image.width, image.height), width = image.width * scale, height = image.height * scale;
          if (item.kind === 'decal') {
            tinted.clearRect(0, 0, 32, 32); tinted.globalCompositeOperation = 'source-over';
            const tintScale = 32 / Math.max(image.width, image.height);
            tinted.drawImage(image, (32 - image.width * tintScale) / 2, (32 - image.height * tintScale) / 2, image.width * tintScale, image.height * tintScale);
            tinted.globalCompositeOperation = 'source-in'; tinted.fillStyle = item.color; tinted.fillRect(0, 0, 32, 32);
            context.drawImage(tint, -cellSize / 2, -cellSize / 2, cellSize, cellSize);
          } else context.drawImage(image, -width / 2, -height / 2, width, height);
        }
        context.restore();
      }
    });
    return () => { cancelled = true; };
  }, [index, area, layers, visuals, view, cellSize]);
  return <canvas ref={canvas} className="GridEditor__scene" width={Math.max(1, view.width)} height={Math.max(1, view.height)}
    style={{ left: view.left, top: view.top, width: view.width, height: view.height }} aria-hidden="true" />;
});
