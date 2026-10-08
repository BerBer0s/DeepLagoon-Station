// Where the camera looks when the tree opens, on Recenter and on Fit. Pure.

import {
  type Camera,
  centerOn,
  clampCamera,
  fitRect,
  fitScale,
  MAX_SCALE,
  READABLE_SCALE,
  type Rect,
  type Size,
} from './camera';
import type { TechState } from './model';
import type { TreeModel, TreeNode } from './tree';

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
