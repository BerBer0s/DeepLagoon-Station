export type TechState = 'researched' | 'available' | 'unaffordable' | 'locked';

export type IconLayer = { url: string; color: string };

export type WireTech = {
  id: string;
  name: string;
  discipline: string;
  tier: number;
  cost: number;
  prerequisites: { id: string }[];
  recipes: { name: string }[];
};

export type WireDiscipline = {
  id: string;
  name: string;
  shortName: string;
  color: string;
  icon: string;
};

export type WireData = {
  labels?: Record<string, string>;
  disciplines?: WireDiscipline[];
  techs?: WireTech[];
  points?: number;
  hasAccess?: boolean;
  states?: { id: string; state: TechState }[];
  iconBatch?: { icons: { id: string; layers: IconLayer[] }[] };
  chatState?: string;
};

export type Tech = Omit<WireTech, 'prerequisites'> & { prerequisites: string[] };

export const buildTechs = (techs: WireTech[]): Tech[] =>
  techs.map((tech) => ({
    ...tech,
    prerequisites: tech.prerequisites.map((prerequisite) => prerequisite.id),
  }));

export const buildStateMap = (
  states: NonNullable<WireData['states']>,
): Map<string, TechState> =>
  new Map(states.map((entry) => [entry.id, entry.state]));
