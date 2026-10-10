// Texts of the menu. The host sends every text localized, under `dl-lathe-*` keys.

export type T = (key: string) => string;

export const makeT =
  (labels: Record<string, string>): T =>
  (key) =>
    labels[`dl-lathe-${key}`] ?? key;
