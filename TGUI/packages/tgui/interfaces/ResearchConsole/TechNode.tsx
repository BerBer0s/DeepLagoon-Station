import { type CSSProperties, memo } from 'react';

import { Button, Icon } from '../../components';
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

const TechIcon = ({ id }: { id: string }) => {
  const layers = useTechIcon(id);
  return (
    <div className="TechNode__icon">
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

type TechNodeProps = {
  node: TreeNode;
  tech: Tech;
  state: TechState;
  selected: boolean;
  /** Part of the chain of the focused technology. */
  inChain: boolean;
  canResearch: boolean;
  color: string;
  labels: Record<string, string>;
  onSelect: (id: string) => void;
  onResearch: (id: string) => void;
};

export const TechNode = memo(function TechNode({
  node,
  tech,
  state,
  selected,
  inChain,
  canResearch,
  color,
  labels,
  onSelect,
  onResearch,
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
        (inChain ? ' TechNode--chain' : '')
      }
      data-id={tech.id}
      style={style}
      onClick={() => onSelect(tech.id)}
    >
      <TechIcon id={tech.id} />
      <div className="TechNode__text">
        <div className="TechNode__name">{tech.name}</div>
        <div className="TechNode__foot">
          <span className="TechNode__cost">
            <Icon name="coins" /> {tech.cost}
          </span>
          {state === 'available' && (
            <Button
              compact
              disabled={!canResearch}
              tooltip={canResearch ? undefined : labels['dl-research-no-access']}
              onClick={() => onResearch(tech.id)}
            >
              {labels[STATE_LABEL.available]}
            </Button>
          )}
        </div>
      </div>
      <span className="TechNode__badge" aria-label={labels[STATE_LABEL[state]]}>
        <Icon name={STATE_ICON[state]} />
      </span>
    </div>
  );
});
