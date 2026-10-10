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
import {
  formatNumber,
  formatSheets,
  type Recipe,
  type WireMaterial,
} from './model';
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

const COSTS_SHOWN = 3;

type RecipeRowProps = {
  recipe: Recipe;
  amount: number;
  selected: boolean;
  stock: Stock;
  materials: Map<string, WireMaterial>;
  /** Counts the queued presses of this recipe; restarts the button's flash. 0 for other rows. */
  pulse: number;
  separator: string;
  t: T;
  onSelect: (id: string) => void;
  onAmount: (id: string, amount: number) => void;
  onQueue: (id: string) => void;
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
      return `${formatNumber(value, separator)} ${t('unit-reagent')}`;
    default:
      return String(value);
  }
};

export const RecipeRow = memo(function RecipeRow({
  recipe,
  amount,
  selected,
  stock,
  materials,
  pulse,
  separator,
  t,
  onSelect,
  onAmount,
  onQueue,
}: RecipeRowProps) {
  const needs = needsOf(recipe, amount, stock);
  const status: Status = worstStatus(needs);
  const text = (need: Need, value: number) => amountText(need, value, materials, separator, t);

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

  return (
    <div
      className={`Recipe Recipe--${status}${selected ? ' Recipe--selected' : ''}`}
      data-recipe={recipe.id}
    >
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
            {needs.slice(0, COSTS_SHOWN).map((need) => {
              const have =
                need.kind === 'material' && need.status === 'later' ? need.free : need.have;
              const filled = need.need <= 0 ? 1 : Math.max(0, Math.min(1, have / need.need));
              return (
                <div
                  key={need.kind + need.id}
                  className={`Cost Cost--${need.status}`}
                  data-tip={`${nameOf(need, materials)}: ${text(need, need.have)} / ${text(need, need.need)}`}
                >
                  <span className="Cost__text">
                    <b>{text(need, need.need)}</b> {nameOf(need, materials)}
                  </span>
                  <span className="Cost__bar">
                    <i style={{ transform: `scaleX(${filled})` }} />
                  </span>
                </div>
              );
            })}
            {needs.length > COSTS_SHOWN && (
              <span className="Cost__more">+{needs.length - COSTS_SHOWN}</span>
            )}
          </div>
        </div>
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
          data-tip={status === 'later' ? t('short-after-queue') : undefined}
          onClick={() => onQueue(recipe.id)}
        >
          {status === 'short' ? t('queue-unavailable') : t('queue-action')}
        </button>
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
          <div className="Recipe__actions">
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
