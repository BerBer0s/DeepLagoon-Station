import { useEffect, useLayoutEffect, useRef, useState } from 'react';

// How long the pointer rests on an element before its tip appears.
const SHOW_DELAY_MS = 350;
const GAP = 8;
const EDGE = 4;

type Tip = { text: string; anchor: DOMRect };

/**
 * One tip for the whole page, for elements that carry a `data-tip` text. The embedded browser has
 * no native tips for plain elements. It listens on the document, shows nothing until the pointer
 * has rested on an element, and hides on any press or wheel turn, since those move what is under it.
 */
export const Tips = () => {
  const [tip, setTip] = useState<Tip | null>(null);
  const box = useRef<HTMLDivElement>(null);

  useEffect(() => {
    let timer: ReturnType<typeof setTimeout> | undefined;
    let target: HTMLElement | null = null;

    const hide = () => {
      clearTimeout(timer);
      target = null;
      setTip(null);
    };
    const onOver = (event: PointerEvent) => {
      const next = (event.target as Element).closest<HTMLElement>('[data-tip]');
      if (next === target) {
        return;
      }
      hide();
      if (next?.dataset.tip) {
        target = next;
        timer = setTimeout(
          () => setTip({ text: next.dataset.tip ?? '', anchor: next.getBoundingClientRect() }),
          SHOW_DELAY_MS,
        );
      }
    };
    const onOut = (event: PointerEvent) => {
      if (!event.relatedTarget) {
        hide();
      }
    };

    document.addEventListener('pointerover', onOver);
    document.addEventListener('pointerout', onOut);
    document.addEventListener('pointerdown', hide, true);
    document.addEventListener('wheel', hide, { capture: true, passive: true });
    return () => {
      clearTimeout(timer);
      document.removeEventListener('pointerover', onOver);
      document.removeEventListener('pointerout', onOut);
      document.removeEventListener('pointerdown', hide, true);
      document.removeEventListener('wheel', hide, true);
    };
  }, []);

  // Above the element, centered; below it if there is no room, and always inside the window.
  useLayoutEffect(() => {
    const element = box.current;
    if (!tip || !element) {
      return;
    }
    const { width, height } = element.getBoundingClientRect();
    const { anchor } = tip;
    const left = anchor.left + anchor.width / 2 - width / 2;
    const above = anchor.top - GAP - height;
    element.style.left = `${Math.max(EDGE, Math.min(left, window.innerWidth - width - EDGE))}px`;
    element.style.top = `${above >= EDGE ? above : anchor.bottom + GAP}px`;
  }, [tip]);

  return tip && (
    <div ref={box} className="Tip">
      {tip.text}
    </div>
  );
};
