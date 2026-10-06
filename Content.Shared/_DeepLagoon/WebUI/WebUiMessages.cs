using Robust.Shared.Serialization;

namespace Content.Shared._DeepLagoon.WebUI;

[Serializable, NetSerializable]
public enum WebUiKey : byte { Key }

/// <summary>Only an interface name and JSON data are supplied, never a URL or executable script.</summary>
[Serializable, NetSerializable]
public sealed class WebUiState(string interfaceName, string dataJson) : BoundUserInterfaceState
{
    public readonly string InterfaceName = interfaceName;
    public readonly string DataJson = dataJson;
}

[Serializable, NetSerializable]
public sealed class WebUiActionMessage(string action, string payloadJson) : BoundUserInterfaceMessage
{
    public const int MaxPayloadLength = 8192;
    public readonly string Action = action;
    public readonly string PayloadJson = payloadJson;
}

/// <summary>Demonstrates a server-authoritative TGUI counter.</summary>
[RegisterComponent]
public sealed partial class WebUiDemoComponent : Component
{
    public int Count;
}
