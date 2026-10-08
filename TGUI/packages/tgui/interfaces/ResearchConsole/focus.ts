// Where the camera looks when the tree opens, on Recenter and on Fit. Pure.

import {
  type Camera,
  centerOn,
  clampCamera,
  fitRect,
  fitScale,
  READABLE_SCALE,
  type Rect,
  rectHeight,
  rectWidth,
  type Size,
} from './camera';
import type { TechState } from './model';
import type { TreeModel, TreeNode } from './tree';

// The first group that has any technologies decides the focus: what can be researched next,
// otherwise what has been researched.
const FOCUS_GROUPS: TechState[][] = [['available', 'unaffordable'], ['researched']];

const boundsOf = (nodes: TreeNode[]): Rect => ({
  x0: Math.min(...nodes.map((node) => node.x)),
  y0: Math.min(...nodes.map((node) => node.y)),
  x1: Math.max(...nodes.map((node) => node.x + node.width)),
  y1: Math.max(...nodes.map((node) => node.y + node.height)),
});

const focusNodes = (tree: TreeModel, states: Map<string, TechState>) => {
  for (const group of FOCUS_GROUPS) {
    const nodes = tree.nodes.filter((node) => group.includes(states.get(node.id) ?? 'locked'));
    if (nodes.length > 0) {
      return nodes;
    }
  }
  const left = Math.min(...tree.nodes.map((node) => node.x));
  return tree.nodes.filter((node) => node.x === left);
};

export const fitCamera = (tree: TreeModel, viewport: Size): Camera =>
  clampCamera(
    fitRect({ x0: 0, y0: 0, x1: tree.width, y1: tree.height }, viewport),
    tree,
    viewport,
  );

export const homeCamera = (
  tree: TreeModel,
  states: Map<string, TechState>,
  viewport: Size,
): Camera => {
  if (tree.nodes.length === 0 || viewport.width <= 0 || viewport.height <= 0) {
    return { x: 0, y: 0, scale: 1 };
  }
  const nodes = focusNodes(tree, states);
  const bounds = boundsOf(nodes);
  const scale = Math.min(1, Math.max(READABLE_SCALE, fitScale(bounds, viewport)));
  const fits =
    rectWidth(bounds) * scale <= viewport.width &&
    rectHeight(bounds) * scale <= viewport.height;
  const center = fits
    ? { x: (bounds.x0 + bounds.x1) / 2, y: (bounds.y0 + bounds.y1) / 2 }
    : {
        x: nodes.reduce((sum, node) => sum + node.x + node.width / 2, 0) / nodes.length,
        y: nodes.reduce((sum, node) => sum + node.y + node.height / 2, 0) / nodes.length,
      };
  return clampCamera(centerOn(center, scale, viewport), tree, viewport);
};
