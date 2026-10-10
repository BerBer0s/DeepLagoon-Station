import { useIcon } from '../tguiIcons';

// Tells the host that an icon key is a lathe recipe and not a technology; the same prefix is on the host side.
const RECIPE_KEY_PREFIX = 'recipe:';

export const recipeIconKey = (recipeId: string) => RECIPE_KEY_PREFIX + recipeId;

/** Returns the icon layers of a technology or recipe and asks the host for them the first time they are shown. */
export const useTechIcon = useIcon;
