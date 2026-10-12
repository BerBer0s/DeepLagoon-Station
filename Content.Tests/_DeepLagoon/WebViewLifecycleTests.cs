using System.Linq;
using System.Text.Json;
using Content.Client._DeepLagoon.WebUI;
using NUnit.Framework;

namespace Content.Tests._DeepLagoon;

[TestFixture]
public sealed class WebViewLifecycleTests
{
    [Test]
    public void RepeatedHeadshotUpdatesReplaceFieldsInsteadOfGrowingJson()
    {
        var data = new TguiData().String("name", "Test");
        for (var i = 0; i < 100; i++)
            data.Array("headshots", new[] { new TguiData().String("id", i.ToString()).String("image", "png") });
        using var json = JsonDocument.Parse(data.ToString());
        Assert.That(json.RootElement.EnumerateObject().Count(), Is.EqualTo(2));
        Assert.That(json.RootElement.GetProperty("headshots")[0].GetProperty("id").GetString(), Is.EqualTo("99"));
        Assert.That(data.ToString().Length, Is.LessThan(100));
    }
}
