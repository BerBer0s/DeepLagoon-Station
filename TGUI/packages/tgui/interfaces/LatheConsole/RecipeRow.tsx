import { type KeyboardEvent, memo } from 'react';

import { Icon } from '../../components';
import {
  maxAmount,
  type Need,
  needsOf,
  type Status,
  type Stock,
  worstStatus,
} from './availability';
import type { CostLayout } from './costs';
import {
  formatNumber,
  formatSheets,
  type Recipe,
  type WireMaterial,
} from './model';
import { type Printing, useStripeStyle } from './printing';
import { RecipeIcon } from './RecipeIcon';
import type { T } from './text';

export const MAX_AMOUNT = 999;

// The expanded row: the description (two lines at most), the facts, the needs in two columns, the actions.
const DETAILS_PADDING = 12;
const DETAILS_DESCRIPTION = 40;
const DETAILS_FACTS = 28;
const DETAILS_NEED = 22;
const DETAILS_ACTIONS = 36;

export const detailsHeight = (recipe: Recipe): number => {
  const needs = recipe.mats.length + recipe.ents.length + recipe.reagents.length;
  return (
    DETAILS_PADDING +
    (recipe.desc ? DETAILS_DESCRIPTION : 0) +
    DETAILS_FACTS +
    Math.ceil(needs / 2) * DETAILS_NEED +
    DETAILS_ACTIONS
  );
};

type RecipeRowProps = {
  recipe: Recipe;
  amount: number;
  selected: boolean;
  stock: Stock;
  materials: Map<string, WireMaterial>;
  layout: CostLayout;
  /** Set on the row of the recipe that is being printed. */
  printing: Printing | null;
  /** How far the batch of the printing recipe is; 0 and 0 when the print is the last of its batch. */
  batchPrinted: number;
  batchRequested: number;
  /** Counts the requests to show this row; the row blinks once for each. 0 for other rows. */
  flash: number;
  /** Counts the queued presses of this recipe; restarts the button's flash. 0 for other rows. */
  pulse: number;
  separator: string;
  t: T;
  onSelect: (id: string) => void;
  onAmount: (id: string, amount: number) => void;
  onQueue: (id: string) => void;
  onFlashEnd: () => void;
};

const nameOf = (need: Need, materials: Map<string, WireMaterial>) =>
  need.kind === 'material' ? (materials.get(need.id)?.name ?? need.id) : need.name;

const amountText = (
  need: Need,
  value: number,
  materials: Map<string, WireMaterial>,
  separator: string,
  t: T,
) => {
  switch (need.kind) {
    case 'material':
      return formatSheets(value, materials.get(need.id)?.sheet ?? 100, separator);
    case 'reagent':
      return `${formatNumber(value, separator)} ${t('unit-reagent')}`;
    default:
      return String(value);
  }
};

/** What of a need is in stock, as a fraction of what is needed: the cell's bar. */
const filledOf = (need: Need) => {
  const have = need.kind === 'material' && need.status === 'later' ? need.free : need.have;
  return need.need <= 0 ? 1 : Math.max(0, Math.min(1, have / need.need));
};

export const RecipeRow = memo(function RecipeRow({
  recipe,
  amount,
  selected,
  stock,
  materials,
  layout,
  printing,
  batchPrinted,
  batchRequested,
  flash,
  pulse,
  separator,
  t,
  onSelect,
  onAmount,
  onQueue,
  onFlashEnd,
}: RecipeRowProps) {
  const needs = needsOf(recipe, amount, stock);
  const status: Status = worstStatus(needs);
  const text = (need: Need, value: number) => amountText(need, value, materials, separator, t);
  const stripe = useStripeStyle(printing);

  let sub = '';
  if (status === 'later') {
    sub = t('short-after-queue');
  } else if (status === 'short') {
    const lacking = needs
      .filter((need) => need.status === 'short')
      .slice(0, 2)
      .map((need) => `${nameOf(need, materials)} ${text(need, need.need - need.have)}`);
    sub = `${t('short')}: ${lacking.join(', ')}`;
  }

  const onKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      onSelect(recipe.id);
    }
  };

  const setAmount = (value: number) =>
    onAmount(recipe.id, Math.max(1, Math.min(MAX_AMOUNT, Math.floor(value))));

  const most = selected ? maxAmount(recipe, stock, MAX_AMOUNT) : 0;

  // One cell per column, in the order of the columns; what has no column is counted in the last one.
  const columnIds = new Set(layout.columns.map((column) => column.id));
  const others = needs.filter((need) => need.kind !== 'material' || !columnIds.has(need.id));
  const othersStatus = worstStatus(others);

  return (
    <div
      className={[
        'Recipe',
        `Recipe--${status}`,
        selected ? 'Recipe--selected' : '',
        printing ? 'Recipe--printing' : '',
      ]
        .filter(Boolean)
        .join(' ')}
      data-recipe={recipe.id}
    >
      {printing && (
        <span className="Recipe__stripe" aria-hidden="true">
          <i key={printing.key} style={stripe} />
        </span>
      )}
      {flash > 0 && (
        <span key={flash} className="Recipe__flash" aria-hidden="true" onAnimationEnd={onFlashEnd} />
      )}
      <div className="Recipe__line">
        <div
          className="Recipe__main"
          role="button"
          tabIndex={0}
          aria-expanded={selected}
          onClick={() => onSelect(recipe.id)}
          onKeyDown={onKeyDown}
        >
          <RecipeIcon id={recipe.id} className="Recipe__icon" />
          <div className="Recipe__name">
            <span className="Recipe__title">{recipe.name}</span>
            {sub !== '' && !selected && <small className="Recipe__sub">{sub}</small>}
          </div>
          <div className="Recipe__costs">
            {layout.columns.map((column) => {
              const need = needs.find((entry) => entry.kind === 'material' && entry.id === column.id);
              return (
                <div
                  key={column.id}
                  className={`Cost${need ? ` Cost--${need.status}` : ' Cost--none'}`}
                  data-tip={
                    need
                      ? `${column.name}: ${text(need, need.have)} / ${text(need, need.need)}`
                      : undefined
                  }
                >
                  {need && (
                    <>
                      <b className="Cost__text">{text(need, need.need)}</b>
                      <span className="Cost__bar">
                        <i style={{ transform: `scaleX(${filledOf(need)})` }} />
                      </span>
                    </>
                  )}
                </div>
              );
            })}
            {layout.other && (
              <div
                className={`Cost Cost--other${others.length > 0 ? ` Cost--${othersStatus}` : ' Cost--none'}`}
                data-tip={
                  others.length > 0
                    ? others
                        .map(
                          (need) =>
                            `${nameOf(need, materials)}: ${text(need, need.have)} / ${text(need, need.need)}`,
                        )
                        .join('\n')
                    : undefined
                }
              >
                {others.length > 0 && <b className="Cost__text">+{others.length}</b>}
              </div>
            )}
          </div>
        </div>
        {printing ? (
          <div className="Recipe__badge" data-tip={t('current')}>
            {batchRequested > 0 ? (
              <span>
                {batchPrinted}/{batchRequested}
              </span>
            ) : (
              <Icon name="print" />
            )}
          </div>
        ) : (
          <div className="Recipe__actions">
            <div className="Amount">
              <button
                type="button"
                aria-label={t('amount-decrease')}
                disabled={amount <= 1}
                onClick={() => setAmount(amount - 1)}
              >
                <Icon name="minus" />
              </button>
              <input
                type="text"
                inputMode="numeric"
                value={amount}
                maxLength={3}
                aria-label={recipe.name}
                onFocus={(event) => event.target.select()}
                onChange={(event) => {
                  const digits = event.target.value.replace(/\D/g, '');
                  setAmount(digits === '' ? 1 : parseInt(digits, 10));
                }}
              />
              <button
                type="button"
                aria-label={t('amount-increase')}
                disabled={amount >= MAX_AMOUNT}
                onClick={() => setAmount(amount + 1)}
              >
                <Icon name="plus" />
              </button>
            </div>
            <button
              key={pulse}
              type="button"
              className={`Recipe__add${pulse > 0 ? ' Recipe__add--pulse' : ''}`}
              disabled={status !== 'ok'}
              aria-label={t('queue-action')}
              data-tip={status === 'later' ? t('short-after-queue') : undefined}
              onClick={() => onQueue(recipe.id)}
            >
              <Icon name={status === 'ok' ? 'plus' : status === 'later' ? 'clock' : 'ban'} />
              <span className="Recipe__addLabel">
                {status === 'short' ? t('queue-unavailable') : t('queue-action')}
              </span>
            </button>
          </div>
        )}
      </div>
      {selected && (
        <div className="Recipe__details" style={{ height: detailsHeight(recipe) }}>
          {recipe.desc !== '' && <p className="Recipe__desc">{recipe.desc}</p>}
          <div className="Recipe__facts">
            <span>
              {t('time')}: <b>{formatNumber(recipe.time, separator)}</b> {t('time-unit')}
            </span>
            {recipe.count > 1 && (
              <span>
                {t('count')}: <b>×{recipe.count}</b>
              </span>
            )}
            {recipe.yields.length > 0 && (
              <span>
                {t('yields')}:{' '}
                {recipe.yields
                  .map((entry) => {
                    const material = materials.get(entry.id);
                    return `${material?.name ?? entry.id} ${formatSheets(entry.n, material?.sheet ?? 100, separator)}`;
                  })
                  .join(', ')}
              </span>
            )}
          </div>
          <ul className="Recipe__needs">
            {needs.map((need) => (
              <li
                key={need.kind + need.id}
                className={`Recipe__need Recipe__need--${need.status}`}
              >
                <span className="Recipe__needName">{nameOf(need, materials)}</span>
                <span>
                  {text(need, need.kind === 'material' && need.status === 'later' ? need.free : need.have)}
                  {' / '}
                  <b>{text(need, need.need)}</b>
                </span>
              </li>
            ))}
          </ul>
          <div className="Recipe__footer">
            <button
              type="button"
              className="Recipe__max"
              disabled={most <= 0 || most === amount}
              onClick={() => setAmount(most)}
            >
              {t('max')} ({most})
            </button>
          </div>
        </div>
      )}
    </div>
  );
});
