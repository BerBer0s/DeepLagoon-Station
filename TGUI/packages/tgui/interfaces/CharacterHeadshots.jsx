import { Button, Section } from '../components';

export const CharacterHeadshots = ({ data, act }) => {
  const gallery = data.gallery || {}, images = gallery.images || [];
  const count = images.filter(image => image.active).length;
  return <Section title="Изображения персонажа">
    {data.headshot && <img className="CharacterEditor__headshot" src={data.headshot} alt="Headshot персонажа" />}
    <div className="CharacterEditor__headshotActions">
      <Button onClick={() => act('headshot-upload')}>{images.length ? 'Заменить изображение' : 'Загрузить изображение'}</Button>
      <Button onClick={() => act('character-card')}>Просмотр профиля</Button>
      <Button disabled={!images.length} onClick={() => act('headshot-download')}>Скачать изображение</Button>
      {(gallery.library || gallery.activeCapacity > 1) && <Button selected={data.libraryOpen} onClick={() => act('headshot-library')}>{gallery.library ? 'Моя библиотека' : 'Изображения профиля'}</Button>}
    </div>
    <small>{data.extendedHeadshot ? 'PNG/JPEG/GIF до 5 МБ.' : 'PNG/JPEG до 1 МБ.'} Изображений в профиле: {count} / {gallery.activeCapacity || 1}.{gallery.library && <> Библиотека этого персонажа: {images.length} / {gallery.capacity}.</>}</small>
    {gallery.error && <p>{gallery.error}</p>}{data.headshotStatus && <p>{data.headshotStatus}</p>}
  </Section>;
};
