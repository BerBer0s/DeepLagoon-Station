import { useEffect, useRef, useState } from 'react';

import { mergeFx, type ResearchFx, researchFx } from './fx';
import type { TechState } from './model';
import type { TreeModel } from './tree';

// The classes that start the animations are removed after the longest of them has ended.
const FX_LIFETIME_MS = 600;

/** The technologies and lines to animate after a research, for a short while; else null. */
export const useResearchFx = (
  tree: TreeModel,
  states: Map<string, TechState>,
): ResearchFx | null => {
  const [fx, setFx] = useState<ResearchFx | null>(null);
  const previous = useRef(states);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    const next = researchFx(tree, previous.current, states);
    previous.current = states;
    if (!next) {
      return;
    }
    // A research that lands while an animation still runs adds to it; running ones keep going.
    setFx((current) => (current ? mergeFx(current, next) : next));
    if (timer.current) {
      clearTimeout(timer.current);
    }
    timer.current = setTimeout(() => setFx(null), FX_LIFETIME_MS);
  }, [tree, states]);

  useEffect(
    () => () => {
      if (timer.current) {
        clearTimeout(timer.current);
      }
    },
    [],
  );

  return fx;
};
