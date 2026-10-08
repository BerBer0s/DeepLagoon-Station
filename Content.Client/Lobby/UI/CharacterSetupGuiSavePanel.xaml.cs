using System.Numerics;
using Content.Client._DeepLagoon.WebUI;

namespace Content.Client.Lobby.UI;

public sealed partial class CharacterSetupGuiSavePanel : CharacterAuxiliaryWindow
{
    public event Action? SaveRequested;
    public event Action? DiscardRequested;

    public CharacterSetupGuiSavePanel(TguiData? appearance = null)
    {
        Panel.OnClose += Close;
        Resizable = false;
        SetSize = new Vector2(540, 250);
        Panel.SetState("CharacterSavePrompt", new TguiData().Object("appearance", appearance ?? new TguiData()).ToString());
        Panel.OnAction += (action, _) =>
        {
            if (action == "save") SaveRequested?.Invoke();
            else if (action == "discard") DiscardRequested?.Invoke();
            else if (action == "cancel") Close();
        };
    }
}
