using System;
using Content.Shared._DeepLagoon.Apartments;
using NUnit.Framework;

namespace Content.Tests.Shared;

[TestFixture]
public sealed class ApartmentGuestAccessTests
{
    [Test]
    public void InvitationExpiresAtTheDeadline()
    {
        var access = new ApartmentGuestAccess();
        var guest = Guid.NewGuid();
        Assert.That(access.Invite(guest, TimeSpan.FromSeconds(10)), Is.True);
        Assert.That(access.MayEnter(guest, TimeSpan.FromSeconds(309)), Is.True);
        Assert.That(access.MayEnter(guest, TimeSpan.FromSeconds(310)), Is.False);
        access.Prune(TimeSpan.FromSeconds(310));
        Assert.That(access.Invitations, Is.Empty);
    }

    [Test]
    public void ReopeningDoesNotRestoreRevokedInvitations()
    {
        var access = new ApartmentGuestAccess();
        var guest = Guid.NewGuid();
        access.Invite(guest, TimeSpan.Zero);
        access.SetOpen(false);
        Assert.That(access.Invite(guest, TimeSpan.Zero), Is.False);
        access.SetOpen(true);
        Assert.That(access.MayEnter(guest, TimeSpan.Zero), Is.False);
        access.Invite(guest, TimeSpan.Zero);
        access.Revoke(guest);
        Assert.That(access.MayEnter(guest, TimeSpan.Zero), Is.False);
    }

    [Test]
    public void BoundAllowsRenewalAndReclaimsExpiredSlots()
    {
        var access = new ApartmentGuestAccess();
        var first = Guid.NewGuid();
        access.Invite(first, TimeSpan.Zero);
        for (var i = 1; i < ApartmentGuestAccess.MaxInvitations; i++) Assert.That(access.Invite(Guid.NewGuid(), TimeSpan.Zero), Is.True);
        Assert.That(access.Invite(Guid.NewGuid(), TimeSpan.FromSeconds(1)), Is.False);
        Assert.That(access.Invite(first, TimeSpan.FromSeconds(1)), Is.True);
        Assert.That(access.Invite(Guid.NewGuid(), TimeSpan.FromSeconds(300)), Is.True);
        Assert.That(access.Invitations, Has.Count.EqualTo(2));
        Assert.That(access.Invite(Guid.Empty, TimeSpan.Zero), Is.False);
    }
}
