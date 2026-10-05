using System;
using System.Collections.Generic;
using System.Reflection;
using Content.Client.Chat.TypingIndicator;
using Content.Shared.CCVar;
using Content.Shared.Chat.TypingIndicator;
using Moq;
using NUnit.Framework;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.Tests.Client;

[TestFixture]
public sealed class TypingIndicatorTest
{
    [Test]
    public void InputAndTimeoutDoNotRaisePredictiveEvents()
    {
        var timing = new Mock<IGameTiming>();
        var now = TimeSpan.Zero;
        var firstPrediction = true;
        timing.SetupGet(t => t.CurTime).Returns(() => now);
        timing.SetupGet(t => t.IsFirstTimePredicted).Returns(() => firstPrediction);
        timing.SetupGet(t => t.InPrediction).Returns(false);
        var player = new Mock<IPlayerManager>();
        player.SetupGet(p => p.LocalEntity).Returns(new EntityUid(1));
        var config = new Mock<IConfigurationManager>();
        config.Setup(c => c.GetCVar(CCVars.ChatShowTypingIndicator)).Returns(true);
        var messages = new List<bool>();
        var network = new Mock<IEntityNetworkManager>();
        network.Setup(n => n.SendSystemNetworkMessage(It.IsAny<EntityEventArgs>(), true))
            .Callback<EntityEventArgs, bool>((msg, _) => messages.Add(((TypingChangedEvent) msg).State == TypingIndicatorState.Typing));
        var entities = new Mock<EntityManager>();
        entities.SetupGet(e => e.EntityNetManager).Returns(network.Object);
        var system = new TypingIndicatorSystem();
        SetField(system, typeof(TypingIndicatorSystem), "_time", timing.Object);
        SetField(system, typeof(TypingIndicatorSystem), "_playerManager", player.Object);
        SetField(system, typeof(TypingIndicatorSystem), "_cfg", config.Object);
        SetField(system, typeof(EntitySystem), "EntityManager", entities.Object);

        system.ClientChangedChatFocus(true);
        messages.Clear();
        system.ClientChangedChatText();
        Assert.That(messages, Is.EqualTo(new[] { true }));
        now = TimeSpan.FromSeconds(3);
        firstPrediction = false;
        system.Update(0);
        Assert.That(messages, Is.EqualTo(new[] { true }), "Replayed ticks must not expire UI typing state.");
        firstPrediction = true;
        system.Update(0);
        Assert.That(messages, Is.EqualTo(new[] { true, false }));
        system.ClientChangedChatText();
        system.ClientSubmittedChatText();
        Assert.That(messages, Is.EqualTo(new[] { true, false, true, false }));
        entities.Verify(e => e.RaisePredictiveEvent(It.IsAny<TypingChangedEvent>()), Times.Never);
    }

    private static void SetField(object target, Type declaringType, string name, object value)
    {
        declaringType.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
    }
}
