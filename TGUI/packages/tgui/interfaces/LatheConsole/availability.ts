// Whether a recipe can be queued now, with the numbers behind it. Costs arrive adjusted by the host, so
// all arithmetic here is on whole numbers and matches the server's check (stock >= cost * amount).

import type { Recipe } from './model';

export type Stock = {
  materials: Map<string, number>;
  /** What the queue will still consume. */
  reserved: Map<string, number>;
  entities: Map<string, number>;
  reagents: Map<string, number>;
};

export const buildStock = (
  stock: { id: string; n: number }[] | undefined,
  reserved: { id: string; n: number }[] | undefined,
  entities: { id: string; n: number }[] | undefined,
  reagents: { id: string; n: number }[] | undefined,
): Stock => {
  const toMap = (list: { id: string; n: number }[] | undefined) =>
    new Map((list ?? []).map((entry) => [entry.id, entry.n]));
  return {
    materials: toMap(stock),
    reserved: toMap(reserved),
    entities: toMap(entities),
    reagents: toMap(reagents),
  };
};

/**
 * ok: it can be queued. short: something is missing now. later: there is enough now, but the queue
 * will use it first, and the server does not accept the recipe.
 */
export type Status = 'ok' | 'later' | 'short';

export type Need = {
  kind: 'material' | 'entity' | 'reagent';
  id: string;
  name: string;
  need: number;
  have: number;
  /** Stock left after the queue has taken its share (materials only). */
  free: number;
  status: Status;
};

export const needsOf = (recipe: Recipe, amount: number, stock: Stock): Need[] => {
  const needs: Need[] = [];
  for (const material of recipe.mats) {
    const need = material.n * amount;
    const have = stock.materials.get(material.id) ?? 0;
    const free = have - (stock.reserved.get(material.id) ?? 0);
    needs.push({
      kind: 'material',
      id: material.id,
      name: material.id,
      need,
      have,
      free,
      status: have < need ? 'short' : free < need ? 'later' : 'ok',
    });
  }
  for (const entity of recipe.ents) {
    const need = entity.n * amount;
    const have = stock.entities.get(entity.id) ?? 0;
    needs.push({
      kind: 'entity',
      id: entity.id,
      name: entity.name,
      need,
      have,
      free: have,
      status: have < need ? 'short' : 'ok',
    });
  }
  for (const reagent of recipe.reagents) {
    const need = reagent.n * amount;
    const have = stock.reagents.get(reagent.id) ?? 0;
    needs.push({
      kind: 'reagent',
      id: reagent.id,
      name: reagent.name,
      need,
      have,
      free: have,
      status: have < need - 1e-6 ? 'short' : 'ok',
    });
  }
  return needs;
};

export const worstStatus = (needs: Need[]): Status => {
  if (needs.some((need) => need.status === 'short')) {
    return 'short';
  }
  return needs.some((need) => need.status === 'later') ? 'later' : 'ok';
};

export const statusOf = (recipe: Recipe, amount: number, stock: Stock): Status =>
  worstStatus(needsOf(recipe, amount, stock));

/** The most of a recipe that can be queued right now; at least 0. */
export const maxAmount = (recipe: Recipe, stock: Stock, cap: number): number => {
  let most = cap;
  for (const need of needsOf(recipe, 1, stock)) {
    if (need.need <= 0) {
      continue;
    }
    const available = need.kind === 'material' ? need.free : need.have;
    most = Math.min(most, Math.floor((available + 1e-6) / need.need));
  }
  return Math.max(0, most);
};
