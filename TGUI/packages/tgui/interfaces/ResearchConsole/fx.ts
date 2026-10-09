// What changed between two sets of technology states, for the short animation after a research.
// Pure.

import type { TechState } from './model';
import type { TreeModel } from './tree';

/** More changes than this at once is a reload or a server switch, not a research. */
const MAX_CHANGES = 8;

export type ResearchFx = {
  /** Technologies that have just been researched. */
  snapped: Set<string>;
  /** Technologies whose prerequisites are now all researched. */
  woken: Set<string>;
  /** Indices into `TreeModel.units`: pieces from a snapped to a woken technology. */
  units: Set<number>;
};

const union = <T>(a: Set<T>, b: Set<T>) => new Set([...a, ...b]);

export const mergeFx = (a: ResearchFx, b: ResearchFx): ResearchFx => ({
  snapped: union(a.snapped, b.snapped),
  woken: union(a.woken, b.woken),
  units: union(a.units, b.units),
});

export const researchFx = (
  tree: TreeModel,
  previous: Map<string, TechState>,
  next: Map<string, TechState>,
): ResearchFx | null => {
  if (previous.size === 0) {
    return null;
  }
  const snapped = new Set<string>();
  const woken = new Set<string>();
  for (const [id, state] of next) {
    const before = previous.get(id);
    if (state === 'researched' && before !== undefined && before !== 'researched') {
      snapped.add(id);
    } else if (before === 'locked' && (state === 'available' || state === 'unaffordable')) {
      woken.add(id);
    }
  }
  if (snapped.size === 0 || snapped.size + woken.size > MAX_CHANGES) {
    return null;
  }
  const edges = new Set<number>();
  tree.edges.forEach((edge, index) => {
    if (snapped.has(edge.from) && woken.has(edge.to)) {
      edges.add(index);
    }
  });
  const units = new Set<number>();
  tree.units.forEach((unit, index) => {
    if (unit.uses.some((use) => edges.has(use))) {
      units.add(index);
    }
  });
  return { snapped, woken, units };
};
