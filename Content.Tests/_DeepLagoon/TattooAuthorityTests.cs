using System;
using System.IO;
using System.Reflection;
using Content.Server._DeepLagoon.Tattoos;
using Content.Shared._DeepLagoon.Tattoos;
using NUnit.Framework;
using Robust.Shared.Network;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Tests._DeepLagoon;

[TestFixture]
public sealed class TattooAuthorityTests
{
    private static TattooArtworkSystem Authority(TattooArtworkProof proof)
    {
        var system = new TattooArtworkSystem();
        typeof(TattooArtworkSystem).GetField("_proof", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(system, proof);
        return system;
    }
    [Test]
    public void ServerRejectsForgedCrossAccountAndChangedBitmapsEvenWhenClientCallsThemNative()
    {
        var proof = new TattooArtworkProof(new byte[32]); var server = Authority(proof);
        var owner = new NetUserId(Guid.NewGuid()); var other = new NetUserId(Guid.NewGuid());
        var raw = new Tattoo { NormalBitmap = TattooTests.Bitmap(), ArtworkProof = Guid.NewGuid().ToString("N") + ":" + new string('0', 64) };
        Assert.That(server.Accept(owner, raw, []), Is.False);
        raw.ArtworkProof = proof.Sign(owner.UserId, raw);
        Assert.That(server.Accept(owner, raw, []), Is.True);
        Assert.That(server.Accept(other, raw, []), Is.False);
        var transformed = raw.Clone(); transformed.Id = Guid.NewGuid().ToString("N"); transformed.Scale = .2f;
        Assert.That(server.Accept(owner, transformed, []), Is.True, "Own signed exports and placement remain free after supporter expiry");
        transformed.NormalBitmap = TattooTests.Bitmap(true);
        Assert.That(server.Accept(owner, transformed, []), Is.False);
        Assert.That(new TattooArtworkProof(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)).Verify(owner.UserId, raw), Is.False);
    }
    [Test]
    public void GrandfatheringOnlyAcceptsTheStoredInkAndFreeVectorDrawing()
    {
        var server = Authority(new TattooArtworkProof(new byte[32])); var user = new NetUserId(Guid.NewGuid());
        var existing = new Tattoo { NormalBitmap = TattooTests.Bitmap() };
        var moved = existing.Clone(); moved.X = .7f;
        Assert.That(server.Accept(user, moved, [existing]), Is.True);
        moved.GlowBitmap = TattooTests.Bitmap(true);
        Assert.That(server.Accept(user, moved, [existing]), Is.False);
        Assert.That(server.Accept(user, TattooTests.Drawing(), []), Is.True);
    }
    [Test]
    public void ServerRendersBothInkChannelsAndEraserFromOperations()
    {
        var recipe = """
            [{"strokes":[
              {"color":"#FF0000FF","width":0.1,"points":[[0.25,0.5],[0.75,0.5]]},
              {"color":"#00FF00FF","width":0.1,"glow":true,"points":[[0.5,0.25],[0.5,0.75]]},
              {"color":"#000000FF","width":0.2,"erase":true,"points":[[0.5,0.5]]}
            ]}]
            """;
        var result = TattooArtworkRenderer.Render(new Tattoo(), recipe);
        using var normal = Image.Load<Rgba32>(Convert.FromBase64String(result.NormalBitmap));
        using var glow = Image.Load<Rgba32>(Convert.FromBase64String(result.GlowBitmap));
        Assert.Multiple(() => {
            Assert.That(normal[80,128].R, Is.EqualTo(255)); Assert.That(glow[80,128].A, Is.Zero);
            Assert.That(glow[128,80].G, Is.EqualTo(255)); Assert.That(normal[128,80].A, Is.Zero);
            Assert.That(normal[128,128].A, Is.Zero); Assert.That(glow[128,128].A, Is.Zero);
            Assert.That(result.Strokes, Is.Empty); Assert.That(result.IsValid(), Is.True);
        });
    }
    [TestCase("[{\"opacity\":2}]")]
    [TestCase("[{\"strokes\":[{\"color\":\"#FFFFFFFF\",\"width\":0.1,\"points\":[[20,0]]}]}]")]
    [TestCase("[{\"useBase\":true},{\"useBase\":true}]")]
    [TestCase("[{\"strokes\":null}]")]
    public void InvalidDrawingOperationsCannotReachTheRasterLoop(string recipe)
        => Assert.Catch(() => TattooArtworkRenderer.Render(new Tattoo(), recipe));
    [Test]
    public void WorkAndPayloadBudgetsBoundAdversarialRecipes()
    {
        Assert.Throws<InvalidDataException>(() => TattooArtworkRenderer.Render(new Tattoo(), new string(' ', 240001)));
        var stroke = "{\"color\":\"#FFFFFFFF\",\"width\":0.2,\"points\":[[0,0],[1,1]]}";
        var recipe = "[{\"strokes\":[" + string.Join(",", System.Linq.Enumerable.Repeat(stroke, 200)) + "]}]";
        Assert.Throws<InvalidDataException>(() => TattooArtworkRenderer.Render(new Tattoo(), recipe));
    }
}
