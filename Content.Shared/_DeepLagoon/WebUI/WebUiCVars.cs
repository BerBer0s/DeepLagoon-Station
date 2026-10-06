using Robust.Shared.Configuration;

namespace Content.Shared._DeepLagoon.WebUI;

[CVarDefs]
public sealed class WebUiCVars
{
    // Opt-in for a development session; not archived and not sent by the server.
    public static readonly CVarDef<string> DevServer = CVarDef.Create("tgui.dev_server", "", CVar.CLIENTONLY);
    public static readonly CVarDef<float> ChatPanelWidth = CVarDef.Create("ui.chat_panel_width", 0.2f, CVar.CLIENTONLY | CVar.ARCHIVE);
}
