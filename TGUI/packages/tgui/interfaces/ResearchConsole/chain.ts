// The chain of a technology: everything it needs, directly or not, and everything it leads to.
// Pure; used to highlight nodes and lines.

import type { TreeModel } from './tree';

export type Chain = {
  nodes: Set<string>;
  /** Indices into `TreeModel.units`. A piece shared by several edges counts if any of them is in the chain. */
  units: Set<number>;
};

const neighbors = (pairs: [string, string][]) => {
  const map = new Map<string, string[]>();
  for (const [from, to] of pairs) {
    const list = map.get(from) ?? [];
    list.push(to);
    map.set(from, list);
  }
  return map;
};

const reach = (start: string, next: Map<string, string[]>) => {
  const seen = new Set([start]);
  const queue = [start];
  for (let index = 0; index < queue.length; index++) {
    for (const id of next.get(queue[index]) ?? []) {
      if (!seen.has(id)) {
        seen.add(id);
        queue.push(id);
      }
    }
  }
  return seen;
};

export const chainOf = (tree: TreeModel, id: string): Chain => {
  const up = reach(id, neighbors(tree.edges.map((edge) => [edge.to, edge.from])));
  const down = reach(id, neighbors(tree.edges.map((edge) => [edge.from, edge.to])));
  // A line between a prerequisite and a dependent that skips the technology itself is not part of it.
  const edges = new Set<number>();
  tree.edges.forEach((edge, index) => {
    if (
      (up.has(edge.from) && up.has(edge.to)) ||
      (down.has(edge.from) && down.has(edge.to))
    ) {
      edges.add(index);
    }
  });
  const units = new Set<number>();
  tree.units.forEach((unit, index) => {
    if (unit.uses.some((use) => edges.has(use))) {
      units.add(index);
    }
  });
  return { nodes: new Set([...up, ...down]), units };
};
