// Layered left-to-right layout of the technology graph. Pure and deterministic: the same input
// always gives the same output, and nothing here depends on React or the DOM.
//
// Steps: layer = longest path from a root; edges that skip layers are carried by one chain of
// lane items per source (so their lines get a lane of their own and never cross a node);
// order inside layers by barycenter sweeps with a crossing-count transpose pass, with the
// discipline as the secondary key; vertical coordinates by isotonic regression towards the
// neighbors' positions.

export const NODE_WIDTH = 224;
export const NODE_HEIGHT = 88;

const LANE_HEIGHT = 10;
const NODE_GAP = 20;
const LANE_GAP = 6;
const ORDER_SWEEPS = 24;
const ORDER_PATIENCE = 4;
const TRANSPOSE_PASSES = 12;
const COORDINATE_SWEEPS = 10;
const EPSILON = 1e-9;

export type LayoutNode = { id: string; group: string };
/** `from` is the prerequisite of `to`. */
export type LayoutEdge = { from: string; to: string };

export type LayoutItem = {
  key: string;
  /** Technology id, or null for a lane item that only carries a line through a layer. */
  nodeId: string | null;
  layer: number;
  group: number;
  height: number;
  /** Vertical center. */
  y: number;
  order: number;
  preds: LayoutItem[];
  succs: LayoutItem[];
};

/** A line piece between two adjacent layers. `uses` are indices into `Layout.edges`. */
export type LayoutUnit = { from: LayoutItem; to: LayoutItem; uses: number[] };

export type Layout = {
  layers: LayoutItem[][];
  items: LayoutItem[];
  nodeItems: Map<string, LayoutItem>;
  units: LayoutUnit[];
  /** Accepted technology edges (known endpoints, no cycles). */
  edges: LayoutEdge[];
  height: number;
};

const edgeKey = (from: string, to: string) => `${from}\u0000${to}`;

const compareStrings = (a: string, b: string) => (a < b ? -1 : a > b ? 1 : 0);

const mean = (values: number[]) =>
  values.reduce((sum, value) => sum + value, 0) / values.length;

const isNode = (item: LayoutItem) => item.nodeId !== null;

const separation = (a: LayoutItem, b: LayoutItem) =>
  (a.height + b.height) / 2 + (isNode(a) && isNode(b) ? NODE_GAP : LANE_GAP);

const assignLayers = (nodes: LayoutNode[], preds: Map<string, string[]>) => {
  const layerOf = new Map<string, number>();
  const visiting = new Set<string>();
  const dropped = new Set<string>();
  const resolve = (id: string): number => {
    const known = layerOf.get(id);
    if (known !== undefined) {
      return known;
    }
    visiting.add(id);
    let layer = 0;
    for (const prerequisite of preds.get(id) ?? []) {
      if (visiting.has(prerequisite)) {
        dropped.add(edgeKey(prerequisite, id));
      } else {
        layer = Math.max(layer, resolve(prerequisite) + 1);
      }
    }
    visiting.delete(id);
    layerOf.set(id, layer);
    return layer;
  };
  for (const node of nodes) {
    resolve(node.id);
  }
  return { layerOf, dropped };
};

class Fenwick {
  private readonly tree: number[];
  constructor(size: number) {
    this.tree = new Array<number>(size + 1).fill(0);
  }
  add(index: number) {
    for (let i = index + 1; i < this.tree.length; i += i & -i) {
      this.tree[i]++;
    }
  }
  /** Number of added indices that are <= index. */
  countUpTo(index: number) {
    let total = 0;
    for (let i = index + 1; i > 0; i -= i & -i) {
      total += this.tree[i];
    }
    return total;
  }
}

const countCrossings = (units: LayoutUnit[], targetCount: number) => {
  const sorted = [...units].sort(
    (a, b) => a.from.order - b.from.order || a.to.order - b.to.order,
  );
  const placed = new Fenwick(targetCount);
  let crossings = 0;
  let inserted = 0;
  let start = 0;
  while (start < sorted.length) {
    let end = start;
    while (
      end < sorted.length &&
      sorted[end].from.order === sorted[start].from.order
    ) {
      crossings += inserted - placed.countUpTo(sorted[end].to.order);
      end++;
    }
    for (let i = start; i < end; i++) {
      placed.add(sorted[i].to.order);
      inserted++;
    }
    start = end;
  }
  return crossings;
};

export const computeLayout = (
  inputNodes: LayoutNode[],
  inputEdges: LayoutEdge[],
): Layout => {
  const nodes = [...inputNodes].sort((a, b) => compareStrings(a.id, b.id));
  const known = new Set(nodes.map((node) => node.id));
  const seen = new Set<string>();
  const candidates = inputEdges
    .filter(
      (edge) =>
        edge.from !== edge.to &&
        known.has(edge.from) &&
        known.has(edge.to) &&
        !seen.has(edgeKey(edge.from, edge.to)) &&
        seen.add(edgeKey(edge.from, edge.to)),
    )
    .sort((a, b) => compareStrings(a.to, b.to) || compareStrings(a.from, b.from));

  const candidatePreds = new Map<string, string[]>();
  for (const edge of candidates) {
    const list = candidatePreds.get(edge.to) ?? [];
    list.push(edge.from);
    candidatePreds.set(edge.to, list);
  }
  const { layerOf, dropped } = assignLayers(nodes, candidatePreds);
  const edges = candidates.filter(
    (edge) => !dropped.has(edgeKey(edge.from, edge.to)),
  );

  const groupNames = [...new Set(nodes.map((node) => node.group))].sort(compareStrings);
  const groupIndex = new Map(groupNames.map((name, index) => [name, index]));

  const makeItem = (
    key: string,
    nodeId: string | null,
    layer: number,
    group: number,
  ): LayoutItem => ({
    key,
    nodeId,
    layer,
    group,
    height: nodeId === null ? LANE_HEIGHT : NODE_HEIGHT,
    y: 0,
    order: 0,
    preds: [],
    succs: [],
  });

  const nodeItems = new Map<string, LayoutItem>();
  for (const node of nodes) {
    nodeItems.set(
      node.id,
      makeItem(node.id, node.id, layerOf.get(node.id) ?? 0, groupIndex.get(node.group) ?? 0),
    );
  }

  // One chain of lane items per source, as long as its farthest target needs.
  const lastTargetLayer = new Map<string, number>();
  for (const edge of edges) {
    const layer = layerOf.get(edge.to) ?? 0;
    lastTargetLayer.set(edge.from, Math.max(lastTargetLayer.get(edge.from) ?? 0, layer));
  }
  const lanes = new Map<string, LayoutItem>();
  const laneKey = (source: string, layer: number) => `${source}#${layer}`;
  for (const node of nodes) {
    const source = nodeItems.get(node.id)!;
    for (let layer = source.layer + 1; layer < (lastTargetLayer.get(node.id) ?? 0); layer++) {
      lanes.set(laneKey(node.id, layer), makeItem(laneKey(node.id, layer), null, layer, source.group));
    }
  }

  const unitByKey = new Map<string, LayoutUnit>();
  const units: LayoutUnit[] = [];
  const addUse = (from: LayoutItem, to: LayoutItem, use: number) => {
    const key = edgeKey(from.key, to.key);
    let unit = unitByKey.get(key);
    if (!unit) {
      unit = { from, to, uses: [] };
      unitByKey.set(key, unit);
      units.push(unit);
      from.succs.push(to);
      to.preds.push(from);
    }
    unit.uses.push(use);
  };
  edges.forEach((edge, use) => {
    const source = nodeItems.get(edge.from)!;
    const target = nodeItems.get(edge.to)!;
    let current = source;
    for (let layer = source.layer + 1; layer < target.layer; layer++) {
      const lane = lanes.get(laneKey(edge.from, layer))!;
      addUse(current, lane, use);
      current = lane;
    }
    addUse(current, target, use);
  });

  const items = [...nodeItems.values(), ...lanes.values()];
  const layerCount = items.reduce((count, item) => Math.max(count, item.layer + 1), 0);
  const layers: LayoutItem[][] = Array.from({ length: layerCount }, () => []);
  for (const item of items) {
    layers[item.layer].push(item);
  }
  const assignOrder = (layer: LayoutItem[]) =>
    layer.forEach((item, index) => {
      item.order = index;
    });
  for (const layer of layers) {
    layer.sort((a, b) => a.group - b.group || compareStrings(a.key, b.key));
    assignOrder(layer);
  }

  orderLayers(layers, units);
  placeVertically(layers);

  const top = Math.min(...items.map((item) => item.y - item.height / 2), 0);
  let height = 0;
  for (const item of items) {
    item.y -= top;
    height = Math.max(height, item.y + item.height / 2);
  }
  return {
    layers: layers.map((layer) => [...layer]),
    items,
    nodeItems,
    units,
    edges,
    height,
  };
};

const orderLayers = (layers: LayoutItem[][], units: LayoutUnit[]) => {
  if (layers.length < 2) {
    return;
  }
  const unitsByLayer: LayoutUnit[][] = Array.from(
    { length: layers.length - 1 },
    () => [],
  );
  for (const unit of units) {
    unitsByLayer[unit.from.layer].push(unit);
  }
  const totalCrossings = () =>
    unitsByLayer.reduce(
      (sum, gap, index) => sum + countCrossings(gap, layers[index + 1].length),
      0,
    );

  const sortByBarycenter = (layer: LayoutItem[], side: 'preds' | 'succs') => {
    const keyed = layer.map((item, index) => ({
      item,
      index,
      value: item[side].length > 0 ? mean(item[side].map((n) => n.order)) : index,
    }));
    keyed.sort((a, b) => {
      const difference = a.value - b.value;
      return (
        (Math.abs(difference) > EPSILON ? difference : 0) ||
        a.item.group - b.item.group ||
        a.index - b.index
      );
    });
    keyed.forEach(({ item }, index) => {
      layer[index] = item;
      item.order = index;
    });
  };

  const snapshot = () => layers.map((layer) => [...layer]);
  const restore = (saved: LayoutItem[][]) =>
    saved.forEach((layer, index) => {
      layers[index] = [...layer];
      layer.forEach((item, order) => {
        item.order = order;
      });
    });

  let best = snapshot();
  let bestCrossings = totalCrossings();
  let stale = 0;
  for (let sweep = 0; sweep < ORDER_SWEEPS && stale < ORDER_PATIENCE; sweep++) {
    if (sweep % 2 === 0) {
      for (let index = 1; index < layers.length; index++) {
        sortByBarycenter(layers[index], 'preds');
      }
    } else {
      for (let index = layers.length - 2; index >= 0; index--) {
        sortByBarycenter(layers[index], 'succs');
      }
    }
    const crossings = totalCrossings();
    if (crossings < bestCrossings) {
      bestCrossings = crossings;
      best = snapshot();
      stale = 0;
    } else {
      stale++;
    }
  }
  restore(best);
  transpose(layers);
};

// Number of crossings between the neighbor lines of two adjacent items on one side.
const pairCrossings = (upper: LayoutItem[], lower: LayoutItem[]) => {
  let count = 0;
  for (const a of upper) {
    for (const b of lower) {
      if (a.order > b.order) {
        count++;
      }
    }
  }
  return count;
};

const transpose = (layers: LayoutItem[][]) => {
  for (let pass = 0; pass < TRANSPOSE_PASSES; pass++) {
    let improved = false;
    for (const layer of layers) {
      for (let index = 0; index + 1 < layer.length; index++) {
        const a = layer[index];
        const b = layer[index + 1];
        const keep = pairCrossings(a.preds, b.preds) + pairCrossings(a.succs, b.succs);
        const swap = pairCrossings(b.preds, a.preds) + pairCrossings(b.succs, a.succs);
        if (swap < keep) {
          layer[index] = b;
          layer[index + 1] = a;
          a.order = index + 1;
          b.order = index;
          improved = true;
        }
      }
    }
    if (!improved) {
      break;
    }
  }
};

// Moves the items of one layer as little as possible (least squares) towards `desired`
// while keeping their order and the minimum separation.
const placeLayer = (layer: LayoutItem[], desired: number[]) => {
  const offsets: number[] = [];
  let offset = 0;
  layer.forEach((item, index) => {
    if (index > 0) {
      offset += separation(layer[index - 1], item);
    }
    offsets.push(offset);
  });
  const blocks: { sum: number; count: number }[] = [];
  desired.forEach((value, index) => {
    blocks.push({ sum: value - offsets[index], count: 1 });
    while (blocks.length >= 2) {
      const top = blocks[blocks.length - 1];
      const below = blocks[blocks.length - 2];
      if (below.sum / below.count <= top.sum / top.count) {
        break;
      }
      blocks.splice(blocks.length - 2, 2, {
        sum: below.sum + top.sum,
        count: below.count + top.count,
      });
    }
  });
  let index = 0;
  for (const block of blocks) {
    for (let i = 0; i < block.count; i++, index++) {
      layer[index].y = block.sum / block.count + offsets[index];
    }
  }
};

const placeVertically = (layers: LayoutItem[][]) => {
  for (const layer of layers) {
    placeLayer(layer, layer.map(() => 0));
  }
  for (let sweep = 0; sweep < COORDINATE_SWEEPS; sweep++) {
    const forward = sweep % 2 === 0;
    const indices = layers.map((_, index) => index);
    if (!forward) {
      indices.reverse();
    }
    for (const index of indices) {
      const layer = layers[index];
      placeLayer(
        layer,
        layer.map((item) => {
          const first = forward ? item.preds : item.succs;
          const second = forward ? item.succs : item.preds;
          const neighbors = first.length > 0 ? first : second;
          return neighbors.length > 0 ? mean(neighbors.map((n) => n.y)) : item.y;
        }),
      );
    }
  }
};
