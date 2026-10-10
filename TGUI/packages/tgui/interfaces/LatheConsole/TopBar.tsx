import { type KeyboardEvent, type RefObject, type WheelEvent } from 'react';

import { Button, Icon } from '../../components';
import type { T } from './text';

type SearchProps = {
  query: string;
  input: RefObject<HTMLInputElement | null>;
  t: T;
  onChange: (query: string) => void;
};

const Search = ({ query, input, t, onChange }: SearchProps) => {
  const onKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.nativeEvent.isComposing) {
      return;
    }
    // Leaving the field also ends the key press: the host sends a stray Backspace after Enter.
    if (event.key === 'Enter') {
      event.preventDefault();
      input.current?.blur();
    }
  };
  return (
    <div className="Search">
      <Icon name="search" />
      <input
        ref={input}
        type="text"
        value={query}
        maxLength={64}
        autoComplete="off"
        spellCheck={false}
        placeholder={t('search-placeholder')}
        aria-label={t('search-placeholder')}
        onChange={(event) => onChange(event.target.value)}
        onKeyDown={onKeyDown}
      />
      {query !== '' && (
        <button
          type="button"
          className="Search__clear"
          aria-label={t('search-clear')}
          data-tip={t('search-clear')}
          onClick={() => {
            onChange('');
            input.current?.focus();
          }}
        >
          <Icon name="times" />
        </button>
      )}
    </div>
  );
};

type TopBarProps = {
  query: string;
  input: RefObject<HTMLInputElement | null>;
  shown: number;
  total: number;
  servers: boolean;
  t: T;
  onQuery: (query: string) => void;
  onServers: () => void;
};

/** The search, how many recipes it shows, and the buttons that open other windows. */
export const TopBar = ({
  query,
  input,
  shown,
  total,
  servers,
  t,
  onQuery,
  onServers,
}: TopBarProps) => (
  <header className="LatheBar">
    <Search query={query} input={input} t={t} onChange={onQuery} />
    <span className="LatheBar__count" data-tip={t('recipes-count')}>
      {shown === total ? total : `${shown} / ${total}`}
    </span>
    <span className="LatheBar__spacer" />
    {servers && (
      <Button
        aria-label={t('servers')}
        data-tip={t('servers')}
        icon="server"
        onClick={onServers}
      >
        <span className="LatheBar__label">{t('servers')}</span>
      </Button>
    )}
  </header>
);

type CategoriesProps = {
  /** null is "all". */
  active: string | null;
  categories: { id: string; name: string; count: number }[];
  total: number;
  onlyAvailable: boolean;
  t: T;
  onSelect: (id: string | null) => void;
  onToggleAvailable: () => void;
};

// A vertical wheel turn over the chips scrolls them sideways when they do not all fit.
const scrollSideways = (event: WheelEvent<HTMLElement>) => {
  event.currentTarget.scrollLeft += event.deltaY;
};

export const Categories = ({
  active,
  categories,
  total,
  onlyAvailable,
  t,
  onSelect,
  onToggleAvailable,
}: CategoriesProps) => (
  <div className="Categories">
    <nav className="Categories__chips" onWheel={scrollSideways}>
      <button
        type="button"
        className={`Chip${active === null ? ' Chip--active' : ''}`}
        onClick={() => onSelect(null)}
      >
        {t('category-all')}
        <span className="Chip__count">{total}</span>
      </button>
      {categories.map((category) => (
        <button
          key={category.id}
          type="button"
          className={`Chip${category.id === active ? ' Chip--active' : ''}`}
          onClick={() => onSelect(category.id === active ? null : category.id)}
        >
          {category.name}
          <span className="Chip__count">{category.count}</span>
        </button>
      ))}
    </nav>
    <button
      type="button"
      className={`Chip Chip--toggle${onlyAvailable ? ' Chip--active Chip--good' : ''}`}
      aria-pressed={onlyAvailable}
      onClick={onToggleAvailable}
    >
      <Icon name="check" /> {t('only-available')}
    </button>
  </div>
);
