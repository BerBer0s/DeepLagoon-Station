import { type PointerEvent, useEffect, useMemo, useRef, useState } from 'react';

import { Button, Icon } from '../../components';
import { rectsIntersect, type Rect, ZOOM_STEP } from './camera';
import { chainOf } from './chain';
import { EdgeLayer } from './EdgeLayer';
import { fitCamera, homeCamera, recenterCamera } from './focus';
import type { Tech, TechState } from './model';
import { TechNode } from './TechNode';
import { type TreeModel, unitStates, visibleEdges } from './tree';
import { useTreeCamera } from './useTreeCamera';

type TechTreeProps = {
  tree: TreeModel;
  techs: Map<string, Tech>;
  disciplineColors: Map<string, string>;
  states: Map<string, TechState>;
  selectedId: string | null;
  canResearch: boolean;
  labels: Record<string, string>;
  onSelect: (id: string | null) => void;
  onResearch: (id: string) => void;
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
  canResearch,
  labels,
  onSelect,
  onResearch,
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
  const edges = useMemo(
    () => visibleEdges(tree, edgeStates, rendered, chain?.units ?? null),
    [tree, edgeStates, rendered, chain],
  );

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
          <EdgeLayer className="ResearchTree__lines" parts={edges.all} />
          {chain && <EdgeLayer className="ResearchTree__chain" parts={edges.chain} />}
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
                canResearch={canResearch}
                color={disciplineColors.get(tech.discipline) ?? '#888888'}
                labels={labels}
                onSelect={onSelect}
                onResearch={onResearch}
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
