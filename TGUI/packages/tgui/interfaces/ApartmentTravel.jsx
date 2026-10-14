import { useState } from 'react';
import { useBackend } from '../backend';
import './ApartmentTravel.scss';

export const ApartmentTravelPanel = () => {
  const { data, act } = useBackend();
  const [search, setSearch] = useState('');
  const [kick, setKick] = useState(null);
  const send = (action, target) => act(action, target ? { target } : {});
  const people = (data.people || []).filter(person => person.name.toLowerCase().includes(search.toLowerCase()));
  return <div className="ApartmentTravel">
    <header><div><strong>{data.atHome ? 'Терминал квартиры' : 'Жилой маршрут Lagoon'}</strong><small>{data.atHome ? 'Возвращение, обустройство и гости' : 'Вход в квартиру на жилой остановке'}</small></div>
      <button onClick={() => send('close')} aria-label="Закрыть">×</button></header>
    <main>
      {data.message && <p role="status" className="ApartmentTravel__notice">{data.message}</p>}
      {data.pending && <p className="ApartmentTravel__notice">Подготовка перехода… Не двигайтесь.</p>}
      {data.atHome ? <section><h3>Возвращение</h3><p>Вы вернётесь на жилую остановку. Сданное снаряжение выдаётся после прибытия.</p>
        <button disabled={data.pending} onClick={() => send('return')}>Вернуться на остановку</button>
        {data.canManage && <button disabled={data.pending} onClick={() => send('edit')}>Обустройство квартиры</button>}</section>
        : <section><h3>Моя квартира</h3><p>Перед прибытием всё снаряжение будет сдано на хранение. Вы получите домашнюю одежду. При возвращении вещи выдаются обратно.</p>
          {!data.canDepart && <p className="ApartmentTravel__warning">Подойдите к шлюзу входа на жилой остановке.</p>}
          <button disabled={!data.canDepart || data.pending} onClick={() => send('depart')}>Сдать снаряжение и отправиться домой</button></section>}
      {!data.atHome && <section><h3>Приглашения в гости</h3>
        {!(data.invitations || []).length && <p>Действующих приглашений нет.</p>}
        {(data.invitations || []).map(person => <div key={person.user} className="ApartmentTravel__row"><span>{person.name}</span>
          <button disabled={!data.canDepart || data.pending} onClick={() => send('visit', person.user)}>Принять и отправиться</button></div>)}</section>}
      {data.canManage && <section><h3>Мои гости</h3>
        <p>{data.atHome ? 'Приглашение действует 5 минут. Закрытие входа отменяет приглашения; находящиеся внутри гости могут самостоятельно вернуться.' : 'Пригласите игроков рядом с вами. Приглашение действует 5 минут; гость нажимает на шлюз и подтверждает свой переход самостоятельно.'}</p>
        <button disabled={data.pending} onClick={() => send(data.entryOpen ? 'closeEntry' : 'openEntry')}>{data.entryOpen ? 'Закрыть вход для гостей' : 'Открыть вход для гостей'}</button>
        <input value={search} onFocus={() => send('focus')} onChange={event => setSearch(event.target.value)} placeholder="Поиск игрока" aria-label="Поиск игрока" />
        {!people.length && <p>{data.atHome ? 'Гостей не найдено.' : 'Рядом нет игроков для приглашения. Подойдите ближе и обновите список.'}</p>}
        {people.map(person => <div key={person.user} className="ApartmentTravel__row"><span>{person.name}<small>{person.inside ? 'В квартире' : person.invited ? `Приглашён · ${person.seconds} сек.` : 'Можно пригласить'}</small></span>
          {person.invited ? <button disabled={data.pending} onClick={() => send('revoke', person.user)}>Отозвать</button>
            : <button disabled={data.pending || !data.entryOpen || person.inside} onClick={() => send('invite', person.user)}>Пригласить</button>}
          {person.inside && <button disabled={data.pending} onClick={() => setKick(person)}>Попросить покинуть квартиру</button>}</div>)}
      </section>}
    </main>
    <footer><button disabled={data.pending} onClick={() => send('refresh')}>Обновить список</button></footer>
    {kick && <div className="ApartmentTravel__veil"><div role="dialog" aria-label="Возвращение гостя"><p>Вернуть {kick.name} на остановку со сданным снаряжением?</p>
      <button onClick={() => setKick(null)}>Отмена</button><button onClick={() => { send('kick', kick.user); setKick(null); }}>Вернуть гостя</button></div></div>}
  </div>;
};

export const ApartmentTravel = ApartmentTravelPanel;
