import {
  type ReactNode,
  useCallback,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
} from 'react';

// Rows beyond the visible ones that are kept rendered, so that a short scroll shows no gaps.
const OVERSCAN_PX = 240;

type Item = { key: string; height: number };

type VirtualListProps<T extends Item> = {
  items: T[];
  renderItem: (item: T) => ReactNode;
  /** The list goes back to the top when this changes (a new search or category). */
  resetKey: string;
  /** The key of an item that has to be fully in view when this changes. */
  revealKey: string | null;
};

/**
 * A list of rows with known heights that renders only the rows in view. Hundreds of recipes would
 * otherwise be thousands of elements, and every repaint of the embedded browser costs the whole window.
 */
export const VirtualList = <T extends Item>({
  items,
  renderItem,
  resetKey,
  revealKey,
}: VirtualListProps<T>) => {
  const box = useRef<HTMLDivElement>(null);
  const frame = useRef(0);
  const [view, setView] = useState({ top: 0, height: 480 });

  const offsets = useMemo(() => {
    const result = new Array<number>(items.length + 1);
    let total = 0;
    for (let i = 0; i < items.length; i++) {
      result[i] = total;
      total += items[i].height;
    }
    result[items.length] = total;
    return result;
  }, [items]);
  const total = offsets[items.length];

  const measure = useCallback(() => {
    frame.current = 0;
    const element = box.current;
    if (element) {
      setView((previous) =>
        previous.top === element.scrollTop && previous.height === element.clientHeight
          ? previous
          : { top: element.scrollTop, height: element.clientHeight },
      );
    }
  }, []);

  const schedule = useCallback(() => {
    if (frame.current === 0) {
      frame.current = requestAnimationFrame(measure);
    }
  }, [measure]);

  useLayoutEffect(() => {
    const element = box.current;
    if (!element) {
      return;
    }
    measure();
    const observer = new ResizeObserver(schedule);
    observer.observe(element);
    return () => {
      observer.disconnect();
      cancelAnimationFrame(frame.current);
      frame.current = 0;
    };
  }, [measure, schedule]);

  useLayoutEffect(() => {
    if (box.current) {
      box.current.scrollTop = 0;
      measure();
    }
  }, [resetKey, measure]);

  useLayoutEffect(() => {
    const element = box.current;
    if (!element || revealKey === null) {
      return;
    }
    const index = items.findIndex((item) => item.key === revealKey);
    if (index < 0) {
      return;
    }
    const top = offsets[index];
    const bottom = offsets[index + 1];
    if (bottom > element.scrollTop + element.clientHeight) {
      element.scrollTop = Math.min(top, bottom - element.clientHeight);
    } else if (top < element.scrollTop) {
      element.scrollTop = top;
    }
    measure();
    // Only a change of the revealed item moves the list.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [revealKey]);

  // The first item whose bottom is past the top of the view, found by bisection.
  let first = 0;
  let last = items.length;
  const from = view.top - OVERSCAN_PX;
  while (first < last) {
    const middle = (first + last) >> 1;
    if (offsets[middle + 1] <= from) {
      first = middle + 1;
    } else {
      last = middle;
    }
  }
  const to = view.top + view.height + OVERSCAN_PX;
  const shown: ReactNode[] = [];
  for (let i = first; i < items.length && offsets[i] < to; i++) {
    const item = items[i];
    shown.push(
      <div
        key={item.key}
        className="VirtualList__item"
        style={{ top: offsets[i], height: item.height }}
      >
        {renderItem(item)}
      </div>,
    );
  }

  return (
    <div ref={box} className="VirtualList" onScroll={schedule}>
      <div className="VirtualList__space" style={{ height: total }}>
        {shown}
      </div>
    </div>
  );
};
