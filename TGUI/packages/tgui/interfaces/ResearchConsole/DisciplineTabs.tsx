import { type CSSProperties, memo, type WheelEvent } from 'react';

import type { WireDiscipline } from './model';

type DisciplineTabsProps = {
  disciplines: WireDiscipline[];
  /** The discipline the tree is narrowed to, or null for all. */
  active: string | null;
  labels: Record<string, string>;
  onSelect: (id: string | null) => void;
};

// A vertical wheel turn over the tabs scrolls them sideways when they do not all fit.
const scrollSideways = (event: WheelEvent<HTMLElement>) => {
  event.currentTarget.scrollLeft += event.deltaY;
};

export const DisciplineTabs = memo(function DisciplineTabs({
  disciplines,
  active,
  labels,
  onSelect,
}: DisciplineTabsProps) {
  return (
    <nav className="DisciplineTabs" onWheel={scrollSideways}>
      <button
        type="button"
        className={`DisciplineTab${active === null ? ' DisciplineTab--active' : ''}`}
        onClick={() => onSelect(null)}
      >
        {labels['dl-research-tabs-all']}
      </button>
      {disciplines.map((discipline) => (
        <button
          key={discipline.id}
          type="button"
          className={`DisciplineTab${discipline.id === active ? ' DisciplineTab--active' : ''}`}
          style={{ '--discipline': discipline.color } as CSSProperties}
          data-tip={discipline.name}
          aria-label={discipline.name}
          onClick={() => onSelect(discipline.id === active ? null : discipline.id)}
        >
          {discipline.icon ? (
            <img src={discipline.icon} alt="" draggable={false} />
          ) : (
            discipline.shortName
          )}
        </button>
      ))}
    </nav>
  );
});
