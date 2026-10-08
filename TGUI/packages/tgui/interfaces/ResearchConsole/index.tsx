import {
  type SyntheticEvent,
  useCallback,
  useMemo,
  useRef,
  useState,
} from 'react';

import { useBackend } from '../../backend';
import { Button } from '../../components';
import { playerTheme } from '../../components/PlayerTheme';
import {
  buildStateMap,
  buildTechs,
  type TechState,
  type WireData,
} from './model';
import './ResearchConsole.scss';
import { TechTree } from './TechTree';
import { buildTree } from './tree';

// Left mouse button drags are reserved for panning, so the browser must not
// start text selection or image dragging. Text fields keep their behavior.
const suppressNativeDrag = (event: SyntheticEvent) => {
  if (!(event.target as Element).closest('input, textarea')) {
    event.preventDefault();
  }
};

const sameStates = (a: Map<string, TechState>, b: Map<string, TechState>) =>
  a.size === b.size && [...a].every(([id, state]) => b.get(id) === state);

export const ResearchConsole = () => {
  const { data, act } = useBackend<WireData>();
  const { disciplines, techs: wireTechs, labels, states } = data;
  const [selectedId, setSelectedId] = useState<string | null>(null);

  const techs = useMemo(() => buildTechs(wireTechs ?? []), [wireTechs]);
  const techById = useMemo(
    () => new Map(techs.map((tech) => [tech.id, tech])),
    [techs],
  );
  const tree = useMemo(() => buildTree(techs), [techs]);
  const disciplineColors = useMemo(
    () => new Map((disciplines ?? []).map((entry) => [entry.id, entry.color])),
    [disciplines],
  );

  // Points change often; the state map keeps its identity while the states themselves do not.
  const previousStates = useRef<Map<string, TechState>>(undefined);
  const stateById = useMemo(() => {
    const next = buildStateMap(states ?? []);
    if (previousStates.current && sameStates(previousStates.current, next)) {
      return previousStates.current;
    }
    previousStates.current = next;
    return next;
  }, [states]);

  const onResearch = useCallback((id: string) => act('research', { id }), [act]);

  const theme = playerTheme(data.chatState);
  // playerTheme also returns the chat's animated-background class. That animation never ends and
  // repaints the whole window on every frame, so only the light/dark class is taken from it.
  const rootClass = `ResearchConsole ${theme.className.split(' ')[0]}`;

  if (!labels || !wireTechs) {
    return <div className={rootClass} style={theme.style} />;
  }

  return (
    <div
      className={rootClass}
      style={theme.style}
      onMouseDown={suppressNativeDrag}
      onDragStart={suppressNativeDrag}
    >
      <header className="ResearchConsole__header">
        <span>
          {labels['dl-research-points']}: <b>{data.points ?? 0}</b>
        </span>
        <Button onClick={() => act('servers')}>{labels['dl-research-servers']}</Button>
      </header>
      {stateById.size === 0 && (
        <div className="ResearchConsole__notice">{labels['dl-research-no-server']}</div>
      )}
      <TechTree
        tree={tree}
        techs={techById}
        disciplineColors={disciplineColors}
        states={stateById}
        selectedId={selectedId}
        canResearch={data.hasAccess === true}
        labels={labels}
        onSelect={setSelectedId}
        onResearch={onResearch}
      />
    </div>
  );
};
