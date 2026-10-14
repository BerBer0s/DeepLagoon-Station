using Robust.Shared.Serialization;

namespace Content.Shared._DeepLagoon.Tattoos;

/// <summary>Recipe is a bounded list of brush strokes/layers, never an asserted source/provenance flag.</summary>
[Serializable, NetSerializable]
public sealed class TattooArtworkRequest(Guid request, Tattoo basis, string recipe) : EntityEventArgs
{
    public Guid Request = request;
    public Tattoo Basis = basis;
    public string Recipe = recipe;
}

[Serializable, NetSerializable]
public sealed class TattooArtworkReply(Guid request, Tattoo? artwork, string error = "") : EntityEventArgs
{
    public Guid Request = request;
    public Tattoo? Artwork = artwork;
    public string Error = error;
}
