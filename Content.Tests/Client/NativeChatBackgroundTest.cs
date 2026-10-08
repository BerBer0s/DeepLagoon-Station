using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Content.Client._DeepLagoon.WebUI;
using Moq;
using NUnit.Framework;
using Robust.Client.Graphics;
using Robust.Shared.ContentPack;
using Robust.UnitTesting;

namespace Content.Tests.Client;

[TestFixture]
public sealed class NativeChatBackgroundTest : RobustUnitTest
{
    public override UnitTestProject Project => UnitTestProject.Client;

    [Test]
    public void ShaderParserPreservesDecimalLiteralsInGeneratedFunctionBodies()
    {
        var path = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory,
            "../../Resources/Textures/_DeepLagoon/Shaders/chat_background.swsl"));
        var parser = typeof(ShaderPrototype).Assembly.GetType("Robust.Client.Graphics.ShaderParser", true)!;
        var parse = parser.GetMethod("Parse", new[] { typeof(string), typeof(IResourceManager) })!;
        var parsed = parse.Invoke(null,
            new object[] { File.ReadAllText(path), new Mock<IResourceManager>().Object })!;
        var functions = (IEnumerable) parsed.GetType().GetProperty("Functions")!.GetValue(parsed)!;
        foreach (var function in functions)
        {
            var body = (string) function.GetType().GetProperty("Body")!.GetValue(function)!;
            // SWSL accepts .09 but emits separate tokens ". 09", which GLSL
            // rejects. Check the translated bodies rather than parsing alone.
            Assert.That(Regex.IsMatch(body, @"\.\s+\d"), Is.False, body);
        }
    }

    [Test]
    public void ConfigurationSupportsAllPresetsAndRejectsInvalidInput()
    {
        using var background = new NativeChatBackground();
        var mode = typeof(NativeChatBackground).GetField("_mode", BindingFlags.NonPublic | BindingFlags.Instance)!;
        for (var i = 0; i <= 11; i++)
        {
            background.Configure("{\"mode\":" + i + ",\"opacity\":0.5,\"reduced\":0," +
                "\"base\":\"#202020ff\",\"background\":\"#151515ff\"," +
                "\"left\":0,\"top\":0.1,\"width\":1,\"height\":0.9}");
            Assert.That(mode.GetValue(background), Is.EqualTo(i));
        }
        background.Configure("{\"mode\":999}");
        Assert.That(mode.GetValue(background), Is.EqualTo(11));
        background.Reset();
        Assert.That(mode.GetValue(background), Is.Zero);
    }
}
