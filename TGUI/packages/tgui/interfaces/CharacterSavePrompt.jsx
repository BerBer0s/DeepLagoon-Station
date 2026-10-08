import { useBackend } from '../backend';
import { Button } from '../components';
import { playerTheme } from '../components/PlayerTheme';
export const CharacterSavePrompt = () => {
  const {data,act}=useBackend();const theme=playerTheme(data.chatState);
  return <div className={'CharacterEditor CharacterEditor--save Chat '+theme.className} style={theme.style}><p>Сохранить изменения персонажа перед выходом?</p><Button color="good" onClick={()=>act('save')}>Сохранить</Button><Button onClick={()=>act('discard')}>Не сохранять</Button><Button onClick={()=>act('cancel')}>Отмена</Button></div>;
};
