// What the list shows: the recipes of a category that match the search, either flat and in the order of
// the match, or (without a search, in all categories) grouped under category headers.

import { rank } from '../fuzzySearch';
import { compareNames, NO_CATEGORY, type Recipe } from './model';

export const ROW_HEIGHT = 46;
export const HEADER_HEIGHT = 26;

export type ListItem =
  | { kind: 'header'; key: string; height: number; name: string; count: number }
  | { kind: 'recipe'; key: string; height: number; recipe: Recipe };

export const filterRecipes = (
  recipes: Recipe[],
  category: string | null,
  query: string,
  isAvailable: ((recipe: Recipe) => boolean) | null,
): Recipe[] => {
  let pool = recipes;
  if (category !== null) {
    pool = pool.filter((recipe) =>
      category === NO_CATEGORY ? recipe.cats.length === 0 : recipe.cats.includes(category),
    );
  }
  if (isAvailable) {
    pool = pool.filter(isAvailable);
  }
  return rank(pool, query, (recipe) => recipe.name);
};

export const groupRecipes = (
  recipes: Recipe[],
  categoryNames: Map<string, string>,
  noCategoryName: string,
): { name: string; recipes: Recipe[] }[] => {
  const groups = new Map<string, Recipe[]>();
  for (const recipe of recipes) {
    // The first category is the primary one; a recipe is listed once.
    const id = recipe.cats.find((category) => categoryNames.has(category)) ?? NO_CATEGORY;
    const group = groups.get(id);
    if (group) {
      group.push(recipe);
    } else {
      groups.set(id, [recipe]);
    }
  }
  return [...groups]
    .map(([id, members]) => ({
      id,
      name: id === NO_CATEGORY ? noCategoryName : (categoryNames.get(id) ?? id),
      recipes: members,
    }))
    .sort((a, b) =>
      a.id === b.id
        ? 0
        : a.id === NO_CATEGORY
          ? 1
          : b.id === NO_CATEGORY
            ? -1
            : compareNames(a.name, b.name),
    );
};

export const buildItems = (
  recipes: Recipe[],
  grouped: boolean,
  categoryNames: Map<string, string>,
  noCategoryName: string,
  heightOf: (recipe: Recipe) => number,
): ListItem[] => {
  if (!grouped) {
    return recipes.map((recipe) => ({
      kind: 'recipe',
      key: recipe.id,
      height: heightOf(recipe),
      recipe,
    }));
  }
  const items: ListItem[] = [];
  for (const group of groupRecipes(recipes, categoryNames, noCategoryName)) {
    items.push({
      kind: 'header',
      key: `header:${group.name}`,
      height: HEADER_HEIGHT,
      name: group.name,
      count: group.recipes.length,
    });
    for (const recipe of group.recipes) {
      items.push({ kind: 'recipe', key: recipe.id, height: heightOf(recipe), recipe });
    }
  }
  return items;
};
