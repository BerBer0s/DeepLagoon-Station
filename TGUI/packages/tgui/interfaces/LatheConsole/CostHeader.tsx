import type { CostLayout } from './costs';

type CostHeaderProps = { layout: CostLayout };

/**
 * The heads of the cost columns, above the list and outside its scroll. The cells of the rows carry only
 * numbers, so the head is what says which material a column is.
 */
export const CostHeader = ({ layout }: CostHeaderProps) => (
  <div className={`CostHeader${layout.compact ? ' CostHeader--compact' : ''}`}>
    <span className="CostHeader__fill" />
    <div className="CostHeader__columns">
      {layout.columns.map((column) => (
        <div key={column.id} className="CostHeader__cell" data-tip={column.name}>
          {column.icon && <img src={column.icon} alt="" draggable={false} />}
          <span className="CostHeader__name">{column.name}</span>
          {!column.icon && <span className="CostHeader__short">{column.name.slice(0, 3)}</span>}
        </div>
      ))}
      {layout.other && <div className="CostHeader__cell CostHeader__cell--other">…</div>}
    </div>
    <span className="CostHeader__slot" />
  </div>
);
