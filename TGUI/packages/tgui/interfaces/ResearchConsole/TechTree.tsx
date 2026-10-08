import { type PointerEvent, useEffect, useMemo, useRef, useState } from 'react';

import { Button, Icon } from '../../components';
import { type Camera, rectsIntersect, type Rect, sameCamera, type Size, ZOOM_STEP } from './camera';
import { chainOf } from './chain';
import { EdgeLayer } from './EdgeLayer';
import { fitCamera, groupCamera, homeCamera, recenterCamera, revealCamera } from './focus';
import type { Tech, TechState } from './model';
import { describeTech } from './status';
import { type Origin, TechNode } from './TechNode';
import type { ResearchFx } from './fx';
import { collectEdges, type TreeModel, unitStates } from './tree';
import { useTreeCamera } from './useTreeCamera';

export type CameraTarget =
  /** Center the technology in the free view. */
  | { kind: 'node'; id: string }
  /** Move no more than needed to bring the technology into the free view. */
  | { kind: 'reveal'; id: string }
  | { kind: 'group'; ids: string[] };

/** Asks the tree to move the camera; a new `serial` is a new request. */
export type CameraCommand = { serial: number; target: CameraTarget };

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
  /** The technologies that stay bright while the others are dimmed; null when none is dimmed. */
  highlight: Set<string> | null;
  labels: Record<string, string>;
  /** Width of the details panel over the right edge of the tree. */
  insetRight: number;
  command: CameraCommand | null;
  onSelect: (id: string | null) => void;
  onActivate: (id: string) => void;
};

const NO_ORIGINS: Origin[] = [];

const nodeFx = (fx: ResearchFx | null, id: string) => {
  if (fx?.snapped.has(id)) {
    return 'snap';
  }
  return fx?.woken.has(id) ? 'wake' : null;
};

const cameraFor = (
  target: CameraTarget,
  tree: TreeModel,
  states: Map<string, TechState>,
  size: Size,
  current: Camera,
): Camera => {
  switch (target.kind) {
    case 'node':
      return recenterCamera(tree, states, size, current, target.id);
    case 'reveal':
      return revealCamera(tree, target.id, current, size);
    case 'group':
      return groupCamera(tree, target.ids, size);
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
  highlight,
  labels,
  insetRight,
  command,
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
    insetRight,
    onClick: (id) => (id ? onActivate(id) : onSelect(null)),
  });
  const { rendered } = camera;

  useEffect(() => {
    if (command) {
      const current = camera.getCamera();
      const next = cameraFor(command.target, tree, statesRef.current, camera.getSize(), current);
      if (!sameCamera(next, current)) {
        camera.moveTo(next, true);
      }
    }
  }, [command]);

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
  // Hub edges are left out here; they appear in the chain of the hovered or selected technology.
  const allEdges = useMemo(
    () => collectEdges(tree, edgeStates, rendered, (index) => !tree.units[index].hub),
    [tree, edgeStates, rendered],
  );
  const originsById = useMemo(() => {
    const map = new Map<string, Origin[]>();
    for (const [id, hubs] of tree.hubParents) {
      map.set(
        id,
        hubs.flatMap((hubId) => {
          const hub = techs.get(hubId);
          return hub
            ? [{ id: hubId, name: hub.name, color: disciplineColors.get(hub.discipline) ?? '#888888' }]
            : [];
        }),
      );
    }
    return map;
  }, [tree, techs, disciplineColors]);
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
      ? describeTech(tech, states.get(tech.id) ?? 'locked', points, canResearch, labels)
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
        data-dim={highlight !== null}
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
                dimmed={highlight !== null && !highlight.has(node.id) && node.id !== selectedId}
                inChain={chain?.nodes.has(node.id) === true}
                origins={originsById.get(node.id) ?? NO_ORIGINS}
                hint={node.id === selectedId ? selectedHint : null}
                attention={node.id === selectedId ? attention : 0}
                fx={nodeFx(fx, node.id)}
                color={disciplineColors.get(tech.discipline) ?? '#888888'}
                labels={labels}
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
          data-tip={labels['dl-research-zoom-out']}
          onClick={() => camera.zoomBy(1 / ZOOM_STEP)}
        >
          <Icon name="minus" />
        </Button>
        <Button
          aria-label={labels['dl-research-zoom-in']}
          data-tip={labels['dl-research-zoom-in']}
          onClick={() => camera.zoomBy(ZOOM_STEP)}
        >
          <Icon name="plus" />
        </Button>
      </div>
    </div>
  );
};
