// Everything about the tree that depends only on the technologies and not on their state:
// positions, routed lines and the edge list. Built once per set of technologies and cached.

import { computeLayout, type LayoutEdge, NODE_HEIGHT, NODE_WIDTH } from './layout';
import type { Tech, TechState } from './model';
import { type RoutedUnit, routeLayout } from './routing';

export type TreeNode = {
  id: string;
  x: number;
  y: number;
  width: number;
  height: number;
};

export type TreeModel = {
  nodes: TreeNode[];
  nodeById: Map<string, TreeNode>;
  /** Technology edges; `from` is the prerequisite. Indexed by `RoutedUnit.uses`. */
  edges: LayoutEdge[];
  units: RoutedUnit[];
  width: number;
  height: number;
};

export type EdgeState = 'locked' | 'open' | 'done';

const EDGE_RANK: Record<EdgeState, number> = { locked: 0, open: 1, done: 2 };

let cachedKey = '';
let cachedModel: TreeModel | undefined;

const signature = (techs: Tech[]) =>
  techs
    .map((tech) => `${tech.id}:${tech.discipline}:${tech.prerequisites.join('+')}`)
    .join('|');

export const buildTree = (techs: Tech[]): TreeModel => {
  const key = signature(techs);
  if (cachedModel && key === cachedKey) {
    return cachedModel;
  }
  const layout = computeLayout(
    techs.map((tech) => ({ id: tech.id, group: tech.discipline })),
    techs.flatMap((tech) =>
      tech.prerequisites.map((from) => ({ from, to: tech.id })),
    ),
  );
  const routing = routeLayout(layout);
  const nodes: TreeNode[] = [];
  for (const [id, item] of layout.nodeItems) {
    nodes.push({
      id,
      x: routing.columnX[item.layer],
      y: item.y - NODE_HEIGHT / 2,
      width: NODE_WIDTH,
      height: NODE_HEIGHT,
    });
  }
  cachedKey = key;
  cachedModel = {
    nodes,
    nodeById: new Map(nodes.map((node) => [node.id, node])),
    edges: layout.edges,
    units: routing.units,
    width: routing.width,
    height: layout.height,
  };
  return cachedModel;
};

/** State of each routed piece: the strongest state among the technology edges that use it. */
export const unitStates = (
  tree: TreeModel,
  states: Map<string, TechState>,
): EdgeState[] => {
  const edgeStates = tree.edges.map((edge): EdgeState => {
    if (states.get(edge.from) !== 'researched') {
      return 'locked';
    }
    return states.get(edge.to) === 'researched' ? 'done' : 'open';
  });
  return tree.units.map((unit) =>
    unit.uses.reduce<EdgeState>(
      (best, use) => (EDGE_RANK[edgeStates[use]] > EDGE_RANK[best] ? edgeStates[use] : best),
      'locked',
    ),
  );
};
