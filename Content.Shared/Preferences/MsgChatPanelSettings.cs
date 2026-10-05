using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.Preferences;

public sealed class MsgChatPanelSettings : NetMessage
{
    public const int MaxLength = 400000;
    public override MsgGroups MsgGroup => MsgGroups.Command;
    public string Data = "";
    public int Revision;
    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        Revision = buffer.ReadVariableInt32();
        Data = buffer.ReadString();
    }
    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.WriteVariableInt32(Revision);
        buffer.Write(Data);
    }
}

public sealed class MsgChatPanelSettingsSaved : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;
    public int Revision;
    public bool Success;
    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        Revision = buffer.ReadVariableInt32();
        Success = buffer.ReadBoolean();
    }
    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.WriteVariableInt32(Revision);
        buffer.Write(Success);
    }
}
