using Content.Shared._DeepLagoon.WebUI;
using Robust.Client.UserInterface;

namespace Content.Client._DeepLagoon.WebUI;

/// <summary>Reusable embedded TGUI: state replay, transport and lifecycle for every content window.</summary>
public sealed class TguiPanel : Control
{
    public GameWebView Web { get; }
    public event Action<string, string>? OnAction;
    public event Action? OnClose;
    private string _interface = "";
    private string _data = "{}";
    private string _title = "DeepLagoon";

    public TguiPanel(bool suspendWhenHidden = false)
    {
        Web = new GameWebView(suspendWhenHidden: suspendWhenHidden);
        HorizontalExpand = VerticalExpand = true;
        AddChild(Web);
        Web.Ready += Publish;
        Web.Message += (type, payload) =>
        {
            if (type == "close") { OnClose?.Invoke(); return; }
            if (type.StartsWith("act/", StringComparison.Ordinal) && type.Length is > 4 and <= 68 &&
                payload.Length <= WebUiActionMessage.MaxPayloadLength)
                OnAction?.Invoke(type[4..], payload);
        };
    }

    public void SetState(string interfaceName, string dataJson, string title = "DeepLagoon")
    {
        if (_interface == interfaceName && _data == dataJson && _title == title) return;
        _interface = interfaceName;
        _data = dataJson;
        _title = title;
        Publish();
    }

    private void Publish()
    {
        if (_interface.Length == 0 || !Web.IsReady) return;
        Web.Send("update", "{\"config\":{\"interface\":" + GameWebView.Quote(_interface) +
            ",\"title\":" + GameWebView.Quote(_title) +
            ",\"status\":2,\"window\":{\"key\":\"deeplagoon\",\"fancy\":false}},\"data\":" + _data + "}");
    }
}
