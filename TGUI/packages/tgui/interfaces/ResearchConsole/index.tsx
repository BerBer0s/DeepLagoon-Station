import { memo, type SyntheticEvent, useCallback, useMemo } from 'react';

import { useBackend } from '../../backend';
import { Button, Icon } from '../../components';
import { playerTheme, TintedSprite } from '../../components/PlayerTheme';
import { useIconRequest, useTechIcon } from './icons';
import {
  buildGroups,
  buildStateMap,
  type Tech,
  type TechState,
  type WireData,
} from './model';
import './ResearchConsole.scss';

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

const WHITE = '#ffffff';

// Left mouse button drags are reserved for panning, so the browser must not
// start text selection or image dragging. Text fields keep their behavior.
const suppressNativeDrag = (event: SyntheticEvent) => {
  if (!(event.target as Element).closest('input, textarea')) {
    event.preventDefault();
  }
};

const TechIcon = ({ id }: { id: string }) => {
  const layers = useTechIcon(id);
  return (
    <div className="ResearchConsole__icon">
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

type TechCardProps = {
  tech: Tech;
  state: TechState;
  canResearch: boolean;
  labels: Record<string, string>;
  onResearch: (id: string) => void;
};

const TechCard = memo(function TechCard({
  tech,
  state,
  canResearch,
  labels,
  onResearch,
}: TechCardProps) {
  const requestIcon = useIconRequest(tech.id);
  return (
    <div
      ref={requestIcon}
      className={`ResearchConsole__card ResearchConsole__card--${state}`}
    >
      <TechIcon id={tech.id} />
      <div className="ResearchConsole__cardBody">
        <div className="ResearchConsole__cardName" title={tech.name}>
          {tech.name}
        </div>
        <div className="ResearchConsole__cardMeta">
          {labels['dl-research-tier']} {tech.tier} · {tech.cost}
        </div>
      </div>
      {state === 'available' ? (
        <Button
          disabled={!canResearch}
          tooltip={canResearch ? undefined : labels['dl-research-no-access']}
          onClick={() => onResearch(tech.id)}
        >
          {labels[STATE_LABEL.available]}
        </Button>
      ) : (
        <span className="ResearchConsole__badge">
          <Icon name={STATE_ICON[state]} /> {labels[STATE_LABEL[state]]}
        </span>
      )}
    </div>
  );
});

export const ResearchConsole = () => {
  const { data, act } = useBackend<WireData>();
  const { disciplines, techs, labels, states } = data;

  const groups = useMemo(
    () => buildGroups(disciplines ?? [], techs ?? []),
    [disciplines, techs],
  );
  const stateById = useMemo(() => buildStateMap(states ?? []), [states]);
  const onResearch = useCallback((id: string) => act('research', { id }), [act]);

  const theme = playerTheme(data.chatState);
  // playerTheme also returns the chat's animated-background class. That animation never ends and
  // repaints the whole window on every frame, so only the light/dark class is taken from it.
  const rootClass = `ResearchConsole ${theme.className.split(' ')[0]}`;

  if (!labels || !techs) {
    return <div className={rootClass} style={theme.style} />;
  }

  return (
    <div
      className={rootClass}
      style={theme.style}
      onDragStart={suppressNativeDrag}
    >
      <header className="ResearchConsole__header">
        <span>
          {labels['dl-research-points']}: <b>{data.points ?? 0}</b>
        </span>
        <Button onClick={() => act('servers')}>{labels['dl-research-servers']}</Button>
      </header>
      <main className="ResearchConsole__content" onMouseDown={suppressNativeDrag}>
        {stateById.size === 0 && (
          <div className="ResearchConsole__notice">{labels['dl-research-no-server']}</div>
        )}
        {groups.map((group) => (
          <section key={group.id} className="ResearchConsole__group">
            <h3 className="ResearchConsole__groupTitle" style={{ borderColor: group.color }}>
              <img src={group.icon} alt="" draggable={false} /> {group.name}
            </h3>
            <div className="ResearchConsole__grid">
              {group.techs.map((tech) => (
                <TechCard
                  key={tech.id}
                  tech={tech}
                  state={stateById.get(tech.id) ?? 'locked'}
                  canResearch={data.hasAccess === true}
                  labels={labels}
                  onResearch={onResearch}
                />
              ))}
            </div>
          </section>
        ))}
      </main>
    </div>
  );
};
