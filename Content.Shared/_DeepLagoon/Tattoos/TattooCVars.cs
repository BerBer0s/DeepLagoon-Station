using Robust.Shared.Configuration;

namespace Content.Shared._DeepLagoon.Tattoos;

[CVarDefs]
public static class TattooCVars
{
    public static readonly CVarDef<bool> Enabled =
        CVarDef.Create("tattoos.enabled", true, CVar.CLIENT | CVar.ARCHIVE | CVar.REPLICATED);
}
