import { useEffect, useState } from 'react';
import { useBackend } from '../backend';
import { Button, Dropdown, Section } from '../components';
import './CharacterEditor.scss';

const Field = ({ label, children }) => <label className="CharacterEditor__field"><span>{label}</span>{children}</label>;
const Select = ({ label, value, options = [], onChange, disabled }) => <Field label={label}>
  <Dropdown aria-label={label} selected={String(value ?? '')} disabled={disabled} width="100%"
    options={options.map(option => ({ value: String(option.id), displayText: option.name }))}
    onSelected={onChange} /></Field>;
const DraftInput = ({ label, value, onCommit, type = 'text', min, max }) => {
  const [draft, setDraft] = useState(String(value ?? ''));
  useEffect(() => setDraft(String(value ?? '')), [value]);
  const commit = () => { if (draft !== String(value)) onCommit(type === 'number' ? Number(draft) : draft); };
  return <Field label={label}><input aria-label={label} type={type} min={min} max={max} value={draft}
    onChange={event => setDraft(event.target.value)} onBlur={commit}
    onKeyDown={event => { if (event.key === 'Enter') event.currentTarget.blur(); }} /></Field>;
};
const palette = ['#181818', '#5b3820', '#965a36', '#d6b36a', '#f0e2c4', '#e7e7e7', '#a12e23', '#e15b9d', '#3878c4', '#39a373'];
const ColorEditor = ({ label, value = '#000000', disabled = false, hint, onChange }) => {
  const [color, setColor] = useState(value);
  const [hex, setHex] = useState(value);
  useEffect(() => { setColor(value); setHex(value); }, [value]);
  const commit = next => { setColor(next); setHex(next); onChange(next); };
  const channels = [1, 3, 5].map(offset => parseInt(color.slice(offset, offset + 2), 16) || 0);
  return <fieldset className="CharacterEditor__color" disabled={disabled} aria-label={label}>
    <legend>{label}</legend>
    <div className="CharacterEditor__colorLine">
      <span className="CharacterEditor__swatch" style={{ backgroundColor: color }} title={color} />
      <input aria-label={label + ' HEX'} value={hex} maxLength={7} spellCheck={false}
        onChange={event => setHex(event.target.value)} onBlur={() => /^#[0-9a-f]{6}$/i.test(hex) ? commit(hex) : setHex(color)}
        onKeyDown={event => { if (event.key === 'Enter') event.currentTarget.blur(); }} />
    </div>
    {channels.map((channel, index) => <label className="CharacterEditor__rgb" key={index}>
      <span>{['R', 'G', 'B'][index]}</span><input aria-label={label + ' ' + ['R', 'G', 'B'][index]} type="range" min="0" max="255" value={channel}
        onChange={event => { const next = [...channels]; next[index] = Number(event.target.value); commit('#' + next.map(value => value.toString(16).padStart(2, '0')).join('')); }} />
      <span>{channel}</span></label>)}
    <div className="CharacterEditor__palette">{palette.map(next => <button type="button" key={next} aria-label={label + ' ' + next}
      title={next} style={{ backgroundColor: next }} onClick={() => commit(next)} />)}</div>
    {hint && <small>{hint}</small>}
  </fieldset>;
};
const consentOptions = [{ id: '0', name: 'Запрещено' }, { id: '1', name: 'Спросить' }, { id: '2', name: 'Разрешено' }];
const priorities = [{ id: '0', name: 'Никогда' }, { id: '1', name: 'Низкий' }, { id: '2', name: 'Средний' }, { id: '3', name: 'Высокий' }];

export const CharacterEditor = () => {
  const { data, act } = useBackend();
  const [search, setSearch] = useState('');
  const [confirmClear, setConfirmClear] = useState(false);
  const change = action => value => act(action, { value });
  if (!data.available) return <div className="CharacterEditor">Выберите персонажа.</div>;
  const matches = name => name.toLowerCase().includes(search.toLowerCase());
  return <div className={'CharacterEditor CharacterEditor--' + data.mode}>
    {data.mode === 'identity' && <>
      <Button fluid selected={data.showClothes} onClick={() => act('clothes')}>Показывать одежду</Button>
      <Section fitted><DraftInput label="Имя" value={data.name} onCommit={change('name')} />
        <Button fluid onClick={() => act('random-name')}>Случайное имя</Button>
        <Button fluid onClick={() => act('random-all')}>Случайный персонаж</Button>
        <small className="CharacterEditor__warning">Имя должно соответствовать правилам именования выбранной расы.</small>
      </Section>
      <div className="CharacterEditor__buttons">
        <Button fluid color="good" disabled={!data.dirty} onClick={() => act('save')}>Сохранить</Button>
        <Button fluid disabled={!data.dirty} onClick={() => act('reset')}>Сбросить</Button>
        <Button fluid onClick={() => act('import')}>Импорт</Button>
        <Button fluid onClick={() => act('export')}>Экспорт</Button>
        <Button fluid onClick={() => act('export-image')}>Экспорт изображения</Button>
        <Button fluid onClick={() => act('open-images')}>Открыть изображения</Button>
      </div>
    </>}
    {data.mode === 'appearance' && <>
      <div className="CharacterEditor__grid">
        <div><Select label="Раса" value={data.species} options={data.speciesOptions} onChange={change('species')} />
          <Button icon="book" onClick={() => act('species-guide')}>Справка о расе</Button></div>
        <DraftInput label="Возраст" type="number" min={data.minAge} max={data.maxAge} value={data.age} onCommit={change('age')} />
        <Select label="Пол" value={data.sex} options={data.sexOptions} onChange={change('sex')} />
        <Select label="Местоимения" value={data.gender} options={data.genderOptions} onChange={change('gender')} />
        <Select label="Эротические взаимодействия" value={data.erp} options={consentOptions} onChange={change('erp')} />
        <Select label="Взаимодействия без согласия" value={data.noncon} options={consentOptions} onChange={change('noncon')} />
        <Select label="Vore" value={data.vore} options={consentOptions} onChange={change('vore')} />
        <Select label="Приоритет появления" value={data.spawn} options={data.spawnOptions} onChange={change('spawn')} />
        {['height', 'width'].map((key, index) => <Field key={key} label={['Рост', 'Ширина'][index]}>
          <input aria-label={['Рост', 'Ширина'][index]} type="range" min="0.8" max="1.2" step="0.01" value={data[key]} onChange={event => change(key)(Number(event.target.value))} />
          <span className="CharacterEditor__rangeValue">{Math.round(data[key] * 100)}%</span>
          <Button fluid onClick={() => change(key)(1)}>Сбросить</Button>
        </Field>)}
      </div>
      {data.humanSkin ? <Field label="Цвет кожи"><div className="CharacterEditor__colorLine">
        <span className="CharacterEditor__swatch" style={{ backgroundColor: data.skinColor }} />
        <input aria-label="Цвет кожи" type="range" min="0" max="100" value={data.skinTone} onChange={event => change('skin-tone')(Number(event.target.value))} />
      </div></Field> : <ColorEditor label="Цвет кожи" value={data.skinColor} onChange={change('skin-color')} />}
      <div className="CharacterEditor__hair">
        {['hair', 'beard'].map((key, index) => {
          const locked = data[key + 'Locked'];
          const missing = data[key + 'Style'] === (index ? 'FacialHairShaved' : 'HairBald');
          return <Section key={key} title={index ? 'Борода и усы' : 'Причёска'}>
            <Select label={index ? 'Стиль бороды' : 'Стиль волос'} value={data[key + 'Style']} options={data[key + 'Options']} onChange={change(key + '-style')} />
            <ColorEditor label={index ? 'Цвет бороды' : 'Цвет волос'} value={locked ? data.skinColor : data[key + 'Color']}
              disabled={locked || missing} onChange={change(key + '-color')}
              hint={locked ? 'Цвет этой расы совпадает с цветом кожи.' : missing ? 'Выберите стиль, чтобы изменить цвет.' : 'Изменения сразу видны на персонаже слева.'} />
          </Section>;
        })}
      </div>
      <ColorEditor label="Цвет глаз" value={data.eyeColor} onChange={change('eye-color')} />
    </>}
    {['jobs', 'traits'].includes(data.mode) && <DraftInput label="Поиск" value={search} onCommit={setSearch} />}
    {data.mode === 'jobs' && <>
      <Select label="Если предпочтения недоступны" value={data.unavailable}
        options={[{ id: '0', name: 'Остаться в лобби' }, { id: '1', name: 'Выбрать запасную профессию' }]} onChange={change('unavailable')} />
      {(data.departments || []).map(department => <Section key={department.id} title={department.name}>
        {department.jobs.filter(job => matches(job.name)).map(job => <div key={job.id} className="CharacterEditor__job">
          <div title={job.description}><strong>{job.name}</strong>{!job.allowed && <small>{job.reason}</small>}</div>
          <Select label={'Приоритет: ' + job.name} value={job.priority} options={priorities} disabled={!job.allowed}
            onChange={value => act('job', { id: job.id, value: Number(value) })} />
          <Button icon="book" tooltip="Справка" onClick={() => act('job-guide', { id: job.id })} />
        </div>)}
      </Section>)}
    </>}
    {data.mode === 'traits' && <>
      <Button color="bad" onClick={() => { if (confirmClear) { act('clear-traits'); setConfirmClear(false); } else setConfirmClear(true); }}>
        {confirmClear ? 'Подтвердить удаление всех черт' : 'Очистить все черты'}</Button>
      {confirmClear && <Button onClick={() => setConfirmClear(false)}>Отмена</Button>}
      {[...new Set((data.traits || []).map(trait => trait.category))].map(category => {
        const points = data.traitPoints?.find(item => item.name === category);
        return <Section key={category} title={category}>
          {points && <div className="CharacterEditor__points">Осталось очков: {Math.max(0, points.max - points.used)} / {points.max}
            <progress max={Math.max(1, points.max)} value={Math.max(0, points.max - points.used)} /></div>}
          <div className="CharacterEditor__traits">{data.traits.filter(trait => trait.category === category && matches(trait.name)).map(trait =>
            <Button fluid key={trait.id} selected={trait.selected} tooltip={trait.description} disabled={!trait.selected && points && points.used + trait.cost > points.max}
              onClick={() => act('trait', { id: trait.id })}>{trait.name} ({trait.cost})</Button>)}</div>
        </Section>;
      })}
    </>}
    {data.mode === 'company' && <>
      <Select label="Компания" value={data.company} options={data.companyOptions} onChange={change('company')} />
      <Section><div className="CharacterEditor__description" dangerouslySetInnerHTML={{ __html: data.companyDescriptionHtml || '' }} /></Section>
      {data.companyImage && <img className="CharacterEditor__companyImage" src={data.companyImage} alt="Компания" />}
    </>}
  </div>;
};
