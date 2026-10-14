using System.Collections.Generic;
using System.Reflection;
using Content.Server._DeepLagoon.DiscordLink;
using Moq;
using NUnit.Framework;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Tests._DeepLagoon;

[TestFixture]
public sealed class AdmissionRevocationTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void RevocationDisconnectsAdmittedAndForcedSessionsImmediately(bool forced)
    {
        var channel = new Mock<INetChannel>(); channel.SetupGet(c => c.IsConnected).Returns(true);
        var session = new Mock<ICommonSession>(); session.SetupGet(s => s.Channel).Returns(channel.Object);
        var system = new DiscordLinkSystem();
        var admitted = (HashSet<ICommonSession>) typeof(DiscordLinkSystem).GetField("_admitted", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(system)!;
        if (!forced) admitted.Add(session.Object);
        system.RevokeAdmission(session.Object, "revoked", forced);
        Assert.That(admitted, Does.Not.Contain(session.Object));
        channel.Verify(c => c.Disconnect("revoked"), Times.Once);
    }
}
