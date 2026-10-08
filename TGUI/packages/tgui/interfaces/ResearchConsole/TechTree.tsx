import { useEffect, useMemo, useRef } from 'react';

import { Button, Icon } from '../../components';
import { rectsIntersect, type Rect, ZOOM_STEP } from './camera';
import { fitCamera, homeCamera, recenterCamera } from './focus';
import type { Tech, TechState } from './model';
import { TechNode } from './TechNode';
import { type EdgeState, type TreeModel, unitStates } from './tree';
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

  const edgeStates = useMemo(() => unitStates(tree, states), [tree, states]);
  const edgePaths = useMemo(() => {
    const parts: Record<EdgeState, string[]> = { locked: [], open: [], done: [] };
    tree.units.forEach((unit, index) => {
      if (rendered && rectsIntersect(unit, rendered)) {
        parts[edgeStates[index]].push(unit.d);
      }
    });
    return {
      locked: parts.locked.join(''),
      open: parts.open.join(''),
      done: parts.done.join(''),
    };
  }, [tree, edgeStates, rendered]);

  return (
    <div
      ref={camera.viewportRef}
      className="ResearchTree"
      {...camera.viewportProps}
    >
      <div
        ref={camera.worldRef}
        className="ResearchTree__world"
        data-lod="near"
        style={{ width: tree.width, height: tree.height }}
      >
        <svg
          className="ResearchTree__edges"
          width={tree.width}
          height={tree.height}
        >
          <path
            className="ResearchTree__edge ResearchTree__edge--locked"
            d={edgePaths.locked}
          />
          <path
            className="ResearchTree__edge ResearchTree__edge--open"
            d={edgePaths.open}
          />
          <path
            className="ResearchTree__edge ResearchTree__edge--done"
            d={edgePaths.done}
          />
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
