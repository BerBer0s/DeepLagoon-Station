import { EDGE_STATES, type EdgeParts } from './tree';

type EdgeLayerProps = {
  className: string;
  parts: EdgeParts;
};

/** One group of lines and arrowheads, a path of each per state. */
export const EdgeLayer = ({ className, parts }: EdgeLayerProps) => (
  <g className={className}>
    {EDGE_STATES.map((state) => (
      <g key={state}>
        <path
          className={`ResearchTree__edge ResearchTree__edge--${state}`}
          d={parts[state].line}
        />
        <path
          className={`ResearchTree__arrow ResearchTree__arrow--${state}`}
          d={parts[state].arrow}
        />
      </g>
    ))}
  </g>
);
