import { useSelector } from 'common/redux';
import { useEffect, useState } from 'react';
import { Button, Input, Section } from 'tgui/components';

export const emoteReducer = (state = { available: [], pinned: [] }, action) =>
  action.type === 'deeplagoon/emotes' ? action.payload : state;
const act = (type, id) => Byond.topic({ type: 'act/' + type, payload: JSON.stringify({ id }) });

export const EmoteBar = () => {
  const { available, pinned } = useSelector(state => state.emotes);
  const [expanded, setExpanded] = useState(false);
  const [search, setSearch] = useState('');
  useEffect(() => {
    if (!expanded) return;
    const close = event => { if (event.key === 'Escape') setExpanded(false); };
    window.addEventListener('keydown', close);
    return () => window.removeEventListener('keydown', close);
  }, [expanded]);
  const play = id => { act('emote', id); setExpanded(false); };
  if (!available.length && !pinned.length) return null;
  const choices = available.filter(emote => emote.name.toLowerCase().includes(search.toLowerCase()));
  return <Section fitted>
    <div className="EmoteBar">
      {pinned.map(id => {
        const emote = available.find(candidate => candidate.id === id);
        return <Button key={id} disabled={!emote} tooltip={emote ? 'Нажмите, чтобы выполнить эмоцию' : 'Эмоция сейчас недоступна'}
          onClick={() => play(id)}>{emote?.name || id}</Button>;
      })}
      <Button icon={expanded ? 'chevron-up' : 'face-smile'} selected={expanded} tooltip="Выбрать или закрепить эмоцию" onClick={() => setExpanded(!expanded)}>{expanded ? 'Скрыть эмоции' : 'Эмоции'}</Button>
    </div>
    {expanded && <div className="EmoteBar__picker">
      <div className="EmoteBar__header"><strong>Выбрать эмоцию</strong>
        <Button icon="times" onClick={() => setExpanded(false)}>Закрыть</Button></div>
      <Input fluid placeholder="Найти эмоцию…" value={search} onInput={(_, value) => setSearch(value)} />
      <div className="EmoteBar__grid">
      {choices.map(emote => <div key={emote.id} className="EmoteBar__row">
        <Button grow tooltip={emote.name} onClick={() => play(emote.id)}>{emote.name}</Button>
        <Button icon="star" selected={pinned.includes(emote.id)} tooltip={pinned.includes(emote.id) ? 'Открепить' : 'Закрепить'}
          onClick={() => act('emote-pin', emote.id)} />
      </div>)}
      {pinned.filter(id => !available.some(emote => emote.id === id)).map(id =>
        <Button key={id} icon="times" tooltip="Открепить недоступную эмоцию" onClick={() => act('emote-pin', id)}>{id}</Button>)}
      </div>
      {!choices.length && <div>Эмоции не найдены</div>}
      <small>Нажмите на эмоцию, чтобы выполнить её; ★ закрепляет кнопку в чате.</small>
    </div>}
  </Section>;
};
