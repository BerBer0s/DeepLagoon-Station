import { Icon } from '../../components';
import { formatNumber, type StockEntry, type WireMaterial } from './model';
import type { T } from './text';

const EJECT_SHEETS = [1, 5, 10, 30];

type MaterialsProps = {
  stock: StockEntry[];
  materials: Map<string, WireMaterial>;
  /** What the queue will still use, per material. */
  reserved: Map<string, number>;
  /** What the recipe under the pointer or the selected one would use, per material. */
  preview: Map<string, number>;
  canEject: boolean;
  silo: boolean;
  /** Names and counts of what the machine's storage and beaker hold and some recipe needs. */
  extras: { id: string; name: string; text: string }[];
  materialMultiplier: number;
  timeMultiplier: number;
  separator: string;
  t: T;
  onEject: (id: string, sheets: number) => void;
};

const percent = (part: number, whole: number) =>
  whole <= 0 ? 0 : Math.max(0, Math.min(100, (part / whole) * 100));

/**
 * The stock of every material as one bar: what is free, what the queue will use (amber), and what the
 * recipe under the pointer would use (hatched). Nothing here changes as the pointer moves except
 * the hatching, so only that is repainted.
 */
export const Materials = ({
  stock,
  materials,
  reserved,
  preview,
  canEject,
  silo,
  extras,
  materialMultiplier,
  timeMultiplier,
  separator,
  t,
  onEject,
}: MaterialsProps) => {
  const multipliers: string[] = [];
  if (Math.abs(materialMultiplier - 1) > 0.001) {
    multipliers.push(`${t('multiplier-material')} ×${formatNumber(materialMultiplier, separator)}`);
  }
  if (Math.abs(timeMultiplier - 1) > 0.001) {
    multipliers.push(`${t('multiplier-time')} ×${formatNumber(timeMultiplier, separator)}`);
  }

  return (
    <section className="Block Materials">
      <h4 className="Block__title">
        <span>{t('materials')}</span>
        {multipliers.length > 0 && <span className="Block__note">{multipliers.join(' · ')}</span>}
      </h4>
      {stock.length === 0 && <div className="Materials__empty">{t('materials-empty')}</div>}
      {stock.map((entry) => {
        const info = materials.get(entry.id);
        const sheets = entry.n / Math.max(1, info?.sheet ?? 100);
        const queued = Math.min(entry.n, reserved.get(entry.id) ?? 0);
        const previewed = Math.min(entry.n - queued, preview.get(entry.id) ?? 0);
        const short = (preview.get(entry.id) ?? 0) > entry.n - queued;
        return (
          <div key={entry.id} className="Material">
            <div className="Material__line">
              {info?.icon && <img src={info.icon} alt="" draggable={false} />}
              <span className="Material__name">{info?.name ?? entry.name}</span>
              <span className="Material__amount">{entry.text}</span>
            </div>
            <div className="Material__bar">
              <div className={`Gauge${short ? ' Gauge--short' : ''}`}>
                <i
                  className="Gauge__free"
                  style={{ width: `${percent(entry.n - queued - previewed, entry.n)}%` }}
                />
                <i
                  className="Gauge__preview"
                  style={{ width: `${percent(previewed, entry.n)}%` }}
                />
                <i className="Gauge__queued" style={{ width: `${percent(queued, entry.n)}%` }} />
              </div>
              {canEject && (
                <div className="Material__eject" data-tip={t('eject')}>
                  {EJECT_SHEETS.map((count) => (
                    <button
                      key={count}
                      type="button"
                      disabled={Math.floor(sheets + 1e-6) < count}
                      onClick={() => onEject(entry.id, count)}
                    >
                      {count}
                    </button>
                  ))}
                </div>
              )}
            </div>
          </div>
        );
      })}
      {extras.map((extra) => (
        <div key={extra.id} className="Material Material--extra">
          <div className="Material__line">
            <span className="Material__name">{extra.name}</span>
            <span className="Material__amount">{extra.text}</span>
          </div>
        </div>
      ))}
      {silo && (
        <div className="Materials__silo">
          <Icon name="link" /> {t('silo-linked')}
        </div>
      )}
      {stock.length > 0 && (
        <div className="Legend">
          <span>
            <i className="Legend__free" />
            {t('legend-stock')}
          </span>
          <span>
            <i className="Legend__queued" />
            {t('legend-queue')}
          </span>
          <span>
            <i className="Legend__preview" />
            {t('legend-recipe')}
          </span>
        </div>
      )}
    </section>
  );
};
