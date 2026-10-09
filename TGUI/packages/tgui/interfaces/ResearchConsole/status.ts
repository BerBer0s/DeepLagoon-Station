// What a technology looks like and says in a given state; shared by the nodes and the details panel.

import type { Tech, TechState } from './model';

// A state is shown by its border style and badge as well as by color.
export const STATE_ICON: Record<TechState, string> = {
  researched: 'check',
  available: 'circle-dot',
  unaffordable: 'coins',
  locked: 'lock',
};

export const STATE_LABEL: Record<TechState, string> = {
  researched: 'dl-research-researched',
  available: 'dl-research-research',
  unaffordable: 'dl-research-unaffordable',
  locked: 'dl-research-locked',
};

/** What a technology says about researching it. */
export type Hint = {
  text: string;
  /** The next click researches it. */
  armed: boolean;
};

export const describeTech = (
  tech: Tech,
  state: TechState,
  points: number,
  canResearch: boolean,
  labels: Record<string, string>,
): Hint => {
  switch (state) {
    case 'researched':
      return { text: labels['dl-research-researched'], armed: false };
    case 'locked':
      return { text: labels['dl-research-hint-locked'], armed: false };
    case 'unaffordable':
      return {
        text: `${labels['dl-research-unaffordable']}: ${tech.cost - points}`,
        armed: false,
      };
    case 'available':
      return canResearch
        ? {
            text: `${labels['dl-research-confirm']} · ${tech.cost}`,
            armed: true,
          }
        : { text: labels['dl-research-no-access'], armed: false };
  }
};
