using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static bool DiscordAdmissionRequired(IConfigurationManager configuration)
    {
#if DEVELOPMENT
        return false;
#else
        return configuration.GetCVar(DiscordLinkEnabled);
#endif
    }

    public static bool DiscordAdmissionDevelopment =>
#if DEVELOPMENT
        true;
#else
        false;
#endif

    public static readonly CVarDef<bool> DiscordLinkEnabled =
        CVarDef.Create("discord_link.enabled", false, CVar.SERVERONLY);

    public static readonly CVarDef<string> DiscordLinkToken =
        CVarDef.Create("discord_link.api_token", "", CVar.SERVERONLY | CVar.CONFIDENTIAL);

    public static readonly CVarDef<string> DiscordAccountMergePython =
        CVarDef.Create("discord_link.merge_python", "/home/ss14/ss14/control-agent-venv/bin/python", CVar.SERVERONLY);

    public static readonly CVarDef<string> DiscordAccountMergeWorker =
        CVarDef.Create("discord_link.merge_worker", "/opt/lagoon-launcher/identity/merge_game_accounts.py", CVar.SERVERONLY);
}
