import {
  type CSSProperties,
  type KeyboardEvent,
  useRef,
  useState,
} from 'react';

import { Icon } from '../../components';
import type { Tech } from './model';
import type { SearchResult } from './search';
import { TechIcon } from './TechIcon';

/** The list shows this many results at most. */
export const MAX_SHOWN_RESULTS = 8;

type SearchBoxProps = {
  query: string;
  /** All matches; the list shows the first few. */
  results: SearchResult[];
  techs: Map<string, Tech>;
  disciplineColors: Map<string, string>;
  labels: Record<string, string>;
  onChange: (query: string) => void;
  onPick: (id: string) => void;
};

export const SearchBox = ({
  query,
  results,
  techs,
  disciplineColors,
  labels,
  onChange,
  onPick,
}: SearchBoxProps) => {
  const input = useRef<HTMLInputElement>(null);
  const [focused, setFocused] = useState(false);
  const [highlighted, setHighlighted] = useState(0);

  const shown = results.slice(0, MAX_SHOWN_RESULTS);
  const current = Math.min(highlighted, shown.length - 1);

  const pick = (id: string) => {
    onPick(id);
    // Leaving the field also ends the key press: the host sends a stray Backspace after Enter.
    input.current?.blur();
  };

  const onKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.nativeEvent.isComposing) {
      return;
    }
    switch (event.key) {
      case 'ArrowDown':
      case 'ArrowUp':
        event.preventDefault();
        setHighlighted(Math.max(0, Math.min(shown.length - 1, current + (event.key === 'ArrowDown' ? 1 : -1))));
        break;
      case 'Enter':
        event.preventDefault();
        if (shown[current]) {
          pick(shown[current].id);
        } else {
          input.current?.blur();
        }
        break;
    }
  };

  return (
    <div className="SearchBox">
      <Icon name="search" />
      <input
        ref={input}
        type="text"
        value={query}
        maxLength={64}
        autoComplete="off"
        spellCheck={false}
        placeholder={labels['dl-research-search-placeholder']}
        aria-label={labels['dl-research-search-placeholder']}
        onChange={(event) => {
          setHighlighted(0);
          onChange(event.target.value);
        }}
        onFocus={() => setFocused(true)}
        onBlur={() => setFocused(false)}
        onKeyDown={onKeyDown}
      />
      {query !== '' && (
        <button
          type="button"
          className="SearchBox__clear"
          aria-label={labels['dl-research-search-clear']}
          data-tip={labels['dl-research-search-clear']}
          onClick={() => {
            onChange('');
            input.current?.focus();
          }}
        >
          <Icon name="times" />
        </button>
      )}
      {focused && query.trim() !== '' && (
        <div className="SearchBox__list">
          {shown.length === 0 && (
            <div className="SearchBox__empty">{labels['dl-research-search-empty']}</div>
          )}
          {shown.map((result, index) => {
            const tech = techs.get(result.id);
            return (
              tech && (
                <button
                  key={result.id}
                  type="button"
                  className={`SearchResult${index === current ? ' SearchResult--current' : ''}`}
                  style={{ '--discipline': disciplineColors.get(tech.discipline) } as CSSProperties}
                  onPointerEnter={() => setHighlighted(index)}
                  onClick={() => pick(result.id)}
                >
                  <TechIcon id={tech.id} className="SearchResult__icon" />
                  <span className="SearchResult__text">
                    <span className="SearchResult__name">{tech.name}</span>
                    {result.recipe && (
                      <span className="SearchResult__recipe">
                        {labels['dl-research-search-recipe']}: {result.recipe}
                      </span>
                    )}
                  </span>
                </button>
              )
            );
          })}
          {results.length > shown.length && (
            <div className="SearchBox__more">
              {labels['dl-research-search-more']}: {results.length - shown.length}
            </div>
          )}
        </div>
      )}
    </div>
  );
};
