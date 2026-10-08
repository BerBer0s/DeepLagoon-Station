import {
  type SyntheticEvent,
  useCallback,
  useEffect,
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
import { useResearchFx } from './useResearchFx';
import { buildTree } from './tree';

// The same research request is repeated only after the technology states change, or after this
// time if the server never answered.
const REQUEST_REPEAT_MS = 1000;

type Request = { id: string; states: Map<string, TechState>; at: number };

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

  const fx = useResearchFx(tree, stateById);

  // The first click on a technology selects it, the second one on the same technology researches
  // it. The callbacks read the latest values from a ref so that the nodes keep their props.
  const [attention, setAttention] = useState(0);
  const lastRequest = useRef<Request | null>(null);
  const latest = useRef({ selectedId, stateById, hasAccess: data.hasAccess === true });
  useEffect(() => {
    latest.current = { selectedId, stateById, hasAccess: data.hasAccess === true };
  });

  // The selection is also written to the ref at once: a second click that arrives before React has
  // rendered the first one must see the selected technology, or it would select it again.
  const select = useCallback((id: string | null) => {
    latest.current.selectedId = id;
    setSelectedId(id);
    setAttention(0);
  }, []);

  const onActivate = useCallback(
    (id: string) => {
      const { selectedId: current, stateById: states, hasAccess } = latest.current;
      if (id !== current) {
        select(id);
        return;
      }
      if (states.get(id) !== 'available' || !hasAccess) {
        setAttention((count) => count + 1);
        return;
      }
      const now = performance.now();
      const last = lastRequest.current;
      if (!last || last.id !== id || last.states !== states || now - last.at >= REQUEST_REPEAT_MS) {
        lastRequest.current = { id, states, at: now };
        act('research', { id });
      }
    },
    [act, select],
  );

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        select(null);
      }
    };
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [select]);

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
        attention={attention}
        points={data.points ?? 0}
        fx={fx}
        canResearch={data.hasAccess === true}
        labels={labels}
        onSelect={select}
        onActivate={onActivate}
      />
    </div>
  );
};
