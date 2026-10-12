export const initialView = { zoom: 1, x: 0, y: 0 };
export const canvasPoint = (point, view) => [
  .5 + (point[0] - .5 - view.x) / view.zoom,
  .5 + (point[1] - .5 - view.y) / view.zoom,
];
export const zoomAt = (view, point, factor) => {
  const zoom = Math.max(.5, Math.min(8, view.zoom * factor));
  const world = canvasPoint(point, view);
  return { zoom, x: point[0] - .5 - (world[0] - .5) * zoom,
    y: point[1] - .5 - (world[1] - .5) * zoom };
};
