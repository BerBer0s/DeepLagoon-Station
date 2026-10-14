using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Physics.Collision.Shapes;

namespace Content.Client._Crescent.ShipShields;

/// <summary>
/// Cached triangle fan for the convex oval produced by ShipShieldsSystem.
/// Shares the collision boundary, including its closing edge, without overlapping fill triangles.
/// </summary>
internal sealed class ShipShieldMesh
{
    public readonly Vector2[] SourceVertices;
    public readonly DrawVertexUV2D[] Vertices;
    public readonly Box2 Bounds;

    public ShipShieldMesh(ChainShape chain)
    {
        SourceVertices = chain.Vertices;
        var min = SourceVertices[0];
        var max = min;
        foreach (var vertex in SourceVertices)
        {
            min = Vector2.Min(min, vertex);
            max = Vector2.Max(max, vertex);
        }

        Bounds = new Box2(min, max);
        var size = Vector2.Max(Bounds.Size, new Vector2(0.001f));
        // CreateLoop already repeats the first boundary vertex at the end.
        Vertices = new DrawVertexUV2D[SourceVertices.Length + 1];
        Vertices[0] = new DrawVertexUV2D(Bounds.Center, new Vector2(0.5f));
        for (var i = 0; i < SourceVertices.Length; i++)
            Vertices[i + 1] = new DrawVertexUV2D(SourceVertices[i], (SourceVertices[i] - min) / size);
    }
}
