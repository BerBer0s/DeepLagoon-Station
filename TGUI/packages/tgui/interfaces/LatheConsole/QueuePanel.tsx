import type { KeyboardEvent } from 'react';

import { Icon } from '../../components';
import { type Current, formatNumber, type QueueEntry } from './model';
import {
  type Printing,
  type ProgressMode,
  useEstimateStyle,
  useIndeterminateStyle,
} from './printing';
import { RecipeIcon } from './RecipeIcon';
import type { T } from './text';

type ProgressProps = { printing: Printing; mode: ProgressMode };

/**
 * Two ways to show a print, because the page does not measure it, it only knows when the server says it
 * began and how long it takes. `estimate` is a bar filling over that time. `indeterminate` is a band going
 * round, which claims only that something is printing. Both step four times a second.
 */
const Progress = ({ printing, mode }: ProgressProps) => {
  const estimate = useEstimateStyle(mode === 'estimate' ? printing : null);
  const indeterminate = useIndeterminateStyle(mode === 'indeterminate' ? printing : null);
  return (
    <div className={`Progress Progress--${mode}`}>
      <i key={printing.key} style={mode === 'estimate' ? estimate : indeterminate} />
    </div>
  );
};

type QueuePanelProps = {
  current: Current;
  printing: Printing | null;
  progressMode: ProgressMode;
  queue: QueueEntry[];
  looping: boolean;
  skipping: boolean;
  separator: string;
  t: T;
  onCancel: (index: number) => void;
  onLoop: (value: boolean) => void;
  onSkip: (value: boolean) => void;
  /** Scrolls the list to the row of the recipe that is printing. */
  onLocate: () => void;
};

export const QueuePanel = ({
  current,
  printing,
  progressMode,
  queue,
  looping,
  skipping,
  separator,
  t,
  onCancel,
  onLoop,
  onSkip,
  onLocate,
}: QueuePanelProps) => {
  // The batch the current print belongs to, while it still has items to print.
  const batch = current.id ? queue.find((entry) => entry.id === current.id) : undefined;
  return (
    <>
      <section className="Block Current">
        <h4 className="Block__title">
          <span>{t('current')}</span>
        </h4>
        {current.id ? (
          <div
            className="Current__card"
            role="button"
            tabIndex={0}
            onClick={onLocate}
            onKeyDown={(event: KeyboardEvent<HTMLDivElement>) => {
              if (event.key === 'Enter' || event.key === ' ') {
                event.preventDefault();
                onLocate();
              }
            }}
          >
            <div className="Current__line">
              <RecipeIcon id={current.id} className="Current__icon" />
              <div className="Current__name">
                <span>{current.name}</span>
                {batch && batch.requested > 1 && (
                  <small>
                    {batch.printed}/{batch.requested}
                  </small>
                )}
              </div>
              <Icon name="crosshairs" className="Current__locate" />
            </div>
            {printing ? (
              <>
                <Progress printing={printing} mode={progressMode} />
                {progressMode === 'estimate' && (
                  <div className="Current__meta">
                    <span>
                      ≈ {formatNumber(printing.total, separator)} {t('time-unit')}
                    </span>
                  </div>
                )}
              </>
            ) : (
              <div className="Current__waiting">{t('current-waiting')}</div>
            )}
          </div>
        ) : (
          <div className="Current__idle">{t('current-idle')}</div>
        )}
      </section>
      <section className="Block Toggles">
        <button
          type="button"
          className={`Chip${looping ? ' Chip--active' : ''}`}
          aria-pressed={looping}
          data-tip={t('loop-tip')}
          onClick={() => onLoop(!looping)}
        >
          <Icon name="repeat" /> {t('loop')}
        </button>
        <button
          type="button"
          className={`Chip${skipping ? ' Chip--active' : ''}`}
          aria-pressed={skipping}
          data-tip={t('skip-tip')}
          onClick={() => onSkip(!skipping)}
        >
          <Icon name="forward" /> {t('skip')}
        </button>
      </section>
      <section className="Block Queue">
        <h4 className="Block__title">
          <span>{t('queue')}</span>
          {queue.length > 0 && <span className="Block__note">{queue.length}</span>}
        </h4>
        <div className="Queue__list">
          {queue.length === 0 && <div className="Queue__empty">{t('queue-empty')}</div>}
          {queue.map((entry) => (
            <div key={entry.index} className="QueueItem">
              <RecipeIcon id={entry.id} className="QueueItem__icon" />
              <span className="QueueItem__name">{entry.name}</span>
              <span className="QueueItem__count">
                {entry.requested > 1 ? `${entry.printed}/${entry.requested}` : ''}
              </span>
              <button
                type="button"
                className="QueueItem__cancel"
                aria-label={t('queue-cancel')}
                data-tip={t('queue-cancel')}
                onClick={() => onCancel(entry.index)}
              >
                <Icon name="times" />
              </button>
            </div>
          ))}
        </div>
      </section>
    </>
  );
};
