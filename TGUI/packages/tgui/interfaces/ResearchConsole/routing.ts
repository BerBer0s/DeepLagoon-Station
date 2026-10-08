// Orthogonal routing of the layout's line pieces. Pure; no React or DOM.
//
// Every piece joins an item in one layer to an item in the next one. Pieces that leave the same
// item share a vertical channel in the gap between the layers (an output bus); pieces that end in
// the same item meet on its last horizontal run (an input bus). Lane items are passed through
// horizontally, so a line never crosses a node.

import { type Layout, type LayoutItem, NODE_WIDTH } from './layout';

const MIN_GAP = 72;
const MAX_GAP = 360;
const CHANNEL_PAD = 24;
const CHANNEL_PITCH = 10;
const CORNER_RADIUS = 10;
const FLAT_EPSILON = 0.5;

export type RoutedUnit = {
  /** Path data with rounded corners. */
  d: string;
  x0: number;
  y0: number;
  x1: number;
  y1: number;
  /** Indices into `Layout.edges` of the technology edges this piece belongs to. */
  uses: number[];
};

export type Routing = {
  /** Left edge of each layer's column. */
  columnX: number[];
  width: number;
  units: RoutedUnit[];
};

type Source = {
  item: LayoutItem;
  targets: number[];
  spanMin: number;
  spanMax: number;
};

const within = (value: number, low: number, high: number) =>
  value > low && value < high;

// Crossings between two channels when `left` is the channel closer to the source layer.
const pairCost = (left: Source, right: Source) =>
  (within(right.item.y, left.spanMin, left.spanMax) ? 1 : 0) +
  left.targets.filter((y) => within(y, right.spanMin, right.spanMax)).length;

const orderChannels = (sources: Source[]) => {
  const order = [...sources];
  for (let pass = 0; pass < order.length; pass++) {
    let changed = false;
    for (let i = 0; i + 1 < order.length; i++) {
      if (pairCost(order[i + 1], order[i]) < pairCost(order[i], order[i + 1])) {
        [order[i], order[i + 1]] = [order[i + 1], order[i]];
        changed = true;
      }
    }
    if (!changed) {
      break;
    }
  }
  return order;
};

const round = (value: number) => Math.round(value * 10) / 10;

const roundedPath = (points: [number, number][]) => {
  let d = `M${round(points[0][0])} ${round(points[0][1])}`;
  for (let i = 1; i < points.length - 1; i++) {
    const [px, py] = points[i - 1];
    const [x, y] = points[i];
    const [nx, ny] = points[i + 1];
    const before = Math.hypot(x - px, y - py);
    const after = Math.hypot(nx - x, ny - y);
    const radius = Math.min(CORNER_RADIUS, before / 2, after / 2);
    d +=
      `L${round(x - ((x - px) / before) * radius)} ${round(y - ((y - py) / before) * radius)}` +
      `Q${round(x)} ${round(y)} ${round(x + ((nx - x) / after) * radius)} ${round(y + ((ny - y) / after) * radius)}`;
  }
  const last = points[points.length - 1];
  return `${d}L${round(last[0])} ${round(last[1])}`;
};

export const routeLayout = (layout: Layout): Routing => {
  const layerCount = layout.layers.length;
  const sourcesByLayer: Source[][] = Array.from({ length: layerCount }, () => []);
  for (const layer of layout.layers) {
    for (const item of layer) {
      if (item.succs.length === 0) {
        continue;
      }
      const targets = item.succs.map((succ) => succ.y);
      sourcesByLayer[item.layer].push({
        item,
        targets,
        spanMin: Math.min(item.y, ...targets),
        spanMax: Math.max(item.y, ...targets),
      });
    }
  }

  const columnX: number[] = [0];
  const channelX = new Map<LayoutItem, number>();
  for (let layer = 0; layer < layerCount; layer++) {
    const ordered = orderChannels(sourcesByLayer[layer]);
    const count = ordered.length;
    const pitch =
      count > 1
        ? Math.min(CHANNEL_PITCH, (MAX_GAP - 2 * CHANNEL_PAD) / (count - 1))
        : 0;
    const gap = Math.min(
      MAX_GAP,
      Math.max(MIN_GAP, 2 * CHANNEL_PAD + (count - 1) * pitch),
    );
    const gapStart = columnX[layer] + NODE_WIDTH;
    const first = gapStart + (gap - (count - 1) * pitch) / 2;
    ordered.forEach((source, index) => {
      channelX.set(source.item, first + index * pitch);
    });
    columnX.push(gapStart + gap);
  }

  const units: RoutedUnit[] = layout.units.map((unit) => {
    const { from, to } = unit;
    const x0 = columnX[from.layer] + NODE_WIDTH;
    // A lane item is a pass-through: the piece runs on across its whole column.
    const x1 = columnX[to.layer] + (to.nodeId === null ? NODE_WIDTH : 0);
    const channel = channelX.get(from) ?? x0;
    const points: [number, number][] =
      Math.abs(from.y - to.y) < FLAT_EPSILON
        ? [[x0, from.y], [x1, from.y]]
        : [[x0, from.y], [channel, from.y], [channel, to.y], [x1, to.y]];
    return {
      d: roundedPath(points),
      x0: Math.min(...points.map(([x]) => x)),
      y0: Math.min(...points.map(([, y]) => y)),
      x1: Math.max(...points.map(([x]) => x)),
      y1: Math.max(...points.map(([, y]) => y)),
      uses: unit.uses,
    };
  });

  return {
    columnX,
    width: layerCount === 0 ? 0 : columnX[layerCount - 1] + NODE_WIDTH,
    units,
  };
};
