import React from 'react';
// Render text as React nodes: user HTML, URLs and arbitrary markup never execute.
export const CharacterText = ({text='',depth=0}) => {
  if(depth>12)return text;
  const token=/-=(#?[0-9a-fA-F]{6})\(([^]*?)\)=-|\*\*([^]*?)\*\*|__([^]*?)__|~~([^]*?)~~|\|\|([^]*?)\|\||`([^`]+)`|\*([^*\n]+)\*|_([^_\n]+)_/g;
  const nodes=[];let last=0,match;
  while((match=token.exec(text))!==null){if(match.index>last)nodes.push(text.slice(last,match.index));const key=match.index;
    if(match[1])nodes.push(<span key={key} style={{color:'#'+match[1].replace('#','')}}><CharacterText text={match[2]} depth={depth+1}/></span>);
    else {const index=match.findIndex((value,i)=>i>=3&&value!==undefined);const value=match[index];const Tag={3:'strong',4:'u',5:'s',6:'span',7:'code',8:'em',9:'em'}[index];nodes.push(<Tag key={key} className={index===6?'CharacterText__spoiler':undefined} tabIndex={index===6?0:undefined}><CharacterText text={value} depth={depth+1}/></Tag>);}
    last=token.lastIndex;
  }
  if(last<text.length)nodes.push(text.slice(last));return <>{nodes}</>;
};
export const FormattingHint=()=> <small className="CharacterTextHint">Форматирование: **жирный**, *курсив*, __подчёркнутый__, ~~зачёркнутый~~, ||спойлер||, `код`. Цвет: -=#ff8800(цветной текст)=-. Переносы строк сохраняются.</small>;
