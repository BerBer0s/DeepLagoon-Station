using Robust.Shared.Serialization;
namespace Content.Shared._DeepLagoon.CharacterInfo;
public static class HeadshotLimits
{
    public const int MaxBytes = 1024 * 1024;
    public const int ChunkBytes = 32768;
    public const int MaxPixels = 4096 * 4096;
}
[Serializable, NetSerializable]
public sealed class HeadshotBeginEvent(Guid request, int slot, int length) : EntityEventArgs
{ public Guid Request = request; public int Slot = slot; public int Length = length; }
[Serializable, NetSerializable]
public sealed class HeadshotUploadEvent(Guid request, int offset, byte[] bytes) : EntityEventArgs
{ public Guid Request = request; public int Offset = offset; public byte[] Bytes = bytes; }
[Serializable, NetSerializable]
public sealed class HeadshotRequestEvent(Guid request, int slot, NetEntity? target = null) : EntityEventArgs
{ public Guid Request = request; public int Slot = slot; public NetEntity? Target = target; }
[Serializable, NetSerializable]
public sealed class HeadshotDataEvent(Guid request, int offset, int length, byte[] bytes, string mime, string id = "", string error = "") : EntityEventArgs
{ public Guid Request = request; public int Offset = offset; public int Length = length; public byte[] Bytes = bytes; public string Mime = mime; public string Id = id; public string Error = error; }

[Serializable, NetSerializable]
public sealed class CharacterInfoOpenEvent(NetEntity target) : EntityEventArgs { public NetEntity Target = target; }
