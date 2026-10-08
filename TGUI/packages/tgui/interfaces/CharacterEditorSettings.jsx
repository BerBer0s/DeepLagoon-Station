import { Button, Dropdown, Section } from '../components';
import { ColorSquare } from '../components/PlayerColorPicker';
import { CHAT_BG_ANIMATIONS, FONTS, TEXT_GLOW_OPTIONS } from '../../tgui-panel/settings/constants';

export const editorAppearanceDefaults = {
  theme: 'dark', fontFamily: 'Default', fontSize: 13, lineHeight: 1.2,
  fontWeight: 400, letterSpacing: 0, borderRadius: 6, chatBgAnimation: 'none',
  chatBgAnimOpacity: 0.5, chatBgColor: '', chatTextColor: '', chatAccentColor: '',
  textGlow: 'none', textGlowColor: '', smoothScroll: false, hoverEffect: false,
};

export const editorAppearance = appearance => {
  const settings = { ...editorAppearanceDefaults, ...appearance };
  const light = settings.theme !== 'dark';
  const accent = settings.chatAccentColor || '#a7d2ff';
  const glow = settings.textGlowColor || accent;
  return {
    fontFamily: settings.fontFamily === 'Default' ? 'Arial, sans-serif' : `${settings.fontFamily}, sans-serif`,
    fontSize: settings.fontSize + 'px', lineHeight: settings.lineHeight,
    fontWeight: settings.fontWeight, letterSpacing: settings.letterSpacing + 'px',
    textShadow: settings.textGlow === 'none' ? 'none' : `0 0 ${settings.textGlow === 'strong' ? 8 : 3}px ${glow}`,
    '--editor-radius': settings.borderRadius + 'px', '--editor-accent': accent,
    '--editor-control-bg': light ? '#dedee3' : '#252b35',
    '--editor-control-text': settings.chatTextColor || (light ? '#171c24' : '#ddd'),
  };
};

export const CharacterEditorSettings = ({ settings: values, act }) => {
  const settings = { ...editorAppearanceDefaults, ...values };
  const change = (field, value) => act('appearance-setting', { field, value: String(value) });
  const field = (label, children) => <label className="CharacterEditor__field"><span>{label}</span>{children}</label>;
  const select = (label, key, options) => field(label, <Dropdown selected={settings[key]}
    options={options.map(option => typeof option === 'string' ? { value: option, displayText: option } : { value: option.id, displayText: option.name })}
    width="100%" onSelected={value => change(key, value)} />);
  const number = (label, key, min, max, step) => field(label, <input type="number" aria-label={label}
    min={min} max={max} step={step} value={settings[key]} onChange={event => {
      if (event.target.value !== '') change(key, Number(event.target.value));
    }} />);
  return <>
    <Section title="Оформление редактора"><p>Настройки сохраняются на этом компьютере и применяются только к редактору персонажа.</p>
      <div className="CharacterEditor__grid">
        {select('Тема', 'theme', [{ id: 'dark', name: 'Тёмная' }, { id: 'light', name: 'Светлая' }, { id: 'default', name: 'Стандартная' }])}
        {select('Шрифт', 'fontFamily', FONTS)}
        {number('Размер шрифта', 'fontSize', 8, 32, 1)}
        {number('Межстрочный интервал', 'lineHeight', 1, 3, 0.1)}
        {number('Насыщенность шрифта', 'fontWeight', 100, 900, 100)}
        {number('Межбуквенный интервал', 'letterSpacing', -0.5, 3, 0.1)}
        {number('Скругление углов', 'borderRadius', 0, 16, 1)}
      </div>
    </Section>
    <Section title="Фон и цвета"><div className="CharacterEditor__grid">
      {select('Анимация фона', 'chatBgAnimation', CHAT_BG_ANIMATIONS)}
      {settings.chatBgAnimation !== 'none' && number('Яркость фона', 'chatBgAnimOpacity', 0.05, 1, 0.05)}
      {select('Свечение текста', 'textGlow', TEXT_GLOW_OPTIONS)}
      {[['chatBgColor', 'Цвет фона', '#171c24'], ['chatTextColor', 'Цвет текста', '#dddddd'], ['chatAccentColor', 'Цвет акцента', '#a7d2ff'],
        ...(settings.textGlow !== 'none' ? [['textGlowColor', 'Цвет свечения', '#a7d2ff']] : [])].map(([key, label, fallback]) =>
        <div key={key} className="CharacterEditor__colorLine"><span>{label}</span><ColorSquare value={settings[key] || fallback} onChange={value => change(key, value.startsWith('#') ? value : '#' + value)} />
          <Button onClick={() => change(key, '')}>Сбросить</Button></div>)}
    </div></Section>
    <Section title="Поведение интерфейса">
      <Button.Checkbox checked={settings.smoothScroll} onClick={() => change('smoothScroll', !settings.smoothScroll)}>Плавная прокрутка</Button.Checkbox>
      <Button.Checkbox checked={settings.hoverEffect} onClick={() => change('hoverEffect', !settings.hoverEffect)}>Подсветка при наведении</Button.Checkbox>
    </Section>
    <Button icon="undo" onClick={() => act('appearance-reset')}>Сбросить настройки редактора</Button>
  </>;
};
