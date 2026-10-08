import { type CSSProperties, memo } from 'react';

import { Icon } from '../../components';
import { TintedSprite } from '../../components/PlayerTheme';
import { useTechIcon } from './icons';
import type { Tech, TechState } from './model';
import type { TreeNode } from './tree';

const WHITE = '#ffffff';

// A state is shown by its border style and badge as well as by color.
const STATE_ICON: Record<TechState, string> = {
  researched: 'check',
  available: 'circle-dot',
  unaffordable: 'coins',
  locked: 'lock',
};

const STATE_LABEL: Record<TechState, string> = {
  researched: 'dl-research-researched',
  available: 'dl-research-research',
  unaffordable: 'dl-research-unaffordable',
  locked: 'dl-research-locked',
};

/** What the selected technology says about researching it. */
export type Hint = {
  text: string;
  /** The next click researches it. */
  armed: boolean;
};

const TechIcon = ({ id, className = '' }: { id: string; className?: string }) => {
  const layers = useTechIcon(id);
  return (
    <div className={`TechNode__icon ${className}`}>
      {layers.map((layer, index) =>
        layer.color.toLowerCase() === WHITE ? (
          <img key={index} src={layer.url} alt="" draggable={false} />
        ) : (
          <TintedSprite key={index} image={layer.url} color={layer.color} />
        ),
      )}
    </div>
  );
};

/** A hub that opens a technology directly. Its line is only drawn on hover or selection. */
export type Origin = { id: string; name: string; color: string };

type TechNodeProps = {
  node: TreeNode;
  tech: Tech;
  state: TechState;
  selected: boolean;
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
        (inChain ? ' TechNode--chain' : '') +
        (hint?.armed ? ' TechNode--armed' : '') +
        (fx ? ' TechNode--' + fx : '')
      }
      data-id={tech.id}
      style={style}
    >
      <TechIcon id={tech.id} />
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
                title={origin.name}
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
