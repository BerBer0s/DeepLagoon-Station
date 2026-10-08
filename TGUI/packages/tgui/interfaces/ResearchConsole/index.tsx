import {
  type CSSProperties,
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
import { DetailsPanel, PANEL_STRIP_WIDTH, PANEL_WIDTH } from './DetailsPanel';
import { DisciplineTabs } from './DisciplineTabs';
import {
  buildDependents,
  buildStateMap,
  buildTechs,
  type TechState,
  type WireData,
} from './model';
import './ResearchConsole.scss';
import { type CameraCommand, type CameraTarget, TechTree } from './TechTree';
import { Tips } from './Tips';
import { useResearchFx } from './useResearchFx';
import { buildTree } from './tree';

// The same research request is repeated only after the technology states change, or after this
// time if the server never answered.
const REQUEST_REPEAT_MS = 1000;

const NO_IDS: string[] = [];

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
  const dependents = useMemo(() => buildDependents(techs), [techs]);
  const disciplineById = useMemo(
    () => new Map((disciplines ?? []).map((entry) => [entry.id, entry])),
    [disciplines],
  );
  const techIdsByDiscipline = useMemo(() => {
    const groups = new Map<string, string[]>();
    for (const tech of techs) {
      groups.set(tech.discipline, [...(groups.get(tech.discipline) ?? []), tech.id]);
    }
    return groups;
  }, [techs]);
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

  const [panelCollapsed, setPanelCollapsed] = useState(false);
  const [cameraCommand, setCameraCommand] = useState<CameraCommand | null>(null);
  const [activeDiscipline, setActiveDiscipline] = useState<string | null>(null);

  const selectedTech = selectedId ? techById.get(selectedId) : undefined;
  const selectedHubs = useMemo(
    () => new Set(selectedId ? tree.hubParents.get(selectedId) : undefined),
    [tree, selectedId],
  );
  const highlight = useMemo(
    () => (activeDiscipline ? new Set(techIdsByDiscipline.get(activeDiscipline)) : null),
    [activeDiscipline, techIdsByDiscipline],
  );
  // The panel lies over the right edge of the tree; the toolbar moves aside for it.
  const panelWidth = selectedTech ? (panelCollapsed ? PANEL_STRIP_WIDTH : PANEL_WIDTH) : 0;

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

  // False when the technology cannot be researched now.
  const research = useCallback(
    (id: string) => {
      const { stateById: states, hasAccess } = latest.current;
      if (states.get(id) !== 'available' || !hasAccess) {
        return false;
      }
      const now = performance.now();
      const last = lastRequest.current;
      if (!last || last.id !== id || last.states !== states || now - last.at >= REQUEST_REPEAT_MS) {
        lastRequest.current = { id, states, at: now };
        act('research', { id });
      }
      return true;
    },
    [act],
  );

  const onActivate = useCallback(
    (id: string) => {
      if (id !== latest.current.selectedId) {
        select(id);
      } else if (!research(id)) {
        setAttention((count) => count + 1);
      }
    },
    [research, select],
  );

  const moveCamera = useCallback((target: CameraTarget) => {
    setCameraCommand((previous) => ({ serial: (previous?.serial ?? 0) + 1, target }));
  }, []);

  // Selecting a technology from the panel also brings it into view.
  const navigate = useCallback(
    (id: string) => {
      select(id);
      moveCamera({ kind: 'node', id });
    },
    [moveCamera, select],
  );

  // A tab narrows the tree to a discipline and brings it into view; the active one clears it.
  const onTab = useCallback(
    (id: string | null) => {
      setActiveDiscipline(id);
      if (id) {
        moveCamera({ kind: 'group', ids: techIdsByDiscipline.get(id) ?? [] });
      }
    },
    [moveCamera, techIdsByDiscipline],
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
        <DisciplineTabs
          disciplines={disciplines ?? []}
          active={activeDiscipline}
          labels={labels}
          onSelect={onTab}
        />
        <Button onClick={() => act('servers')}>{labels['dl-research-servers']}</Button>
      </header>
      {stateById.size === 0 && (
        <div className="ResearchConsole__notice">{labels['dl-research-no-server']}</div>
      )}
      <div
        className="ResearchConsole__body"
        style={{ '--rc-inset': `${panelWidth}px` } as CSSProperties}
      >
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
          highlight={highlight}
          labels={labels}
          command={cameraCommand}
          onSelect={select}
          onActivate={onActivate}
        />
        {selectedTech && (
          <DetailsPanel
            tech={selectedTech}
            state={stateById.get(selectedTech.id) ?? 'locked'}
            discipline={disciplineById.get(selectedTech.discipline)}
            points={data.points ?? 0}
            canResearch={data.hasAccess === true}
            techs={techById}
            states={stateById}
            disciplineColors={disciplineColors}
            dependents={dependents.get(selectedTech.id) ?? NO_IDS}
            hubs={selectedHubs}
            collapsed={panelCollapsed}
            labels={labels}
            onToggle={() => setPanelCollapsed((collapsed) => !collapsed)}
            onNavigate={navigate}
            onResearch={research}
          />
        )}
      </div>
      <Tips />
    </div>
  );
};
