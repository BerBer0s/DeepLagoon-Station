// Search over technology names and the names of the recipes they unlock. Pure.

import type { Tech } from './model';

export type SearchEntry = {
  id: string;
  name: string;
  recipes: { name: string; normalized: string }[];
  normalized: string;
};

export type SearchResult = {
  id: string;
  /** The recipe the technology was found by; null when its own name matched. */
  recipe: string | null;
};

/** Lower case, one space between words; "ё" is taken for "е", as people type it both ways. */
export const normalize = (text: string) =>
  text.toLowerCase().replace(/ё/g, 'е').replace(/\s+/g, ' ').trim();

export const buildSearchIndex = (techs: Tech[]): SearchEntry[] =>
  techs.map((tech) => ({
    id: tech.id,
    name: tech.name,
    normalized: normalize(tech.name),
    recipes: tech.recipes.map((recipe) => ({
      name: recipe.name,
      normalized: normalize(recipe.name),
    })),
  }));

// Lower is better: a name that starts with the query, a name that has it, then the same for recipes.
const RANK_NAME_START = 0;
const RANK_NAME = 1;
const RANK_RECIPE_START = 2;
const RANK_RECIPE = 3;

// Every word of the query has to be in the text, in any order.
const hasWords = (text: string, words: string[]) => words.every((word) => text.includes(word));

type Match = { result: SearchResult; rank: number; name: string };

const matchEntry = (entry: SearchEntry, query: string, words: string[]): Match | null => {
  if (hasWords(entry.normalized, words)) {
    return {
      result: { id: entry.id, recipe: null },
      rank: entry.normalized.startsWith(query) ? RANK_NAME_START : RANK_NAME,
      name: entry.normalized,
    };
  }
  const recipe = entry.recipes.find((candidate) => hasWords(candidate.normalized, words));
  if (!recipe) {
    return null;
  }
  return {
    result: { id: entry.id, recipe: recipe.name },
    rank: entry.recipes.some((candidate) => candidate.normalized.startsWith(query))
      ? RANK_RECIPE_START
      : RANK_RECIPE,
    name: entry.normalized,
  };
};

/** All matches, best first; empty for an empty query. */
export const searchTechs = (index: SearchEntry[], text: string): SearchResult[] => {
  const query = normalize(text);
  if (query === '') {
    return [];
  }
  const words = query.split(' ');
  return index
    .flatMap((entry) => matchEntry(entry, query, words) ?? [])
    .sort((a, b) => a.rank - b.rank || a.name.localeCompare(b.name))
    .map((match) => match.result);
};
