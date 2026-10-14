import React, { createContext, useContext, useEffect, useRef, useState } from 'react';
import { createRoot } from 'react-dom/client';
import { ShuttleConsole } from '../packages/tgui/interfaces/ShuttleConsole.jsx';
import { paintPreviewRadar } from './generated-shuttle-radar.mjs';

export const PreviewContext = createContext(null);
export const usePreviewBackend = () => useContext(PreviewContext);

const skins = [
  { id: 'quasimorph', name: 'Quasimorph', label: '#9dc48b' },
  { id: 'military', name: 'Военный терминал', label: '#bac6a0' },
  { id: 'graphite', name: 'Чёрный графит', label: '#bacbbc' },
  { id: 'field', name: 'Полевой пульт', label: '#c0cbae' },
];
const initial = {
  mode: 0,
  nav: { name: 'ПЕЛАГЕЯ / DL-014', designation: 'DL-014', position: '124.8 / −40.2',
    azimuth: '124.0°', velocity: '18.5 / 0.0', angular: '0.0°/с', search: '', range: 3000,
    speed: '60', iff: true, details: true, docks: true, dampener: 1,
    ports: Array.from({ length: 8 }, (_, i) => ({ id: i + 1, name: `Порт ${i + 1}` })) },
  weapons: { status: 'Сервер орудий подключён', hasWeapons: true, entries: [
    { id: 'cannon', name: 'Корабельная пушка · 40 снарядов', selected: true },
  ] },
  map: { status: 'FTL готов', progress: .4, destinations: [{ id: 'station', name: 'Станция DeepLagoon' }], beacons: true },
  dock: { ports: [{ id: 'dock', name: 'Шлюз №1', connected: true, selected: true }], locked: false },
};

function Variant({ skin }) {
  const stage = useRef(null);
  const [data, setData] = useState(initial);
  const [closed, setClosed] = useState(false);
  const [notice, setNotice] = useState('');
  useEffect(() => {
    const app = stage.current.querySelector('.ShuttleConsole');
    if (!app) return;
    app.dataset.skin = skin.id;
    const viewport = app.querySelector('.ShuttleConsole__viewport');
    const design = { title: skin.label, glass: .4 };
    let frame = 0;
    const draw = () => {
      cancelAnimationFrame(frame);
      frame = requestAnimationFrame(() => {
        if (viewport.clientWidth < 10) return;
        app.style.setProperty('--preview-title', design.title);
        const result = paintPreviewRadar({ element: viewport, source: window.shuttleGlassSource,
          colorConversions: window.shuttleColorConversions, strength: design.glass });
        app.dataset.maskVerified = result.maskMatches ? 'true' : 'false';
      });
    };
    const observer = new ResizeObserver(draw);
    observer.observe(viewport);
    draw();
    let tweak;
    if (globalThis.Tweak) {
      tweak = new Tweak({ container: stage.current, onChange: draw });
      tweak.addColorPicker(design, 'title', { label: 'Цвет заголовков', reference: '--preview-title' });
      tweak.addSlider(design, 'glass', { label: 'Стекло', min: .1, max: .5, step: .05 });
    }
    return () => { observer.disconnect(); cancelAnimationFrame(frame); tweak?.dispose(); };
  }, [skin.id, closed]);
  const act = (action, payload) => {
    if (action === 'viewport') return;
    if (action === 'mode') { setData(d => ({ ...d, mode: payload.value })); setNotice(''); return; }
    if (action === 'close') { setClosed(true); return; }
    if (action === 'weapon') {
      setData(d => ({ ...d, weapons: { ...d.weapons, entries: d.weapons.entries.map(weapon => ({ ...weapon,
        selected: payload.id === 'all' ? true : payload.id === 'none' ? false :
          payload.id === weapon.id ? !weapon.selected : weapon.selected })) } }));
      return;
    }
    if (action === 'nav') {
      if (['iff', 'details', 'docks'].includes(payload.id))
        setData(d => ({ ...d, nav: { ...d.nav, [payload.id]: !d.nav[payload.id] } }));
      else if (payload.id === 'dampener') setData(d => ({ ...d, nav: { ...d.nav, dampener: Number(payload.value) } }));
      else if (['search', 'range', 'speed'].includes(payload.id)) setData(d => ({ ...d, nav: { ...d.nav, [payload.id]: payload.value } }));
      else if (payload.id === 'port') setNotice(`Порт ${payload.value}`);
      return;
    }
    setNotice(action === 'map' ? 'Цель выбрана' : 'Команда стыковки');
  };
  return <div ref={stage} className="ShuttleThemeStage" aria-label={skin.name}>
    {closed ? <div className="ShuttleThemeClosed"><button onClick={() => setClosed(false)}>Открыть консоль</button></div> :
      <PreviewContext.Provider value={{ data, act }}><ShuttleConsole /></PreviewContext.Provider>}
    <span className="ShuttleThemeNotice" aria-live="polite">{notice}</span>
  </div>;
}

for (const skin of skins) {
  const root = document.getElementById(`shuttle-variant-${skin.id}`);
  createRoot(root).render(<Variant skin={skin} />);
}
