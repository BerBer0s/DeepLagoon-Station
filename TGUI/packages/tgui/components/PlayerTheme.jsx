import '../../tgui-panel/styles/components/ChatBgAnimations.scss';
export const playerTheme = state => {
  let settings={};
  try {settings=JSON.parse(state||'{}').settings||{};} catch (_) {}
  const light=settings.theme==='default'||settings.theme==='light';
  const animation=/^[a-z-]+$/.test(settings.chatBgAnimation||'')?settings.chatBgAnimation:'none';
  return {className:(light?'theme-light':'theme-dark')+' Chat--bgAnim-'+animation,
    style:{backgroundColor:settings.chatBgColor||(light?'#eeeeee':'#171c24'),color:settings.chatTextColor||(light?'#171c24':'#ddd'),
      '--player-surface':light?'#dedee3':'#252b35','--chat-bg-anim-opacity':settings.chatBgAnimOpacity??0.5}};
};
export const TintedSprite = ({image,color='#ffffff',baseColor='#ffffff'}) => {
  color='#'+[1,3,5].map(i=>Math.round((parseInt(color.slice(i,i+2),16)||0)*(parseInt(baseColor.slice(i,i+2),16)||0)/255).toString(16).padStart(2,'0')).join('');
  const id='tint'+color.replace(/[^a-z0-9]/gi,'');
  const rgb=[1,3,5].map(i=>(parseInt(color.slice(i,i+2),16)||0)/255);
  return <><svg width="0" height="0" aria-hidden="true"><filter id={id} colorInterpolationFilters="sRGB"><feColorMatrix type="matrix" values={`${rgb[0]} 0 0 0 0 0 ${rgb[1]} 0 0 0 0 0 ${rgb[2]} 0 0 0 0 0 1 0`} /></filter></svg><img src={image} alt="" style={{filter:`url(#${id})`}} /></>;
};
