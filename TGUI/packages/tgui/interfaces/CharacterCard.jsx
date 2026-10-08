import {useBackend} from '../backend';
import {useState} from 'react';
import {Button} from '../components';
import {playerTheme} from '../components/PlayerTheme';
import {CharacterText} from '../components/CharacterText';
import './CharacterEditor.scss';
const statuses=['Запрещено','Спросить','Разрешено'];
export const CharacterCard=()=>{
 const {data,act}=useBackend();const theme=playerTheme(data.chatState);
 const [imageIndex,setImageIndex]=useState(0),images=data.headshots||[];
 return <div className={'CharacterCard Chat '+theme.className} style={theme.style}>
  <aside>{(images.length>0||data.headshot)&&<img className="CharacterCard__headshot" src={images[imageIndex%images.length]?.image||data.headshot} alt={'Headshot '+data.name}/>}
   {images.length>1&&<div className="CharacterCard__imageNavigation"><Button onClick={()=>setImageIndex((imageIndex+images.length-1)%images.length)}>◀</Button><span>{imageIndex%images.length+1} / {images.length}</span><Button onClick={()=>setImageIndex((imageIndex+1)%images.length)}>▶</Button></div>}
   <div className="CharacterCard__model"/>
   {['erp','noncon','vore'].map((key,i)=><div key={key} className={'CharacterCard__consent consent-'+data[key]}><strong>{['Эротические взаимодействия','Взаимодействия без согласия','Vore'][i]}</strong><span>{statuses[data[key]]}</span></div>)}
  </aside>
  <main><div className="CharacterCard__header"><h2>{data.name}</h2><Button onClick={()=>act('close')}>Закрыть</Button></div>
    <h3>Описание персонажа</h3><div className="CharacterText"><CharacterText text={data.flavor}/></div>
    <h3>OOC заметки</h3><div className="CharacterText"><CharacterText text={data.ooc}/></div>
  </main></div>;
};
