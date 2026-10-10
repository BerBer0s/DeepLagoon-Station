import { type CSSProperties, useRef } from 'react';

import { Icon } from '../../components';
import { formatNumber, type Current, type QueueEntry } from './model';
import { RecipeIcon } from './RecipeIcon';
import type { T } from './text';

// The bar moves in this many steps per second: a smooth bar would repaint the whole window every
// frame, and four steps a second is smooth enough for a print of a few seconds.
const STEPS_PER_SECOND = 4;
const MAX_STEPS = 60;

type ProgressProps = { current: Current };

/**
 * One animation for the whole print, started where the server says the print is (negative delay).
 * The style is fixed per print: a later state of the same print must not restart the bar.
 */
const Progress = ({ current }: ProgressProps) => {
  const frozen = useRef<{ key: string | undefined; style: CSSProperties }>(undefined);
  let fixed = frozen.current;
  if (!fixed || fixed.key !== current.key) {
    const total = Math.max(0.1, current.total ?? 0.1);
    const steps = Math.max(1, Math.min(MAX_STEPS, Math.round(total * STEPS_PER_SECOND)));
    fixed = {
      key: current.key,
      style: {
        animationDuration: `${total}s`,
        animationDelay: `-${Math.min(total, current.elapsed ?? 0)}s`,
        animationTimingFunction: `steps(${steps}, end)`,
      },
    };
    frozen.current = fixed;
  }
  return (
    <div className="Progress">
      <i key={current.key} style={fixed.style} />
    </div>
  );
};

type QueuePanelProps = {
  current: Current;
  queue: QueueEntry[];
  looping: boolean;
  skipping: boolean;
  separator: string;
  t: T;
  onCancel: (index: number) => void;
  onLoop: (value: boolean) => void;
  onSkip: (value: boolean) => void;
};

export const QueuePanel = ({
  current,
  queue,
  looping,
  skipping,
  separator,
  t,
  onCancel,
  onLoop,
  onSkip,
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
          <div className="Current__card">
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
            </div>
            {current.active ? (
              <>
                <Progress current={current} />
                <div className="Current__meta">
                  <span>
                    {formatNumber(current.total ?? 0, separator)} {t('time-unit')}
                  </span>
                </div>
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
