import {
  type MouseEvent,
  type PointerEvent,
  useCallback,
  useEffect,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
} from 'react';

import { Button, Icon } from '../../components';
import {
  type Camera,
  clampCamera,
  containsRect,
  expandRect,
  type Rect,
  rectArea,
  rectsIntersect,
  type Size,
  unionRect,
  viewRect,
  ZOOM_STEP,
  zoomAt,
} from './camera';
import { fitCamera, homeCamera } from './focus';
import type { Tech, TechState } from './model';
import { TechNode } from './TechNode';
import { type EdgeState, type TreeModel, unitStates } from './tree';

const DRAG_THRESHOLD = 4;
// A browser starts with a tiny viewport; the initial camera waits for a real one.
const MIN_VIEWPORT = 120;
// The rendered region is the view grown by this share of its size on each side.
const CULL_MARGIN = 0.75;
// The region is shrunk again once it is this many times larger than needed.
const CULL_SHRINK_AREA = 4;
const WHEEL_SENSITIVITY = 0.0015;
const WHEEL_LINE_PIXELS = 16;
const WHEEL_MAX_DELTA = 240;
// Below this scale the nodes drop their text (styled by `data-lod`).
const FAR_SCALE = 0.5;
const SMOOTH_CLASS = 'ResearchTree__world--smooth';

// The camera outlives the component, so a re-mounted tree keeps the view.
let rememberedCamera: Camera | undefined;

type Drag = {
  pointerId: number;
  startX: number;
  startY: number;
  origin: Camera;
  active: boolean;
};

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
  const viewportRef = useRef<HTMLDivElement>(null);
  const worldRef = useRef<HTMLDivElement>(null);
  const cameraRef = useRef<Camera>(rememberedCamera ?? { x: 0, y: 0, scale: 1 });
  const sizeRef = useRef<Size>({ width: 0, height: 0 });
  const treeRef = useRef(tree);
  const statesRef = useRef(states);
  const renderedRef = useRef<Rect | null>(null);
  const dragRef = useRef<Drag | null>(null);
  const justDraggedRef = useRef(false);
  const initializedRef = useRef(false);
  const moveEndRef = useRef<((event: TransitionEvent) => void) | null>(null);
  const [rendered, setRendered] = useState<Rect | null>(null);

  useLayoutEffect(() => {
    statesRef.current = states;
  });

  const syncCulling = useCallback((view: Rect) => {
    const wanted = expandRect(view, CULL_MARGIN);
    const current = renderedRef.current;
    if (
      !current ||
      !containsRect(current, view) ||
      rectArea(current) > CULL_SHRINK_AREA * rectArea(wanted)
    ) {
      renderedRef.current = wanted;
      setRendered(wanted);
    }
  }, []);

  // Moves the camera by writing the world's transform directly, so a move never re-renders nodes.
  const setCamera = useCallback(
    (next: Camera, smooth = false) => {
      const world = worldRef.current;
      if (!world) {
        return;
      }
      const size = sizeRef.current;
      const previous = cameraRef.current;
      const camera = clampCamera(next, treeRef.current, size);
      cameraRef.current = camera;
      rememberedCamera = camera;
      world.classList.toggle(SMOOTH_CLASS, smooth);
      world.style.transform = `translate(${camera.x}px, ${camera.y}px) scale(${camera.scale})`;
      world.dataset.lod = camera.scale < FAR_SCALE ? 'far' : 'near';
      const target = viewRect(camera, size);
      if (smooth) {
        // Keep everything on the way rendered until the move ends.
        syncCulling(unionRect(viewRect(previous, size), target));
        if (moveEndRef.current) {
          world.removeEventListener('transitionend', moveEndRef.current);
        }
        // Transitions of the nodes inside bubble up here too; only the world's own move counts.
        const onEnd = (event: TransitionEvent) => {
          if (event.target !== world) {
            return;
          }
          world.removeEventListener('transitionend', onEnd);
          moveEndRef.current = null;
          world.classList.remove(SMOOTH_CLASS);
          syncCulling(viewRect(cameraRef.current, sizeRef.current));
        };
        moveEndRef.current = onEnd;
        world.addEventListener('transitionend', onEnd);
      } else {
        syncCulling(target);
      }
    },
    [syncCulling],
  );

  // Turns an unfinished smooth move into a plain camera at its current position.
  const freeze = useCallback(() => {
    const world = worldRef.current;
    if (world?.classList.contains(SMOOTH_CLASS)) {
      const matrix = new DOMMatrixReadOnly(getComputedStyle(world).transform);
      setCamera({ x: matrix.e, y: matrix.f, scale: matrix.a });
    }
  }, [setCamera]);

  const initialize = useCallback(() => {
    const size = sizeRef.current;
    if (
      initializedRef.current ||
      treeRef.current.nodes.length === 0 ||
      size.width < MIN_VIEWPORT ||
      size.height < MIN_VIEWPORT
    ) {
      return;
    }
    initializedRef.current = true;
    setCamera(
      rememberedCamera ?? homeCamera(treeRef.current, statesRef.current, size),
    );
  }, [setCamera]);

  useLayoutEffect(() => {
    treeRef.current = tree;
    const viewport = viewportRef.current;
    if (!viewport) {
      return;
    }
    const bounds = viewport.getBoundingClientRect();
    sizeRef.current = { width: bounds.width, height: bounds.height };
    renderedRef.current = null;
    if (initializedRef.current) {
      setCamera(cameraRef.current);
    } else {
      initialize();
    }
  }, [tree, setCamera, initialize]);

  useEffect(() => {
    const viewport = viewportRef.current;
    if (!viewport) {
      return;
    }
    const observer = new ResizeObserver(([entry]) => {
      const { width, height } = entry.contentRect;
      const old = sizeRef.current;
      if (width === old.width && height === old.height) {
        return;
      }
      sizeRef.current = { width, height };
      if (!initializedRef.current) {
        initialize();
        return;
      }
      const camera = cameraRef.current;
      setCamera({
        ...camera,
        x: camera.x + (width - old.width) / 2,
        y: camera.y + (height - old.height) / 2,
      });
    });
    observer.observe(viewport);
    return () => observer.disconnect();
  }, [setCamera, initialize]);

  // React attaches wheel listeners as passive; zooming has to cancel the default scroll.
  useEffect(() => {
    const viewport = viewportRef.current;
    if (!viewport) {
      return;
    }
    const onWheel = (event: WheelEvent) => {
      event.preventDefault();
      freeze();
      const pixels =
        event.deltaMode === 1 ? event.deltaY * WHEEL_LINE_PIXELS : event.deltaY;
      const delta = Math.max(-WHEEL_MAX_DELTA, Math.min(WHEEL_MAX_DELTA, pixels));
      const bounds = viewport.getBoundingClientRect();
      setCamera(
        zoomAt(
          cameraRef.current,
          { x: event.clientX - bounds.left, y: event.clientY - bounds.top },
          Math.exp(-delta * WHEEL_SENSITIVITY),
        ),
      );
    };
    viewport.addEventListener('wheel', onWheel, { passive: false });
    return () => viewport.removeEventListener('wheel', onWheel);
  }, [freeze, setCamera]);

  const endDrag = (event: PointerEvent<HTMLDivElement>) => {
    const drag = dragRef.current;
    if (!drag || drag.pointerId !== event.pointerId) {
      return;
    }
    dragRef.current = null;
    if (drag.active) {
      justDraggedRef.current = true;
      event.currentTarget.classList.remove('ResearchTree--dragging');
      if (event.currentTarget.hasPointerCapture(event.pointerId)) {
        event.currentTarget.releasePointerCapture(event.pointerId);
      }
    }
  };

  const onPointerDown = (event: PointerEvent<HTMLDivElement>) => {
    justDraggedRef.current = false;
    if (
      event.button !== 0 ||
      (event.target as Element).closest('.ResearchTree__toolbar')
    ) {
      return;
    }
    freeze();
    dragRef.current = {
      pointerId: event.pointerId,
      startX: event.clientX,
      startY: event.clientY,
      origin: cameraRef.current,
      active: false,
    };
  };

  const onPointerMove = (event: PointerEvent<HTMLDivElement>) => {
    const drag = dragRef.current;
    if (!drag || drag.pointerId !== event.pointerId) {
      return;
    }
    if ((event.buttons & 1) === 0) {
      endDrag(event);
      return;
    }
    const dx = event.clientX - drag.startX;
    const dy = event.clientY - drag.startY;
    if (!drag.active) {
      if (Math.hypot(dx, dy) < DRAG_THRESHOLD) {
        return;
      }
      // Capture only once it is a drag, so a plain click still reaches the node under it.
      drag.active = true;
      event.currentTarget.setPointerCapture(event.pointerId);
      event.currentTarget.classList.add('ResearchTree--dragging');
    }
    setCamera({ ...drag.origin, x: drag.origin.x + dx, y: drag.origin.y + dy });
  };

  const onClick = (event: MouseEvent<HTMLDivElement>) => {
    if (justDraggedRef.current) {
      justDraggedRef.current = false;
      return;
    }
    if (!(event.target as Element).closest('.TechNode, .ResearchTree__toolbar')) {
      onSelect(null);
    }
  };

  const zoomBy = (factor: number) => {
    const size = sizeRef.current;
    setCamera(
      zoomAt(cameraRef.current, { x: size.width / 2, y: size.height / 2 }, factor),
      true,
    );
  };

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
      ref={viewportRef}
      className="ResearchTree"
      onPointerDown={onPointerDown}
      onPointerMove={onPointerMove}
      onPointerUp={endDrag}
      onPointerCancel={endDrag}
      onClick={onClick}
    >
      <div
        ref={worldRef}
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
            setCamera(
              homeCamera(treeRef.current, statesRef.current, sizeRef.current),
              true,
            )
          }
        >
          <Icon name="crosshairs" /> {labels['dl-research-recenter']}
        </Button>
        <Button
          onClick={() =>
            setCamera(fitCamera(treeRef.current, sizeRef.current), true)
          }
        >
          <Icon name="expand" /> {labels['dl-research-fit']}
        </Button>
        <Button
          aria-label={labels['dl-research-zoom-out']}
          onClick={() => zoomBy(1 / ZOOM_STEP)}
        >
          <Icon name="minus" />
        </Button>
        <Button
          aria-label={labels['dl-research-zoom-in']}
          onClick={() => zoomBy(ZOOM_STEP)}
        >
          <Icon name="plus" />
        </Button>
      </div>
    </div>
  );
};
