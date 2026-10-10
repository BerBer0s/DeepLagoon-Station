// Case-insensitive search that forgives typos. A twin of Content.Client/_DeepLagoon/Search/FuzzySearch.cs:
// the algorithm, the tiers and the constants must stay equal in both, change them together.
//
// Normalization: lower case, "ё" is "е", whitespace runs collapse to one space.
// Every word of the query has to be found in the text, in any order.
//
// Tiers, best first (the rank is tier * 16 + typo cost, lower is better):
// exact (whole string), prefix (string starts with the query), word prefix, substring,
// the extra text (description), typos.
//
// Typos: only for query words of 4+ characters without digits, against text words of 4+ characters. One
// mistake is allowed for 4-6 characters, two for 7+. A mistake is an insert, a delete, a replace or a swap
// of neighbours (optimal string alignment distance). A word is also compared with the same-length start of
// a longer text word, so half-typed input still works. Typo matches are shown only when there are fewer than
// FUZZY_BELOW_COUNT better ones.
//
// Examples: "wsll" finds "Wall" (typo); "wall" finds "Wall"; "стена" finds "Стена"; "елка" finds "ёлка";
// "wal" finds "Wall" (prefix); "all" finds "Wall" (substring); "wl" does not find "Wall" (too short for typos).
//
// This file is not an interface: only .js, .jsx and .tsx files of this folder are routed.

/** Typos start at this length of a query word and of a text word. */
export const MIN_FUZZY_WORD_LENGTH = 4;

/** Query words of this length and longer may have two mistakes instead of one. */
export const LONG_WORD_LENGTH = 7;

/** Typo matches are shown only when there are fewer plain matches than this. */
export const FUZZY_BELOW_COUNT = 5;

export const TIER_EXACT = 0;
export const TIER_PREFIX = 1;
export const TIER_WORD_PREFIX = 2;
export const TIER_SUBSTRING = 3;
export const TIER_EXTRA = 4;
export const TIER_FUZZY = 5;

const TIER_MULTIPLIER = 16;
const MAX_COST = TIER_MULTIPLIER - 1;
const TEXT_CACHE_LIMIT = 32768;

export type PreparedQuery = {
  normalized: string;
  /** Words of the query, split on whitespace. They match in any order. */
  words: string[];
  /** True when at least one word is long enough for typo tolerance. */
  canFuzzy: boolean;
};

export type PreparedText = {
  normalized: string;
  /** Runs of letters and digits, so "(wall)" gives "wall". */
  words: string[];
};

/** Lower case, "ё" is "е", one space between words. */
export const normalize = (text: string | null | undefined): string =>
  (text ?? '').toLowerCase().replace(/ё/g, 'е').replace(/\s+/g, ' ').trim();

const DIGIT = /\p{Nd}/u;
const TOKEN = /[\p{L}\p{Nd}]+/gu;

/** A query word may have typos when it is long enough and has no digits (numbers and codes are exact). */
export const canFuzzyWord = (word: string): boolean =>
  word.length >= MIN_FUZZY_WORD_LENGTH && !DIGIT.test(word);

/** How many mistakes a query word of this length may have. */
export const tolerance = (wordLength: number): number => (wordLength >= LONG_WORD_LENGTH ? 2 : 1);

/** Build it once per text change. */
export const prepareQuery = (query: string | null | undefined): PreparedQuery => {
  const normalized = normalize(query);
  const words = normalized === '' ? [] : normalized.split(' ');
  return { normalized, words, canFuzzy: words.some(canFuzzyWord) };
};

const textCache = new Map<string, PreparedText>();

/** The prepared form of a text. Cached, so repeated calls for the same string are cheap. */
export const prepareText = (text: string | null | undefined): PreparedText => {
  const source = text ?? '';
  const cached = textCache.get(source);
  if (cached) {
    return cached;
  }
  const normalized = normalize(source);
  const prepared = { normalized, words: normalized.match(TOKEN) ?? [] };
  if (textCache.size >= TEXT_CACHE_LIMIT) {
    textCache.clear();
  }
  textCache.set(source, prepared);
  return prepared;
};

const makeRank = (tier: number, cost: number) => tier * TIER_MULTIPLIER + Math.min(cost, MAX_COST);

// Optimal string alignment distance of a[0..lengthA) and b[0..lengthB) (insert, delete, replace, swap of
// neighbours). Gives up above max and returns max + 1.
const distance = (a: string, b: string, lengthA: number, lengthB: number, max: number): number => {
  if (Math.abs(lengthA - lengthB) > max) {
    return max + 1;
  }
  // previous2 is row i-2, previous is row i-1, current is row i.
  let previous2: number[] = new Array(lengthB + 1);
  let previous: number[] = new Array(lengthB + 1);
  let current: number[] = new Array(lengthB + 1);
  for (let j = 0; j <= lengthB; j++) {
    previous[j] = j;
  }
  let previousMin = 0;
  for (let i = 1; i <= lengthA; i++) {
    current[0] = i;
    let rowMin = current[0];
    for (let j = 1; j <= lengthB; j++) {
      const substitution = a[i - 1] === b[j - 1] ? 0 : 1;
      let value = Math.min(
        Math.min(previous[j] + 1, current[j - 1] + 1),
        previous[j - 1] + substitution,
      );
      if (i > 1 && j > 1 && a[i - 1] === b[j - 2] && a[i - 2] === b[j - 1]) {
        value = Math.min(value, previous2[j - 2] + 1);
      }
      current[j] = value;
      rowMin = Math.min(rowMin, value);
    }
    // A swap reaches back two rows, so both of the last two rows have to be over the limit.
    if (rowMin > max && previousMin > max) {
      return max + 1;
    }
    previousMin = rowMin;
    const next = previous2;
    previous2 = previous;
    previous = current;
    current = next;
  }
  return previous[lengthB];
};

// The tier a single query word reaches in the text, or -1.
const matchWord = (
  word: string,
  text: PreparedText,
  allowFuzzy: boolean,
): { tier: number; cost: number } => {
  if (text.words.some((candidate) => candidate.startsWith(word))) {
    return { tier: TIER_WORD_PREFIX, cost: 0 };
  }
  if (text.normalized.includes(word)) {
    return { tier: TIER_SUBSTRING, cost: 0 };
  }
  if (!allowFuzzy || !canFuzzyWord(word)) {
    return { tier: -1, cost: 0 };
  }
  const allowed = tolerance(word.length);
  let best = allowed + 1;
  for (const candidate of text.words) {
    if (candidate.length < MIN_FUZZY_WORD_LENGTH) {
      continue;
    }
    if (Math.abs(candidate.length - word.length) <= allowed) {
      best = Math.min(
        best,
        distance(word, candidate, word.length, candidate.length, Math.min(best - 1, allowed)),
      );
    }
    // Half-typed input: compare with the start of a longer word.
    if (candidate.length > word.length) {
      best = Math.min(
        best,
        distance(word, candidate, word.length, word.length, Math.min(best - 1, allowed)),
      );
    }
  }
  if (best > allowed) {
    return { tier: -1, cost: 0 };
  }
  return { tier: TIER_FUZZY, cost: best };
};

/**
 * The rank of the text for the query (lower is better), or null when it does not match.
 * An empty query matches everything with rank 0.
 */
export const match = (
  query: PreparedQuery,
  text: PreparedText,
  allowFuzzy = true,
): number | null => {
  if (query.normalized === '') {
    return 0;
  }
  if (text.normalized === query.normalized) {
    return TIER_EXACT * TIER_MULTIPLIER;
  }
  if (text.normalized.startsWith(query.normalized)) {
    return TIER_PREFIX * TIER_MULTIPLIER;
  }
  let tier = TIER_WORD_PREFIX;
  let cost = 0;
  for (const word of query.words) {
    const found = matchWord(word, text, allowFuzzy);
    if (found.tier < 0) {
      return null;
    }
    tier = Math.max(tier, found.tier);
    cost += found.cost;
  }
  return makeRank(tier, cost);
};

/** Same as match for a plain string. */
export const matchString = (
  query: PreparedQuery,
  text: string | null | undefined,
  allowFuzzy = true,
): number | null => match(query, prepareText(text), allowFuzzy);

type Found<T> = { item: T; rank: number; index: number };

// Null for an empty query: everything matches.
const search = <T>(
  items: readonly T[],
  query: string | null | undefined,
  name: (item: T) => string | null | undefined,
  extra?: (item: T) => string | null | undefined,
): Found<T>[] | null => {
  const prepared = prepareQuery(query);
  if (prepared.normalized === '') {
    return null;
  }
  const matched: Found<T>[] = [];
  const rest: { item: T; index: number }[] = [];
  items.forEach((item, index) => {
    let rank = matchString(prepared, name(item), false);
    if (rank === null && extra && matchString(prepared, extra(item), false) !== null) {
      rank = makeRank(TIER_EXTRA, 0);
    }
    if (rank !== null) {
      matched.push({ item, rank, index });
    } else {
      rest.push({ item, index });
    }
  });
  if (matched.length < FUZZY_BELOW_COUNT && prepared.canFuzzy) {
    for (const { item, index } of rest) {
      const rank = matchString(prepared, name(item));
      if (rank !== null) {
        matched.push({ item, rank, index });
      }
    }
  }
  return matched;
};

/**
 * Keeps the items that match the query and orders them best first; items of one rank keep their incoming
 * order. An empty query returns the items unchanged, in the same order.
 * `name` may match with typos; `extra` (a description) matches only as a substring, without typos.
 */
export const rank = <T>(
  items: readonly T[],
  query: string | null | undefined,
  name: (item: T) => string | null | undefined,
  extra?: (item: T) => string | null | undefined,
): T[] => {
  const matched = search(items, query, name, extra);
  if (matched === null) {
    return [...items];
  }
  return matched.sort((a, b) => a.rank - b.rank || a.index - b.index).map((found) => found.item);
};

/** Same matching as `rank`, but the matching items keep their incoming order. */
export const filter = <T>(
  items: readonly T[],
  query: string | null | undefined,
  name: (item: T) => string | null | undefined,
  extra?: (item: T) => string | null | undefined,
): T[] => {
  const matched = search(items, query, name, extra);
  if (matched === null) {
    return [...items];
  }
  return matched.sort((a, b) => a.index - b.index).map((found) => found.item);
};

/**
 * Plain substring search that ignores case and "ё" but forgives nothing; for names of people and other texts
 * where a typo match would show the wrong one. An empty query matches everything.
 */
export const contains = (
  text: string | null | undefined,
  query: string | null | undefined,
): boolean => {
  const normalizedQuery = normalize(query);
  return normalizedQuery === '' || prepareText(text).normalized.includes(normalizedQuery);
};
