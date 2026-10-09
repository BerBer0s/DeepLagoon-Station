import { type CSSProperties, memo } from 'react';

import { Button, Icon } from '../../components';
import { recipeIconKey } from './icons';
import type { Tech, TechState, WireDiscipline } from './model';
import { describeTech, STATE_ICON } from './status';
import { TechIcon } from './TechIcon';

/** Width of the open panel, and of the strip it folds into; the tree reserves the same space. */
export const PANEL_WIDTH = 320;
export const PANEL_STRIP_WIDTH = 36;

type TechLinkProps = {
  tech: Tech;
  state: TechState;
  color: string | undefined;
  hub: boolean;
  labels: Record<string, string>;
  onNavigate: (id: string) => void;
};

const NO_HUBS = new Set<string>();

const TechLink = ({ tech, state, color, hub, labels, onNavigate }: TechLinkProps) => (
  <button
    type="button"
    className={`DetailsLink DetailsLink--${state}`}
    style={{ '--discipline': color } as CSSProperties}
    onClick={() => onNavigate(tech.id)}
  >
    <Icon name={STATE_ICON[state]} />
    <span className="DetailsLink__name">{tech.name}</span>
    {hub && <span className="DetailsLink__tag">{labels['dl-research-details-hub']}</span>}
  </button>
);

type DetailsPanelProps = {
  tech: Tech;
  state: TechState;
  discipline: WireDiscipline | undefined;
  points: number;
  canResearch: boolean;
  techs: Map<string, Tech>;
  states: Map<string, TechState>;
  disciplineColors: Map<string, string>;
  /** The technologies that need this one. */
  dependents: string[];
  /** The hubs among the prerequisites; their lines are not drawn unless asked for. */
  hubs: Set<string> | undefined;
  collapsed: boolean;
  labels: Record<string, string>;
  onToggle: () => void;
  onNavigate: (id: string) => void;
  onResearch: (id: string) => void;
};

export const DetailsPanel = memo(function DetailsPanel({
  tech,
  state,
  discipline,
  points,
  canResearch,
  techs,
  states,
  disciplineColors,
  dependents,
  hubs = NO_HUBS,
  collapsed,
  labels,
  onToggle,
  onNavigate,
  onResearch,
}: DetailsPanelProps) {
  if (collapsed) {
    return (
      <aside className="Details Details--collapsed" style={{ width: PANEL_STRIP_WIDTH }}>
        <Button
          aria-label={labels['dl-research-details-show']}
          data-tip={labels['dl-research-details-show']}
          icon="chevron-left"
          onClick={onToggle}
        />
      </aside>
    );
  }

  const hint = describeTech(tech, state, points, canResearch, labels);
  const link = (id: string, hub: boolean) => {
    const target = techs.get(id);
    return (
      target && (
        <TechLink
          key={id}
          tech={target}
          state={states.get(id) ?? 'locked'}
          color={disciplineColors.get(target.discipline)}
          hub={hub}
          labels={labels}
          onNavigate={onNavigate}
        />
      )
    );
  };
  return (
    <aside
      className="Details"
      style={
        {
          width: PANEL_WIDTH,
          '--discipline': disciplineColors.get(tech.discipline),
        } as CSSProperties
      }
    >
      <header className="Details__head">
        <TechIcon id={tech.id} className="Details__icon" />
        <div className="Details__title">
          <h2 className="Details__name">{tech.name}</h2>
          <div className="Details__meta">
            <span className="Details__discipline">{discipline?.name}</span>
            <span>
              {labels['dl-research-tier']} {tech.tier}
            </span>
            <span className="Details__cost" data-tip={labels['dl-research-cost']}>
              <Icon name="coins" /> {tech.cost}
            </span>
          </div>
        </div>
        <Button
          aria-label={labels['dl-research-details-hide']}
          data-tip={labels['dl-research-details-hide']}
          icon="chevron-right"
          onClick={onToggle}
        />
      </header>
      <Button
        fluid
        color={hint.armed ? 'good' : 'default'}
        disabled={!hint.armed}
        icon={STATE_ICON[state]}
        onClick={() => onResearch(tech.id)}
      >
        {hint.armed
          ? `${labels['dl-research-research']} · ${tech.cost}`
          : state === 'locked'
            ? labels['dl-research-locked']
            : hint.text}
      </Button>
      {state === 'locked' && <div className="Details__reason">{hint.text}</div>}
      <div key={tech.id} className="Details__scroll">
        {tech.effects.length > 0 && (
          <section className="Details__section">
            <h3>{labels['dl-research-details-effects']}</h3>
            <ul className="Details__effects">
              {tech.effects.map((effect, index) => (
                <li key={index}>{effect.text}</li>
              ))}
            </ul>
          </section>
        )}
        {tech.requires.length > 0 && (
          <section className="Details__section">
            <h3>{labels['dl-research-details-requires']}</h3>
            {tech.requires.map((prerequisite) =>
              techs.has(prerequisite.id) ? (
                link(prerequisite.id, hubs.has(prerequisite.id))
              ) : (
                <div key={prerequisite.id} className="DetailsLink DetailsLink--locked DetailsLink--outside">
                  <Icon name="lock" />
                  <span className="DetailsLink__name">{prerequisite.name}</span>
                  <span className="DetailsLink__tag">
                    {labels['dl-research-details-outside']}
                  </span>
                </div>
              ),
            )}
          </section>
        )}
        {dependents.length > 0 && (
          <section className="Details__section">
            <h3>{labels['dl-research-details-opens']}</h3>
            {dependents.map((id) => link(id, false))}
          </section>
        )}
        {tech.recipes.length > 0 && (
          <section className="Details__section">
            <h3>{labels['dl-research-details-recipes']}</h3>
            <ul className="Details__recipes">
              {tech.recipes.map((recipe) => (
                <li key={recipe.id}>
                  <TechIcon id={recipeIconKey(recipe.id)} className="Details__recipeIcon" />
                  <span>{recipe.name}</span>
                </li>
              ))}
            </ul>
          </section>
        )}
      </div>
    </aside>
  );
});
