using System;
using System.Collections.Generic;
using System.Text.Json;
using Content.Client._DeepLagoon.WebUI;
using NUnit.Framework;

namespace Content.Tests._DeepLagoon;

[TestFixture]
public sealed class TguiActionDataTests
{
    [Test]
    public void ScalarsRoundTripWithoutForkApis()
    {
        var values = new Dictionary<string, object>
        {
            ["name"] = "Семён🙂\n\t\"\\",
            ["enabled"] = true,
            ["count"] = -37,
            ["value"] = 1.25,
            ["empty"] = "",
            ["null"] = null,
            ["object"] = new { nested = new[] { 1, 2 } }
        };
        Assert.That(TguiActionData.TryParse(JsonSerializer.Serialize(values), out var data), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(data.String("name"), Is.EqualTo(values["name"]));
            Assert.That(data.String("enabled"), Is.EqualTo("true"));
            Assert.That(data.TryInt("count", out var count) && count == -37, Is.True);
            Assert.That(data.TryFloat("value", out var value) && value == 1.25f, Is.True);
            Assert.That(data.String("empty"), Is.Empty);
            Assert.That(data.String("null"), Is.Null);
            Assert.That(data.String("object"), Is.Null);
        });
    }

    [TestCase("{name: yaml}")]
    [TestCase("{\"x\":1,\"x\":2}")]
    [TestCase("{\"x\":1,\"\\u0078\":2}")]
    [TestCase("{\"x\":01}")]
    [TestCase("{\"x\":+1}")]
    [TestCase("{\"x\":.5}")]
    [TestCase("{\"x\":1.}")]
    [TestCase("{\"x\":1e+}")]
    [TestCase("{\"x\":NaN}")]
    [TestCase("{\"x\":trueX}")]
    [TestCase("{\"x\":1,}")]
    [TestCase("{\"x\":[1,]}")]
    [TestCase("{\"x\":\"\\q\"}")]
    [TestCase("{\"x\":\"\\uD800\"}")]
    [TestCase("{\"x\":\"\\uDC00\"}")]
    [TestCase("{\"x\":\"line\nbreak\"}")]
    [TestCase("{} trailing")]
    [TestCase("\u00a0{}")]
    [TestCase("[]")]
    [TestCase("null")]
    public void MalformedOrAmbiguousInputIsRejected(string input)
    {
        Assert.That(TguiActionData.TryParse(input, out var data), Is.False);
        Assert.That(data, Is.Null);
    }

    [Test]
    public void ResourceLimitsApplyBeforeParsing()
    {
        Assert.That(TguiActionData.TryParse("{}", out _, 1), Is.False);
        Assert.That(TguiActionData.TryParse("{}", out _, 0), Is.False);
        Assert.That(TguiActionData.TryParse("{}", out _, 1048577), Is.False);
        var large = JsonSerializer.Serialize(new { text = new string('a', 140000) });
        Assert.That(TguiActionData.TryParse(large, out _), Is.False);
        Assert.That(TguiActionData.TryParse(large, out _, 300000), Is.True);
        var allowed = "{\"x\":" + new string('[', 15) + "0" + new string(']', 15) + "}";
        var denied = "{\"x\":" + new string('[', 16) + "0" + new string(']', 16) + "}";
        Assert.That(TguiActionData.TryParse(allowed, out _), Is.True);
        Assert.That(TguiActionData.TryParse(denied, out _), Is.False);
        Assert.That(TguiActionData.TryParse("{\"x\":1e400}", out var data), Is.True);
        Assert.That(data.TryFloat("x", out _), Is.False);
    }

    [Test]
    public void EscapedUnicodeAndAllControlCharactersRoundTrip()
    {
        for (var c = 0; c <= char.MaxValue; c += 137)
        {
            if (char.IsSurrogate((char)c)) continue;
            var value = ((char)c).ToString();
            Assert.That(TguiActionData.TryParse(JsonSerializer.Serialize(new { value }), out var data), Is.True);
            Assert.That(data.String("value"), Is.EqualTo(value));
        }
        Assert.That(TguiActionData.TryParse("{\"x\":\"\\uD83D\\uDE42\",\"n\":-1.25E+2}", out var escaped), Is.True);
        Assert.That(escaped.String("x"), Is.EqualTo("🙂"));
        Assert.That(escaped.TryFloat("n", out var number) && number == -125f, Is.True);
    }
}
