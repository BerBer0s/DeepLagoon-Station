import { type CSSProperties, memo } from 'react';

import { Icon } from '../../components';
import type { Tech, TechState } from './model';
import { type Hint, STATE_ICON, STATE_LABEL } from './status';
import { TechIcon } from './TechIcon';
import type { TreeNode } from './tree';

/** A hub that opens a technology directly. Its line is only drawn on hover or selection. */
export type Origin = { id: string; name: string; color: string };

type TechNodeProps = {
  node: TreeNode;
  tech: Tech;
  state: TechState;
  selected: boolean;
  /** Not among the technologies a tab or a search brings forward. */
  dimmed: boolean;
  /** Part of the chain of the focused technology. */
  inChain: boolean;
  origins: Origin[];
  /** Only the selected technology has a hint. */
  hint: Hint | null;
  /** Grows each time the player clicks the selected technology and nothing can be done. */
  attention: number;
  /** Plays a short animation: just researched, or just made available. */
  fx: 'snap' | 'wake' | null;
  color: string;
  labels: Record<string, string>;
};

export const TechNode = memo(function TechNode({
  node,
  tech,
  state,
  selected,
  dimmed,
  inChain,
  origins,
  hint,
  attention,
  fx,
  color,
  labels,
}: TechNodeProps) {
  const style = {
    left: node.x,
    top: node.y,
    width: node.width,
    height: node.height,
    '--discipline': color,
  } as CSSProperties;
  return (
    <div
      className={
        `TechNode TechNode--${state}` +
        (selected ? ' TechNode--selected' : '') +
        (dimmed ? ' TechNode--dim' : '') +
        (inChain ? ' TechNode--chain' : '') +
        (hint?.armed ? ' TechNode--armed' : '') +
        (fx ? ' TechNode--' + fx : '')
      }
      data-id={tech.id}
      data-tip={tech.name}
      style={style}
    >
      <TechIcon id={tech.id} className="TechNode__icon" />
      <div className="TechNode__text">
        <div className="TechNode__name">{tech.name}</div>
        <div className="TechNode__foot">
          <span className="TechNode__cost">
            <Icon name="coins" /> {tech.cost}
          </span>
          {hint?.armed ? (
            <span className="TechNode__go">{labels[STATE_LABEL.available]}</span>
          ) : (
            origins.map((origin) => (
              <span
                key={origin.id}
                className="TechNode__origin"
                data-tip={origin.name}
                style={{ borderColor: origin.color }}
              >
                <TechIcon id={origin.id} className="TechNode__icon--fill" />
              </span>
            ))
          )}
        </div>
      </div>
      {hint && (
        <div
          key={attention}
          className={
            'TechNode__hint' +
            (hint.armed ? ' TechNode__hint--go' : '') +
            (attention > 0 ? ' TechNode__hint--attention' : '')
          }
        >
          {hint.text}
        </div>
      )}
      <span className="TechNode__badge" aria-label={labels[STATE_LABEL[state]]}>
        <Icon name={STATE_ICON[state]} />
      </span>
    </div>
  );
});
