// Where the camera looks when the tree opens, on Recenter and on Fit. Pure.

import {
  type Camera,
  centerOn,
  clampCamera,
  FAR_SCALE,
  fitRect,
  fitScale,
  MAX_SCALE,
  READABLE_SCALE,
  type Rect,
  type Size,
} from './camera';
import { NODE_HEIGHT, NODE_WIDTH } from './layout';
import type { TechState } from './model';
import type { TreeModel, TreeNode } from './tree';

// Technologies of one group that sit in neighboring columns, less than this far apart on the
// other axis, form one cluster: a group is drawn in several places, one per cluster.
const CLUSTER_LINK_X = NODE_WIDTH;
const CLUSTER_LINK_Y = 3 * NODE_HEIGHT;

// What the player can do next. When nothing is, the roots are shown.
const NEXT_STATES: TechState[] = ['available', 'unaffordable'];

const boundsOf = (nodes: TreeNode[]): Rect => ({
  x0: Math.min(...nodes.map((node) => node.x)),
  y0: Math.min(...nodes.map((node) => node.y)),
  x1: Math.max(...nodes.map((node) => node.x + node.width)),
  y1: Math.max(...nodes.map((node) => node.y + node.height)),
});

const centerOf = (node: TreeNode) => ({
  x: node.x + node.width / 2,
  y: node.y + node.height / 2,
});

const focusNodes = (tree: TreeModel, states: Map<string, TechState>) => {
  const next = tree.nodes.filter((node) =>
    NEXT_STATES.includes(states.get(node.id) ?? 'locked'),
  );
  if (next.length > 0) {
    return next;
  }
  const left = Math.min(...tree.nodes.map((node) => node.x));
  return tree.nodes.filter((node) => node.x === left);
};

// The nodes that fit together into one screen-sized window at the readable scale, as many as
// possible. Averaging all of them instead would land between distant clusters, on empty space.
const densestWindow = (nodes: TreeNode[], viewport: Size) => {
  const width = viewport.width / READABLE_SCALE;
  const height = viewport.height / READABLE_SCALE;
  let best: TreeNode[] = [];
  for (const left of nodes) {
    for (const top of nodes) {
      const inside = nodes.filter(
        (node) =>
          node.x >= left.x &&
          node.x + node.width <= left.x + width &&
          node.y >= top.y &&
          node.y + node.height <= top.y + height,
      );
      if (inside.length > best.length) {
        best = inside;
      }
    }
  }
  return best.length > 0 ? best : nodes.slice(0, 1);
};

export const fitCamera = (tree: TreeModel, viewport: Size): Camera =>
  clampCamera(
    fitRect({ x0: 0, y0: 0, x1: tree.width, y1: tree.height }, viewport),
    tree,
    viewport,
  );

/** The first view: the largest group of next steps that fits on screen, else the roots. */
export const homeCamera = (
  tree: TreeModel,
  states: Map<string, TechState>,
  viewport: Size,
): Camera => {
  if (tree.nodes.length === 0 || viewport.width <= 0 || viewport.height <= 0) {
    return { x: 0, y: 0, scale: 1 };
  }
  const bounds = boundsOf(densestWindow(focusNodes(tree, states), viewport));
  const scale = Math.min(1, Math.max(READABLE_SCALE, fitScale(bounds, viewport)));
  return clampCamera(
    centerOn(
      { x: (bounds.x0 + bounds.x1) / 2, y: (bounds.y0 + bounds.y1) / 2 },
      scale,
      viewport,
    ),
    tree,
    viewport,
  );
};

/** Recenter: the selected technology if there is one, otherwise the first view. */
export const recenterCamera = (
  tree: TreeModel,
  states: Map<string, TechState>,
  viewport: Size,
  current: Camera,
  selectedId: string | null,
): Camera => {
  const selected = selectedId ? tree.nodeById.get(selectedId) : undefined;
  if (!selected) {
    return homeCamera(tree, states, viewport);
  }
  const scale = Math.min(MAX_SCALE, Math.max(current.scale, READABLE_SCALE));
  return clampCamera(centerOn(centerOf(selected), scale, viewport), tree, viewport);
};

// How much of the free view around a technology must be clear for it to count as in view: its hint
// hangs below it, and the toolbar sits above it, in the corner.
const REVEAL_MARGIN = { left: 24, right: 24, top: 48, bottom: 72 };

/** The camera moved as little as needed to bring a technology into the free view; unchanged if it is. */
export const revealCamera = (
  tree: TreeModel,
  id: string,
  current: Camera,
  viewport: Size,
): Camera => {
  const node = tree.nodeById.get(id);
  if (!node) {
    return current;
  }
  const left = node.x * current.scale + current.x;
  const top = node.y * current.scale + current.y;
  const right = left + node.width * current.scale;
  const bottom = top + node.height * current.scale;
  const shift = (low: number, high: number, start: number, end: number) => {
    if (low < start) {
      return start - low;
    }
    return high > end ? end - high : 0;
  };
  const dx = shift(left, right, REVEAL_MARGIN.left, viewport.width - REVEAL_MARGIN.right);
  const dy = shift(top, bottom, REVEAL_MARGIN.top, viewport.height - REVEAL_MARGIN.bottom);
  return dx === 0 && dy === 0 ? current : { ...current, x: current.x + dx, y: current.y + dy };
};

const gapBetween = (a: TreeNode, b: TreeNode) => ({
  x: Math.max(0, Math.max(a.x, b.x) - Math.min(a.x + a.width, b.x + b.width)),
  y: Math.max(0, Math.max(a.y, b.y) - Math.min(a.y + a.height, b.y + b.height)),
});

// Single-linkage clusters, the biggest first (ties: the leftmost, then the topmost).
const clustersOf = (nodes: TreeNode[]): TreeNode[][] => {
  const parent = nodes.map((_, index) => index);
  const root = (index: number): number =>
    parent[index] === index ? index : (parent[index] = root(parent[index]));
  nodes.forEach((a, i) =>
    nodes.slice(i + 1).forEach((b, offset) => {
      const gap = gapBetween(a, b);
      if (gap.x <= CLUSTER_LINK_X && gap.y <= CLUSTER_LINK_Y) {
        parent[root(i)] = root(i + 1 + offset);
      }
    }),
  );
  const groups = new Map<number, TreeNode[]>();
  nodes.forEach((node, index) =>
    groups.set(root(index), [...(groups.get(root(index)) ?? []), node]),
  );
  return [...groups.values()].sort(
    (a, b) =>
      b.length - a.length ||
      Math.min(...a.map((n) => n.x)) - Math.min(...b.map((n) => n.x)) ||
      Math.min(...a.map((n) => n.y)) - Math.min(...b.map((n) => n.y)),
  );
};

/**
 * The view for a group of technologies, such as a discipline. If everything fits on screen with
 * readable labels, all of it; otherwise only the biggest cluster, at no less than that scale.
 */
export const groupCamera = (tree: TreeModel, ids: string[], viewport: Size): Camera => {
  const nodes = ids.flatMap((id) => tree.nodeById.get(id) ?? []);
  if (nodes.length === 0) {
    return homeCamera(tree, new Map(), viewport);
  }
  const all = boundsOf(nodes);
  const bounds = fitScale(all, viewport) >= FAR_SCALE ? all : boundsOf(clustersOf(nodes)[0]);
  const scale = Math.min(1, Math.max(FAR_SCALE, fitScale(bounds, viewport)));
  return clampCamera(
    centerOn({ x: (bounds.x0 + bounds.x1) / 2, y: (bounds.y0 + bounds.y1) / 2 }, scale, viewport),
    tree,
    viewport,
  );
};
