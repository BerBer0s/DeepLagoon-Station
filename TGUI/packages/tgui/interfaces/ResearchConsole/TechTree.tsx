import { type PointerEvent, useEffect, useMemo, useRef, useState } from 'react';

import { Button, Icon } from '../../components';
import { rectsIntersect, type Rect, ZOOM_STEP } from './camera';
import { chainOf } from './chain';
import { EdgeLayer } from './EdgeLayer';
import { fitCamera, homeCamera, recenterCamera } from './focus';
import type { Tech, TechState } from './model';
import { type Hint, TechNode } from './TechNode';
import type { ResearchFx } from './fx';
import { collectEdges, type TreeModel, unitStates } from './tree';
import { useTreeCamera } from './useTreeCamera';

type TechTreeProps = {
  tree: TreeModel;
  techs: Map<string, Tech>;
  disciplineColors: Map<string, string>;
  states: Map<string, TechState>;
  selectedId: string | null;
  attention: number;
  points: number;
  fx: ResearchFx | null;
  canResearch: boolean;
  labels: Record<string, string>;
  onSelect: (id: string | null) => void;
  onActivate: (id: string) => void;
};

const nodeFx = (fx: ResearchFx | null, id: string) => {
  if (fx?.snapped.has(id)) {
    return 'snap';
  }
  return fx?.woken.has(id) ? 'wake' : null;
};

const describeSelected = (
  tech: Tech,
  state: TechState,
  points: number,
  canResearch: boolean,
  labels: Record<string, string>,
): Hint => {
  switch (state) {
    case 'researched':
      return { text: labels['dl-research-researched'], armed: false };
    case 'locked':
      return { text: labels['dl-research-hint-locked'], armed: false };
    case 'unaffordable':
      return {
        text: `${labels['dl-research-unaffordable']}: ${tech.cost - points}`,
        armed: false,
      };
    case 'available':
      return canResearch
        ? {
            text: `${labels['dl-research-confirm']} · ${tech.cost}`,
            armed: true,
          }
        : { text: labels['dl-research-no-access'], armed: false };
  }
};

const nodeRect = (node: {
  x: number;
  y: number;
  width: number;
  height: number;
}): Rect => ({
  x0: node.x,
  y0: node.y,
  x1: node.x + node.width,
  y1: node.y + node.height,
});

export const TechTree = ({
  tree,
  techs,
  disciplineColors,
  states,
  selectedId,
  attention,
  points,
  fx,
  canResearch,
  labels,
  onSelect,
  onActivate,
}: TechTreeProps) => {
  const statesRef = useRef(states);
  const selectedRef = useRef(selectedId);
  useEffect(() => {
    statesRef.current = states;
    selectedRef.current = selectedId;
  });

  const camera = useTreeCamera({
    tree,
    initialCamera: (size) => homeCamera(tree, statesRef.current, size),
    onBackgroundClick: () => onSelect(null),
  });
  const { rendered } = camera;

  const visibleNodes = useMemo(
    () =>
      rendered
        ? tree.nodes.filter((node) => rectsIntersect(nodeRect(node), rendered))
        : [],
    [tree, rendered],
  );

  // Hovering a node previews its chain; otherwise the selected node's chain is shown.
  const [hoveredId, setHoveredId] = useState<string | null>(null);
  const focusId = hoveredId ?? selectedId;
  const chain = useMemo(
    () => (focusId && tree.nodeById.has(focusId) ? chainOf(tree, focusId) : null),
    [tree, focusId],
  );

  const edgeStates = useMemo(() => unitStates(tree, states), [tree, states]);
  const allEdges = useMemo(
    () => collectEdges(tree, edgeStates, rendered, () => true),
    [tree, edgeStates, rendered],
  );
  const chainEdges = useMemo(
    () =>
      chain
        ? collectEdges(tree, edgeStates, rendered, (index) => chain.units.has(index))
        : null,
    [tree, edgeStates, rendered, chain],
  );
  const igniteEdges = useMemo(
    () =>
      fx
        ? collectEdges(tree, edgeStates, rendered, (index) => fx.units.has(index))
        : null,
    [tree, edgeStates, rendered, fx],
  );

  const selectedHint = useMemo(() => {
    const tech = selectedId ? techs.get(selectedId) : undefined;
    return tech
      ? describeSelected(tech, states.get(tech.id) ?? 'locked', points, canResearch, labels)
      : null;
  }, [selectedId, techs, states, points, canResearch, labels]);

  const onPointerOver = (event: PointerEvent<HTMLDivElement>) => {
    if (camera.isDragging()) {
      return;
    }
    const id =
      (event.target as Element).closest<HTMLElement>('.TechNode')?.dataset.id ?? null;
    setHoveredId((current) => (current === id ? current : id));
  };

  return (
    <div
      ref={camera.viewportRef}
      className="ResearchTree"
      {...camera.viewportProps}
      onPointerOver={onPointerOver}
      onPointerLeave={() => setHoveredId(null)}
    >
      <div
        ref={camera.worldRef}
        className="ResearchTree__world"
        data-lod="near"
        data-focus={chain !== null}
        style={{ width: tree.width, height: tree.height }}
      >
        <svg
          className="ResearchTree__edges"
          width={tree.width}
          height={tree.height}
        >
          <EdgeLayer className="ResearchTree__lines" parts={allEdges} />
          {chainEdges && (
            <EdgeLayer className="ResearchTree__chain" parts={chainEdges} />
          )}
          {igniteEdges && (
            <EdgeLayer className="ResearchTree__ignite" parts={igniteEdges} />
          )}
        </svg>
        {visibleNodes.map((node) => {
          const tech = techs.get(node.id);
          return (
            tech && (
              <TechNode
                key={node.id}
                node={node}
                tech={tech}
                state={states.get(node.id) ?? 'locked'}
                selected={node.id === selectedId}
                inChain={chain?.nodes.has(node.id) === true}
                hint={node.id === selectedId ? selectedHint : null}
                attention={node.id === selectedId ? attention : 0}
                fx={nodeFx(fx, node.id)}
                color={disciplineColors.get(tech.discipline) ?? '#888888'}
                labels={labels}
                onActivate={onActivate}
              />
            )
          );
        })}
      </div>
      <div className="ResearchTree__toolbar">
        <Button
          onClick={() =>
            camera.moveTo(
              recenterCamera(
                tree,
                statesRef.current,
                camera.getSize(),
                camera.getCamera(),
                selectedRef.current,
              ),
              true,
            )
          }
        >
          <Icon name="crosshairs" /> {labels['dl-research-recenter']}
        </Button>
        <Button
          onClick={() => camera.moveTo(fitCamera(tree, camera.getSize()), true)}
        >
          <Icon name="expand" /> {labels['dl-research-fit']}
        </Button>
        <Button
          aria-label={labels['dl-research-zoom-out']}
          onClick={() => camera.zoomBy(1 / ZOOM_STEP)}
        >
          <Icon name="minus" />
        </Button>
        <Button
          aria-label={labels['dl-research-zoom-in']}
          onClick={() => camera.zoomBy(ZOOM_STEP)}
        >
          <Icon name="plus" />
        </Button>
      </div>
    </div>
  );
};
