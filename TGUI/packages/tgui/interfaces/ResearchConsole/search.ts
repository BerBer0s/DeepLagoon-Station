// Search over technology names and the names of the recipes they unlock. Pure.
// Matching is the shared fuzzy search (../fuzzySearch.ts): case, "ё", words in any order, typos in names.

import {
  FUZZY_BELOW_COUNT,
  match,
  normalize,
  prepareQuery,
  prepareText,
  type PreparedText,
} from '../fuzzySearch';
import type { Tech } from './model';

export { normalize };

export type SearchEntry = {
  id: string;
  name: string;
  recipes: { name: string; text: PreparedText }[];
  text: PreparedText;
};

export type SearchResult = {
  id: string;
  /** The recipe the technology was found by; null when its own name matched. */
  recipe: string | null;
};

export const buildSearchIndex = (techs: Tech[]): SearchEntry[] =>
  techs.map((tech) => ({
    id: tech.id,
    name: tech.name,
    text: prepareText(tech.name),
    recipes: tech.recipes.map((recipe) => ({
      name: recipe.name,
      text: prepareText(recipe.name),
    })),
  }));

// Lower is better: a name (its own tiers), then recipes, then names found only with typos. Recipes sit
// between the two, as a typo never ranks above a plain match.
const RECIPE_OFFSET = 100;
const FUZZY_OFFSET = 200;

type Match = { result: SearchResult; rank: number; name: string };

/** All matches, best first; empty for an empty query. */
export const searchTechs = (index: SearchEntry[], text: string): SearchResult[] => {
  const query = prepareQuery(text);
  if (query.normalized === '') {
    return [];
  }
  const matches: Match[] = [];
  const rest: SearchEntry[] = [];
  for (const entry of index) {
    const nameRank = match(query, entry.text, false);
    if (nameRank !== null) {
      matches.push({ result: { id: entry.id, recipe: null }, rank: nameRank, name: entry.text.normalized });
      continue;
    }
    let best: { name: string; rank: number } | null = null;
    for (const recipe of entry.recipes) {
      const recipeRank = match(query, recipe.text, false);
      if (recipeRank !== null && (best === null || recipeRank < best.rank)) {
        best = { name: recipe.name, rank: recipeRank };
      }
    }
    if (best) {
      matches.push({
        result: { id: entry.id, recipe: best.name },
        rank: RECIPE_OFFSET + best.rank,
        name: entry.text.normalized,
      });
    } else {
      rest.push(entry);
    }
  }
  if (matches.length < FUZZY_BELOW_COUNT && query.canFuzzy) {
    for (const entry of rest) {
      const nameRank = match(query, entry.text);
      if (nameRank !== null) {
        matches.push({
          result: { id: entry.id, recipe: null },
          rank: FUZZY_OFFSET + nameRank,
          name: entry.text.normalized,
        });
      }
    }
  }
  return matches
    .sort((a, b) => a.rank - b.rank || a.name.localeCompare(b.name))
    .map((found) => found.result);
};
