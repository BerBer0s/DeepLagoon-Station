using Robust.Shared.Configuration;
namespace Content.Shared.CCVar;
public sealed partial class CCVars
{
    public static readonly CVarDef<bool> PersonalLoadoutsEnabled = CVarDef.Create("game.personal_loadouts_enabled", true, CVar.SERVER | CVar.REPLICATED);
    public static readonly CVarDef<int> PersonalLoadoutPoints = CVarDef.Create("game.personal_loadout_points", 14, CVar.SERVER | CVar.REPLICATED);
}
