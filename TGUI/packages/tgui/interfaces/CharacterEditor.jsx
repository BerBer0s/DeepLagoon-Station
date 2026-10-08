import { useEffect, useRef, useState } from 'react';
import { useBackend } from '../backend';
import { Button, Dropdown, Section } from '../components';
import './CharacterEditor.scss';
import { ColorSquare } from '../components/PlayerColorPicker';
import { playerTheme, TintedSprite } from '../components/PlayerTheme';
import { CharacterText, FormattingHint } from '../components/CharacterText';
import { EquipmentBrowser } from './EquipmentBrowser';

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
const ColorEditor = ColorSquare;
const consentOptions = [{ id: '0', name: 'Запрещено' }, { id: '1', name: 'Спросить' }, { id: '2', name: 'Разрешено' }];
const priorities = [{ id: '0', name: 'Никогда' }, { id: '1', name: 'Низкий' }, { id: '2', name: 'Средний' }, { id: '3', name: 'Высокий' }];

const HairCarousel = ({options,selected,color,onSelect}) => {
  const track=useRef(null);
  const turn=direction=>track.current?.scrollBy({left:direction*track.current.clientWidth,behavior:'smooth'});
  return <div className="HairCarousel"><button type="button" aria-label="Предыдущие четыре" onClick={()=>turn(-1)}>◀</button><div className="HairCarousel__track" ref={track}>
    {options.map(option=><button type="button" key={option.id} aria-pressed={selected===option.id} onClick={()=>onSelect(option.id)}><div className="CharacterEditor__sprite">{(option.images||[]).map((image,i)=><TintedSprite key={i} image={image.url} color={color} />)}</div><span>{option.name}</span></button>)}
  </div><button type="button" aria-label="Следующие четыре" onClick={()=>turn(1)}>▶</button></div>;
};

export const CharacterEditor = () => {
  const { data, act } = useBackend();
  const equipmentCache=useRef(null);
  if(data.equipment)equipmentCache.current=data.equipment;
  const [search, setSearch] = useState('');
  const [charactersOpen,setCharactersOpen]=useState(false),[statsOpen,setStatsOpen]=useState(false),[deleteSlot,setDeleteSlot]=useState(null);
  const [confirmClear, setConfirmClear] = useState(false);
  const change = action => value => act(action, { value });
  if (!data.available) return <div className="CharacterEditor">Выберите персонажа.</div>;
  const matches = name => name.toLowerCase().includes(search.toLowerCase());
  return <div className={'CharacterEditor Chat ' + playerTheme(data.chatState).className + ' CharacterEditor--' + data.mode} style={playerTheme(data.chatState).style}>
    {data.setup && <Button className="CharacterEditor__close" color="bad" onClick={()=>act('setup/close')}><span aria-hidden="true">×</span>Закрыть</Button>}
    <aside className="CharacterEditor__preview"><div className="CharacterEditor__previewSpace" />
      <div className="CharacterEditor__rotation"><Button onClick={()=>act('rotate',{value:-1})}>◀</Button><Button onClick={()=>act('rotate',{value:1})}>▶</Button><Button selected={data.showClothes} onClick={()=>act('clothes')}>Show</Button></div>
      <div className="CharacterEditor__slots">{(data.previewSlots||[]).map((slot,index)=><button type="button" key={slot.id} title={slot.name} style={{gridColumn:index%2?3:1,gridRow:Math.floor(index/2)+1}} aria-label={slot.name} aria-pressed={slot.selected} onClick={()=>act('preview-slot',{value:slot.id})}><div className="CharacterEditor__sprite">{(slot.images.length?slot.images:[{url:slot.background,color:"#ffffff"}]).map((image,i)=><TintedSprite key={i} image={image.url} color={image.color} />)}</div></button>)}</div>
    </aside>
    <main className="CharacterEditor__content">
    {data.setup && <>
      <div className="CharacterEditor__tabs"><Button onClick={()=>setCharactersOpen(!charactersOpen)} selected={charactersOpen}>Персонажи</Button><Button onClick={()=>setStatsOpen(!statsOpen)} selected={statsOpen}>Статистика</Button>{data.setup.notes&&<Button onClick={()=>act('setup/notes')}>Заметки администрации</Button>}</div>
      {charactersOpen&&<Section title="Персонажи"><div className="CharacterEditor__traits">{data.setup.characters.map(character=><div key={character.slot}><Button fluid selected={character.slot===data.setup.selected} onClick={()=>act('setup/select',{slot:character.slot})}>{character.name} · {character.balance}</Button>{character.slot!==data.setup.selected&&<Button color="bad" onClick={()=>{if(deleteSlot===character.slot){act('setup/delete',{slot:character.slot});setDeleteSlot(null);}else setDeleteSlot(character.slot);}}>{deleteSlot===character.slot?'Подтвердить удаление':'Удалить'}</Button>}</div>)}</div><Button disabled={!data.setup.canCreate} onClick={()=>act('setup/create')}>Создать персонажа</Button>{deleteSlot!==null&&<Button onClick={()=>setDeleteSlot(null)}>Отмена</Button>}</Section>}
      {statsOpen&&<Section title={'Общее время: '+data.setup.overallTime}><table><thead><tr><th>Профессия</th><th>Время</th></tr></thead><tbody>{data.setup.playtimes.map(time=><tr key={time.name}><td>{time.name}</td><td>{time.time}</td></tr>)}</tbody></table></Section>}
    </>}
    <nav className="CharacterEditor__tabs">{(data.tabs || []).map(tab=><Button key={tab.id} selected={tab.mode===data.mode} onClick={()=>act('select-tab',{value:tab.id})}>{tab.name}</Button>)}</nav>
    <div className="CharacterEditor__identity"><DraftInput label="Имя" value={data.name} onCommit={change('name')} />
      <Button disabled={!data.dirty} color="good" onClick={()=>act('save')}>Сохранить</Button><Button disabled={!data.dirty} onClick={()=>act('reset')}>Сбросить</Button>
      <Button selected={data.showClothes} onClick={()=>act('clothes')}>Одежда</Button>
      <Button onClick={()=>act('random-name')}>Случайное имя</Button><Button onClick={()=>act('random-all')}>Случайный персонаж</Button>
      <Button onClick={()=>act('import')}>Импорт</Button><Button onClick={()=>act('export')}>Экспорт</Button>
    </div>
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
      <ColorEditor label="Цвет кожи" value={data.skinColor} onChange={change('skin-color')} hint={data.humanSkin ? 'Цвет приводится к допустимому оттенку кожи.' : undefined} />
      <div className="CharacterEditor__hair">
        {data.hairOptions?.length <= 1 && data.beardOptions?.length <= 1 && <small>Для этой расы нет обычных причёсок и бороды. Доступные украшения головы настраиваются во вкладке «Особенности».</small>}
        {['hair', 'beard'].map((key, index) => {
          if ((data[key + 'Options'] || []).length <= 1) return null;
          const locked = data[key + 'Locked'];
          const missing = data[key + 'Style'] === (index ? 'FacialHairShaved' : 'HairBald');
          return <Section key={key} title={index ? 'Борода и усы' : 'Причёска'}>
            <details><summary>{index ? 'Выбор бороды и усов' : 'Выбор причёски'}</summary><HairCarousel options={data[key+'Options']||[]} selected={data[key+'Style']} color={locked ? data.skinColor : data[key+'Color']} onSelect={change(key+'-style')} /></details>
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
    {data.mode === 'saved' && <Section title="Сохранённые предметы"><div className="CharacterEditor__tiles">{(data.savedItems||[]).map((item,i)=><div key={i}><div className="CharacterEditor__sprite">{item.images.map((image,j)=><TintedSprite key={j} image={image.url} color={image.color} />)}</div>{item.name}</div>)}</div></Section>}
    {equipmentCache.current && <div style={{display:data.mode==='equipment'?'block':'none'}}><EquipmentBrowser data={equipmentCache.current} act={(action,payload)=>act('equipment/'+action,payload)} /></div>}
    {data.mode === 'flavor' && <>
      <Section title="Headshot">{data.headshot&&<img className="CharacterEditor__headshot" src={data.headshot} alt="Headshot персонажа"/>}<div><Button onClick={()=>act('headshot-upload')}>{data.hasHeadshot?'Заменить изображение':'Загрузить изображение'}</Button></div><div className="CharacterEditor__profileRow"><Button onClick={()=>act('character-card')}>Просмотр профиля</Button></div><Button disabled={!data.hasHeadshot} onClick={()=>act('headshot-download')}>Скачать изображение</Button><small>PNG/JPEG, до 1 МБ, одно изображение на персонажа. Сначала сохраните нового персонажа. Изображение доступно только через игру.</small>{data.headshotStatus&&<p>{data.headshotStatus}</p>}</Section>
      {[['flavor','Описание персонажа',data.flavorText],['ooc','OOC заметки',data.oocNotes]].map(([key,label,value])=><Section key={key} title={label}><textarea aria-label={label} value={value||''} maxLength={data.maxFlavorLength} onChange={e=>change(key)(e.target.value)}/><FormattingHint/><details><summary>Предпросмотр форматирования</summary><div className="CharacterText"><CharacterText text={value}/></div></details></Section>)}
    </>}
    {data.mode === 'markings' && <>
      <Section title="Выбранные особенности">{(data.markings||[]).map(marking=><div key={marking.index}>
        <strong>{marking.name}</strong><Button onClick={()=>act('marking-remove',{index:marking.index})}>Удалить</Button>
        <Button onClick={()=>act('marking-move',{index:marking.index,value:-1})}>↑</Button><Button onClick={()=>act('marking-move',{index:marking.index,value:1})}>↓</Button>
        {(marking.colors||[]).map((color,i)=><ColorEditor key={i} label={'Цвет '+(i+1)} value={color.name} disabled={marking.locked} onChange={value=>act('marking-color',{index:marking.index,layer:i,value})} />)}
      </div>)}</Section>
      <DraftInput label="Поиск" value={search} onCommit={setSearch} />
      {[...new Set((data.markingOptions||[]).map(m=>m.category))].map(category=><details key={category}><summary>{category}</summary><div className="CharacterEditor__tiles">{data.markingOptions.filter(m=>m.category===category&&matches(m.name)).map(option=><button type="button" key={option.id} onClick={()=>act('marking-add',{value:option.id})}><div className="CharacterEditor__sprite">{option.images.map((image,i)=><img key={i} src={image.url} alt="" />)}</div>{option.name}</button>)}</div></details>)}
    </>}
    {data.mode === 'company'  && <>
      <Select label="Компания" value={data.company} options={data.companyOptions} onChange={change('company')} />
      <Section><div className="CharacterEditor__description" dangerouslySetInnerHTML={{ __html: data.companyDescriptionHtml || '' }} /></Section>
      {data.companyImage && <img className="CharacterEditor__companyImage" src={data.companyImage} alt="Компания" />}
    </>}
    </main>
  </div>;
};
