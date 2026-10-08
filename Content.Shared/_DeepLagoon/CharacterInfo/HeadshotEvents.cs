using Robust.Shared.Serialization;
namespace Content.Shared._DeepLagoon.CharacterInfo;
public static class HeadshotLimits
{
    public static int ActiveSlots(int tier, bool booster) => tier > 0 ? Math.Clamp(tier + 1, 2, 4) : booster ? 2 : 1;
    public static int LibrarySlots(int tier, bool booster) => tier >= 3 ? 10 : tier >= 2 ? 5 : ActiveSlots(tier, booster);
    // The engine sandbox explicitly permits this hashing API on the client.
    public static string ContentHash(byte[] bytes) => Convert.ToHexString(SpaceWizards.Sodium.CryptoGenericHashBlake2B.Hash(32, bytes, default));
    public const int MaxBytes = 1024 * 1024;
    public const int SponsorMaxBytes = 5 * 1024 * 1024;
    public const int MaxAnimationPixels = 32 * 1024 * 1024;
    public const int MaxFrames = 300;
    public const int ChunkBytes = 32768;
    public const int MaxPixels = 4096 * 4096;
}
[Serializable, NetSerializable]
public sealed class HeadshotBeginEvent(Guid request, int slot, int length, bool append = false, string imageId = "") : EntityEventArgs
{ public Guid Request = request; public int Slot = slot; public int Length = length; public bool Append = append; public string ImageId = imageId; }
[Serializable, NetSerializable]
public sealed class HeadshotUploadEvent(Guid request, int offset, byte[] bytes) : EntityEventArgs
{ public Guid Request = request; public int Offset = offset; public byte[] Bytes = bytes; }
[Serializable, NetSerializable]
public sealed class HeadshotRequestEvent(Guid request, int slot, NetEntity? target = null, string cachedHash = "", string imageId = "") : EntityEventArgs
{ public Guid Request = request; public int Slot = slot; public NetEntity? Target = target; public string CachedHash = cachedHash; public string ImageId = imageId; }

[Serializable, NetSerializable]
public sealed class HeadshotGalleryRequestEvent(Guid request, int slot, NetEntity? target = null, string action = "", string imageId = "") : EntityEventArgs
{ public Guid Request = request; public int Slot = slot; public NetEntity? Target = target; public string Action = action; public string ImageId = imageId; }
[Serializable, NetSerializable]
public sealed class HeadshotGalleryDataEvent(Guid request, string[] images, string[] active, int capacity, int activeCapacity, string error = "") : EntityEventArgs
{ public Guid Request = request; public string[] Images = images; public string[] Active = active; public int Capacity = capacity; public int ActiveCapacity = activeCapacity; public string Error = error; }
[Serializable, NetSerializable]
public sealed class HeadshotDataEvent(Guid request, int offset, int length, byte[] bytes, string mime, string id = "", string error = "", bool notModified = false) : EntityEventArgs
{ public Guid Request = request; public int Offset = offset; public int Length = length; public byte[] Bytes = bytes; public string Mime = mime; public string Id = id; public string Error = error; public bool NotModified = notModified; }

[Serializable, NetSerializable]
public sealed class CharacterInfoOpenEvent(NetEntity target) : EntityEventArgs { public NetEntity Target = target; }
