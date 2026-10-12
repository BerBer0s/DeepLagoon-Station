import { useEffect, useRef, useState } from 'react';
import { useIcon } from '../interfaces/tguiIcons';
import { TintedSprite } from './PlayerTheme';

/** Decode only icons that enter the viewport, including horizontal hair carousels. */
export const LazySprite = ({ id, color = '#ffffff' }: { id?: string; color?: string }) => {
  const root = useRef<HTMLSpanElement>(null);
  const [visible, setVisible] = useState(false);
  useEffect(() => {
    const observer = new IntersectionObserver(entries => {
      if (entries.some(entry => entry.isIntersecting)) {
        setVisible(true);
        observer.disconnect();
      }
    });
    if (root.current) observer.observe(root.current);
    return () => observer.disconnect();
  }, [id]);
  const layers = useIcon(visible ? id || '' : '');
  return <span ref={root} style={{ display: 'block', width: '100%', height: '100%' }}>
    {layers.map((layer, index) => <TintedSprite key={index} image={layer.url} color={color} baseColor={layer.color} />)}
  </span>;
};
