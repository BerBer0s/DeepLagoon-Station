// The print that is running now, as the page sees it. The host sends where the print is (`elapsed`) once per
// state; the page turns that into the moment the print began on its own clock, so that everything that
// shows the print (the bar, the stripe of its row) can start at the right point whenever it is drawn.

import { type CSSProperties, useRef } from 'react';

import type { Current } from './model';

/** Everything that shows a print steps at this interval (seconds): smooth motion would repaint the window every frame. */
export const TICK = 0.25;

/** One cycle of the stripe is this many ticks. */
const STRIPE_TICKS = 4;
const MAX_STEPS = 80;
const MIN_TOTAL = 0.1;

export type ProgressMode = 'estimate' | 'indeterminate';

export type Printing = {
  id: string;
  /** Changes with every print. */
  key: string;
  /** performance.now() at the moment the print began. */
  startedAt: number;
  /** Seconds one print takes, as the server says. */
  total: number;
};

export const makePrinting = (current: Current): Printing | null =>
  current.active && current.id
    ? {
        id: current.id,
        key: current.key ?? '',
        startedAt: performance.now() - (current.elapsed ?? 0) * 1000,
        total: Math.max(MIN_TOTAL, current.total ?? MIN_TOTAL),
      }
    : null;

const elapsedOf = (printing: Printing) => Math.max(0, (performance.now() - printing.startedAt) / 1000);

/**
 * A style fixed per print: a later state of the same print must not restart the animation, and a row
 * that is drawn later (scrolled into view) must still step together with the bar.
 */
const useFrozenStyle = (
  printing: Printing | null,
  make: (printing: Printing) => CSSProperties,
): CSSProperties | undefined => {
  const frozen = useRef<{ key: string; style: CSSProperties }>(undefined);
  if (!printing) {
    return undefined;
  }
  if (frozen.current?.key !== printing.key) {
    frozen.current = { key: printing.key, style: make(printing) };
  }
  return frozen.current.style;
};

/** The determinate bar: one stepped animation over the whole print, started where the print is. */
export const useEstimateStyle = (printing: Printing | null) =>
  useFrozenStyle(printing, (print) => {
    const steps = Math.max(1, Math.min(MAX_STEPS, Math.round(print.total / TICK)));
    return {
      animationDuration: `${print.total}s`,
      animationDelay: `-${Math.min(print.total, elapsedOf(print))}s`,
      animationTimingFunction: `steps(${steps}, end)`,
    };
  });

/** A band that goes round the track: it says "working", not how far. */
export const useIndeterminateStyle = (printing: Printing | null) =>
  useFrozenStyle(printing, (print) => {
    const cycle = TICK * 12;
    return {
      animationDuration: `${cycle}s`,
      animationDelay: `-${elapsedOf(print) % cycle}s`,
      animationTimingFunction: 'steps(12, end)',
    };
  });

/** The stripe of the printing row. Its ticks fall on the same instants as the bar's. */
export const useStripeStyle = (printing: Printing | null) =>
  useFrozenStyle(printing, (print) => {
    const cycle = TICK * STRIPE_TICKS;
    return {
      animationDuration: `${cycle}s`,
      animationDelay: `-${elapsedOf(print) % cycle}s`,
      animationTimingFunction: `steps(${STRIPE_TICKS}, end)`,
    };
  });
