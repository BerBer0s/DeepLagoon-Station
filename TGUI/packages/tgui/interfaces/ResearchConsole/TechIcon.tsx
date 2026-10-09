import { TintedSprite } from '../../components/PlayerTheme';
import { useTechIcon } from './icons';

const WHITE = '#ffffff';

type TechIconProps = {
  /** A technology id, or `recipeIconKey(id)`. */
  id: string;
  className?: string;
};

/** The layers of an icon, which the host sends once the icon has been asked for. */
export const TechIcon = ({ id, className = '' }: TechIconProps) => {
  const layers = useTechIcon(id);
  return (
    <div className={`TechIcon ${className}`}>
      {layers.map((layer, index) =>
        layer.color.toLowerCase() === WHITE ? (
          <img key={index} src={layer.url} alt="" draggable={false} />
        ) : (
          <TintedSprite key={index} image={layer.url} color={layer.color} />
        ),
      )}
    </div>
  );
};
