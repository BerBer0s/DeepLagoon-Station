using System;
using System.IO;
using System.Reflection;
using Content.Client.UserInterface.Systems.Chat.Controls;
using Moq;
using NUnit.Framework;
using Robust.Shared.ContentPack;
using Robust.Shared.Log;

namespace Content.Tests.Client;

[TestFixture]
public sealed class ClientSandboxTest
{
    [Test]
    public void ClientAssemblyPassesEngineTypeChecks()
    {
        var log = new LogManager();
        var handler = new Mock<ILogHandler>();
        log.RootSawmill.AddHandler(handler.Object);
        var resources = new Mock<IResourceManager>();
        resources.Setup(r => r.ContentFileRead(It.IsAny<Robust.Shared.Utility.ResPath>()))
            .Throws<FileNotFoundException>();
        var type = typeof(IModLoader).Assembly.GetType("Robust.Shared.ContentPack.AssemblyTypeChecker", true)!;
        var checker = Activator.CreateInstance(type, resources.Object, log.GetSawmill("res.typecheck"))!;
        type.GetField("EngineModuleDirectories")!.SetValue(checker, new[] { AppContext.BaseDirectory });
        using var assembly = File.OpenRead(typeof(ChatTabsSettings).Assembly.Location);
        var result = (bool) type.GetMethod("CheckAssembly", new[] { typeof(Stream) })!.Invoke(checker, new object[] { assembly })!;
        foreach (var invocation in handler.Invocations)
            TestContext.Out.WriteLine($"{invocation.Arguments[0]}: {invocation.Arguments[1].GetType().GetMethod("RenderMessage", new[] { typeof(IFormatProvider) })!.Invoke(invocation.Arguments[1], new object?[] { null })}");
        Assert.That(result, Is.True, "Content.Client must pass the engine sandbox as well as the compiler.");
    }
}
