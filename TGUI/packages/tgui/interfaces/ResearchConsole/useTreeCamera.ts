// Camera, culling and pointer handling of the technology tree. The camera lives in refs and is
// written straight to the world's `transform`, once per animation frame; React renders only when
// the view leaves the already rendered region.

import {
  type PointerEvent,
  type RefObject,
  useCallback,
  useEffect,
  useLayoutEffect,
  useRef,
  useState,
} from 'react';

import {
  type Camera,
  clampCamera,
  containsRect,
  expandRect,
  FAR_SCALE,
  type Rect,
  rectArea,
  type Size,
  TINY_SCALE,
  unionRect,
  viewRect,
  zoomAt,
} from './camera';
import type { TreeModel } from './tree';

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
const SMOOTH_MS = 220;
// The world is a separate compositor layer only while it moves, so that a pan or zoom does not
// repaint it; afterwards it is painted once at the final scale.
const MOVING_CLASS = 'ResearchTree__world--moving';
const MOTION_SETTLE_MS = 160;

// The camera outlives the component, so a re-mounted tree keeps the view.
let rememberedCamera: Camera | undefined;

type Drag = {
  pointerId: number;
  startX: number;
  startY: number;
  origin: Camera;
  active: boolean;
  /** The technology under the pointer when it went down, or null for the background. */
  targetId: string | null;
};

const nodeIdAt = (target: EventTarget | null) =>
  (target as Element | null)?.closest<HTMLElement>('.TechNode')?.dataset.id ?? null;

type TreeCameraOptions = {
  tree: TreeModel;
  /** The camera for the first view; called once the viewport has a real size. */
  initialCamera: (size: Size) => Camera;
  /** A press and release without a drag in between; `id` is null on the background. */
  onClick: (id: string | null) => void;
};

export type TreeCamera = {
  viewportRef: RefObject<HTMLDivElement | null>;
  worldRef: RefObject<HTMLDivElement | null>;
  /** Region of the world that is rendered, or null before the first view. */
  rendered: Rect | null;
  viewportProps: {
    onPointerDown: (event: PointerEvent<HTMLDivElement>) => void;
    onPointerMove: (event: PointerEvent<HTMLDivElement>) => void;
    onPointerUp: (event: PointerEvent<HTMLDivElement>) => void;
    onPointerCancel: (event: PointerEvent<HTMLDivElement>) => void;
  };
  moveTo: (camera: Camera, smooth: boolean) => void;
  zoomBy: (factor: number) => void;
  getCamera: () => Camera;
  getSize: () => Size;
  isDragging: () => boolean;
};

export const useTreeCamera = ({
  tree,
  initialCamera,
  onClick,
}: TreeCameraOptions): TreeCamera => {
  const viewportRef = useRef<HTMLDivElement>(null);
  const worldRef = useRef<HTMLDivElement>(null);
  const cameraRef = useRef<Camera>(rememberedCamera ?? { x: 0, y: 0, scale: 1 });
  const sizeRef = useRef<Size>({ width: 0, height: 0 });
  const treeRef = useRef(tree);
  const renderedRef = useRef<Rect | null>(null);
  const dragRef = useRef<Drag | null>(null);
  const initializedRef = useRef(false);
  const smoothRef = useRef(false);
  const frameRef = useRef<number | null>(null);
  const settleRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const moveEndRef = useRef<((event: TransitionEvent) => void) | null>(null);
  const initialCameraRef = useRef(initialCamera);
  const clickRef = useRef(onClick);
  const [rendered, setRendered] = useState<Rect | null>(null);

  useLayoutEffect(() => {
    initialCameraRef.current = initialCamera;
    clickRef.current = onClick;
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

  const cancelFrame = useCallback(() => {
    if (frameRef.current !== null) {
      cancelAnimationFrame(frameRef.current);
      frameRef.current = null;
    }
  }, []);

  // Writes the camera to the DOM. A smooth move is a CSS transition to the target.
  const apply = useCallback((smooth: boolean) => {
    const world = worldRef.current;
    if (!world) {
      return;
    }
    const camera = cameraRef.current;
    world.style.transition = smooth ? `transform ${SMOOTH_MS}ms ease-out` : 'none';
    world.style.transform = `translate(${camera.x}px, ${camera.y}px) scale(${camera.scale})`;
    world.dataset.lod =
      camera.scale < TINY_SCALE ? 'tiny' : camera.scale < FAR_SCALE ? 'far' : 'near';
    world.classList.add(MOVING_CLASS);
    if (settleRef.current) {
      clearTimeout(settleRef.current);
    }
    settleRef.current = setTimeout(
      () => world.classList.remove(MOVING_CLASS),
      MOTION_SETTLE_MS + (smooth ? SMOOTH_MS : 0),
    );
  }, []);

  const setCamera = useCallback(
    (next: Camera, smooth = false) => {
      if (!worldRef.current) {
        return;
      }
      const size = sizeRef.current;
      const previous = cameraRef.current;
      const camera = clampCamera(next, treeRef.current, size);
      cameraRef.current = camera;
      rememberedCamera = camera;
      smoothRef.current = smooth;
      if (smooth) {
        cancelFrame();
        apply(true);
        // Keep everything on the way rendered until the move ends.
        syncCulling(unionRect(viewRect(previous, size), viewRect(camera, size)));
        const world = worldRef.current;
        if (moveEndRef.current) {
          world.removeEventListener('transitionend', moveEndRef.current);
        }
        // Transitions of the nodes inside bubble up here too; only the world's own move counts.
        const onEnd = (event: TransitionEvent) => {
          if (event.target !== world || event.propertyName !== 'transform') {
            return;
          }
          world.removeEventListener('transitionend', onEnd);
          moveEndRef.current = null;
          world.style.transition = 'none';
          smoothRef.current = false;
          syncCulling(viewRect(cameraRef.current, sizeRef.current));
        };
        moveEndRef.current = onEnd;
        world.addEventListener('transitionend', onEnd);
      } else if (frameRef.current === null) {
        frameRef.current = requestAnimationFrame(() => {
          frameRef.current = null;
          apply(false);
          syncCulling(viewRect(cameraRef.current, sizeRef.current));
        });
      }
    },
    [apply, cancelFrame, syncCulling],
  );

  // Turns an unfinished smooth move into a plain camera at its current position.
  const freeze = useCallback(() => {
    const world = worldRef.current;
    if (world && smoothRef.current) {
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
    setCamera(rememberedCamera ?? initialCameraRef.current(size));
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

  useEffect(
    () => () => {
      cancelFrame();
      if (settleRef.current) {
        clearTimeout(settleRef.current);
      }
    },
    [cancelFrame],
  );

  const releaseDrag = (event: PointerEvent<HTMLDivElement>) => {
    const drag = dragRef.current;
    if (!drag || drag.pointerId !== event.pointerId) {
      return null;
    }
    dragRef.current = null;
    if (drag.active) {
      event.currentTarget.classList.remove('ResearchTree--dragging');
      if (event.currentTarget.hasPointerCapture(event.pointerId)) {
        event.currentTarget.releasePointerCapture(event.pointerId);
      }
    }
    return drag;
  };

  const onPointerDown = (event: PointerEvent<HTMLDivElement>) => {
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
      targetId: nodeIdAt(event.target),
    };
  };

  // A click is a press and a release on the same technology (or on the background) without a drag
  // in between. It is not taken from the `click` event: that one is lost when the node under the
  // pointer is re-rendered between the press and the release.
  const onPointerUp = (event: PointerEvent<HTMLDivElement>) => {
    const drag = releaseDrag(event);
    if (drag && !drag.active && nodeIdAt(event.target) === drag.targetId) {
      clickRef.current(drag.targetId);
    }
  };

  const onPointerMove = (event: PointerEvent<HTMLDivElement>) => {
    const drag = dragRef.current;
    if (!drag || drag.pointerId !== event.pointerId) {
      return;
    }
    if ((event.buttons & 1) === 0) {
      releaseDrag(event);
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

  const zoomBy = (factor: number) => {
    const size = sizeRef.current;
    setCamera(
      zoomAt(cameraRef.current, { x: size.width / 2, y: size.height / 2 }, factor),
      true,
    );
  };

  return {
    viewportRef,
    worldRef,
    rendered,
    viewportProps: {
      onPointerDown,
      onPointerMove,
      onPointerUp,
      onPointerCancel: releaseDrag,
    },
    moveTo: setCamera,
    zoomBy,
    getCamera: () => cameraRef.current,
    getSize: () => sizeRef.current,
    isDragging: () => dragRef.current?.active === true,
  };
};
