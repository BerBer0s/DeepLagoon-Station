import { Button, Icon } from '../../components';
import { DisciplineTabs } from './DisciplineTabs';
import type { Tech, WireDiscipline } from './model';
import type { SearchResult } from './search';
import { SearchBox } from './SearchBox';

type TopBarProps = {
  points: number;
  disciplines: WireDiscipline[];
  activeDiscipline: string | null;
  query: string;
  results: SearchResult[];
  techs: Map<string, Tech>;
  disciplineColors: Map<string, string>;
  labels: Record<string, string>;
  onDiscipline: (id: string | null) => void;
  onQuery: (query: string) => void;
  onPick: (id: string) => void;
  onServers: () => void;
};

/**
 * Points, discipline tabs, search and the server button. In a narrow window the points and the
 * button keep only their icons (the words are in their tips) and the tabs scroll sideways.
 */
export const TopBar = ({
  points,
  disciplines,
  activeDiscipline,
  query,
  results,
  techs,
  disciplineColors,
  labels,
  onDiscipline,
  onQuery,
  onPick,
  onServers,
}: TopBarProps) => (
  <header className="TopBar">
    <span className="TopBar__points" data-tip={labels['dl-research-points']}>
      <Icon name="flask" />
      <span className="TopBar__label">{labels['dl-research-points']}:</span>
      <b>{points}</b>
    </span>
    <DisciplineTabs
      disciplines={disciplines}
      active={activeDiscipline}
      labels={labels}
      onSelect={onDiscipline}
    />
    <SearchBox
      query={query}
      results={results}
      techs={techs}
      disciplineColors={disciplineColors}
      labels={labels}
      onChange={onQuery}
      onPick={onPick}
    />
    <Button
      aria-label={labels['dl-research-servers']}
      data-tip={labels['dl-research-servers']}
      icon="server"
      onClick={onServers}
    >
      <span className="TopBar__label">{labels['dl-research-servers']}</span>
    </Button>
  </header>
);
