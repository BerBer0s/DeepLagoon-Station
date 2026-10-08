import { useRef } from 'react';
import { Button } from './Button';
import { ColorSquare } from './PlayerColorPicker';

export const CharacterTextEditor = ({ label, value, maxLength, onChange }) => {
  const input = useRef(null), selection = useRef([0, 0]);
  const remember = () => { if (input.current) selection.current = [input.current.selectionStart, input.current.selectionEnd]; };
  const wrap = (before, after = before) => {
    const [start, end] = selection.current;
    const selected = value.slice(start, end) || 'текст';
    const next = value.slice(0, start) + before + selected + after + value.slice(end);
    if (next.length > maxLength) return;
    onChange(next);
    requestAnimationFrame(() => { input.current?.focus(); input.current?.setSelectionRange(start + before.length, start + before.length + selected.length); remember(); });
  };
  return <div className="CharacterTextEditor"><div className="CharacterTextEditor__tools" role="toolbar" aria-label={'Форматирование: ' + label}>
    {[['Жирный', '**'], ['Курсив', '*'], ['Подчёркнутый', '__'], ['Зачёркнутый', '~~'], ['Спойлер', '||'], ['Код', '`']].map(([name, token]) =>
      <Button key={name} onMouseDown={event => event.preventDefault()} onClick={() => wrap(token)}>{name}</Button>)}
    <ColorSquare label="Цвет текста" value="#ffffff" onChange={color => wrap('-=' + (color.startsWith('#') ? color : '#' + color) + '(', ')=-')} />
  </div><small>Выделите текст и нажмите нужное оформление. Без выделения вставится заготовка.</small>
    <textarea ref={input} aria-label={label} value={value} maxLength={maxLength} onSelect={remember} onKeyUp={remember} onMouseUp={remember}
      onKeyDown={event => { if ((event.ctrlKey || event.metaKey) && ['b', 'i', 'u'].includes(event.key.toLowerCase())) { event.preventDefault(); remember(); wrap({ b: '**', i: '*', u: '__' }[event.key.toLowerCase()]); } }}
      onChange={event => { remember(); onChange(event.target.value); }} />
  </div>;
};
