// The cost columns of the list: one fixed column per material, so that a material is always in the same
// place. A machine with few materials shows them as wide columns; with many, as narrow ones, and what does
// not fit goes into one last column.

import type { Recipe, WireMaterial } from './model';

/** Up to this many materials the columns are wide. */
export const FULL_MAX_COLUMNS = 4;
/** Beyond that, this many narrow columns are shown. */
export const COMPACT_MAX_COLUMNS = 6;

export type CostColumn = { id: string; name: string; icon: string };

export type CostLayout = {
  compact: boolean;
  /** The most used materials first. */
  columns: CostColumn[];
  /** There is a last column for what has no column: other materials, entities, reagents. */
  other: boolean;
};

export const buildCostLayout = (
  recipes: Recipe[],
  materials: Map<string, WireMaterial>,
): CostLayout => {
  const counts = new Map<string, number>();
  for (const recipe of recipes) {
    for (const material of recipe.mats) {
      counts.set(material.id, (counts.get(material.id) ?? 0) + 1);
    }
  }
  const nameOf = (id: string) => materials.get(id)?.name ?? id;
  const ordered = [...counts.keys()].sort(
    (a, b) => (counts.get(b) ?? 0) - (counts.get(a) ?? 0) || nameOf(a).localeCompare(nameOf(b)),
  );
  const compact = ordered.length > FULL_MAX_COLUMNS;
  const shown = compact ? ordered.slice(0, COMPACT_MAX_COLUMNS) : ordered;
  const ids = new Set(shown);
  return {
    compact,
    columns: shown.map((id) => ({ id, name: nameOf(id), icon: materials.get(id)?.icon ?? '' })),
    other: recipes.some(
      (recipe) =>
        recipe.ents.length > 0 ||
        recipe.reagents.length > 0 ||
        recipe.mats.some((material) => !ids.has(material.id)),
    ),
  };
};
