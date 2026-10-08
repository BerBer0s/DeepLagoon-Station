// Camera math for the technology tree. Pure; the screen position of a world point is
// `world * scale + (x, y)`, relative to the viewport's top-left corner.

export type Camera = { x: number; y: number; scale: number };
export type Size = { width: number; height: number };
export type Point = { x: number; y: number };
export type Rect = { x0: number; y0: number; x1: number; y1: number };

export const MAX_SCALE = 1.5;
export const ZOOM_STEP = 1.25;
/** Smallest scale at which node labels are still comfortable to read. */
export const READABLE_SCALE = 0.7;

const FIT_PADDING = 48;
const MIN_SCALE_RATIO = 0.8;
// How far past the tree's edge the view center may travel, as a share of the viewport.
const OVERSCROLL = 0.4;

const clamp = (value: number, low: number, high: number) =>
  Math.min(high, Math.max(low, value));

export const rectWidth = (rect: Rect) => rect.x1 - rect.x0;
export const rectHeight = (rect: Rect) => rect.y1 - rect.y0;
export const rectArea = (rect: Rect) => rectWidth(rect) * rectHeight(rect);

export const rectsIntersect = (a: Rect, b: Rect) =>
  a.x0 < b.x1 && a.x1 > b.x0 && a.y0 < b.y1 && a.y1 > b.y0;

export const containsRect = (outer: Rect, inner: Rect) =>
  outer.x0 <= inner.x0 &&
  outer.y0 <= inner.y0 &&
  outer.x1 >= inner.x1 &&
  outer.y1 >= inner.y1;

export const unionRect = (a: Rect, b: Rect): Rect => ({
  x0: Math.min(a.x0, b.x0),
  y0: Math.min(a.y0, b.y0),
  x1: Math.max(a.x1, b.x1),
  y1: Math.max(a.y1, b.y1),
});

/** Grows a rect by `share` of its own size on every side. */
export const expandRect = (rect: Rect, share: number): Rect => {
  const dx = rectWidth(rect) * share;
  const dy = rectHeight(rect) * share;
  return { x0: rect.x0 - dx, y0: rect.y0 - dy, x1: rect.x1 + dx, y1: rect.y1 + dy };
};

export const viewRect = (camera: Camera, viewport: Size): Rect => ({
  x0: -camera.x / camera.scale,
  y0: -camera.y / camera.scale,
  x1: (viewport.width - camera.x) / camera.scale,
  y1: (viewport.height - camera.y) / camera.scale,
});

export const fitScale = (rect: Rect, viewport: Size) =>
  Math.min(
    (viewport.width - 2 * FIT_PADDING) / Math.max(rectWidth(rect), 1),
    (viewport.height - 2 * FIT_PADDING) / Math.max(rectHeight(rect), 1),
  );

const minScaleFor = (tree: Size, viewport: Size) =>
  Math.min(
    fitScale({ x0: 0, y0: 0, x1: tree.width, y1: tree.height }, viewport),
    1,
  ) * MIN_SCALE_RATIO;

export const clampCamera = (camera: Camera, tree: Size, viewport: Size): Camera => {
  const scale = clamp(camera.scale, minScaleFor(tree, viewport), MAX_SCALE);
  const slackX = viewport.width * OVERSCROLL;
  const slackY = viewport.height * OVERSCROLL;
  return {
    scale,
    x: clamp(camera.x, viewport.width / 2 - tree.width * scale - slackX, viewport.width / 2 + slackX),
    y: clamp(camera.y, viewport.height / 2 - tree.height * scale - slackY, viewport.height / 2 + slackY),
  };
};

/** Zooms by `factor` keeping the world point under `anchor` (viewport pixels) fixed. */
export const zoomAt = (camera: Camera, anchor: Point, factor: number): Camera => {
  const scale = camera.scale * factor;
  return {
    scale,
    x: anchor.x - ((anchor.x - camera.x) * scale) / camera.scale,
    y: anchor.y - ((anchor.y - camera.y) * scale) / camera.scale,
  };
};

export const centerOn = (center: Point, scale: number, viewport: Size): Camera => ({
  scale,
  x: viewport.width / 2 - center.x * scale,
  y: viewport.height / 2 - center.y * scale,
});

export const fitRect = (rect: Rect, viewport: Size): Camera =>
  centerOn(
    { x: (rect.x0 + rect.x1) / 2, y: (rect.y0 + rect.y1) / 2 },
    Math.min(fitScale(rect, viewport), MAX_SCALE),
    viewport,
  );
