import { TintedSprite } from '../../components/PlayerTheme';
import { useIcon } from '../tguiIcons';

const WHITE = '#ffffff';

type RecipeIconProps = {
  /** A recipe id; the host sends the icon once it has been asked for. */
  id: string;
  className?: string;
};

export const RecipeIcon = ({ id, className = '' }: RecipeIconProps) => {
  const layers = useIcon(id);
  return (
    <div className={`RecipeIcon ${className}`}>
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
