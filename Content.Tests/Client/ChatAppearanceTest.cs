using System.Linq;
using Content.Client.UserInterface.Systems.Chat.Controls;
using NUnit.Framework;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Tests.Client;

[TestFixture]
public sealed class ChatAppearanceTest
{
    [Test]
    public void AppearanceAndRenamedTabRoundTripTogether()
    {
        var settings = new ChatTabsSettings
        {
            Tabs = new() { new() { Name = "Мой чат: важное" } },
            Appearance = new()
            {
                PanelColor = "#112233", ChatColor = "#223344", InputColor = "#8899AA",
                GradientColor = "#445566", Background = "Image", Image = "aGVsbG8=",
                Font = "NotoSansDisplay", FontSize = 25, WholeWords = true,
                HighlightWords = "капитан, тревога", HighlightColor = "#FFCC00"
            }
        };
        var restored = ChatTabsSettings.Deserialize(settings.Serialize())!;
        Assert.Multiple(() =>
        {
            Assert.That(restored.Tabs[0].Name, Is.EqualTo(settings.Tabs[0].Name));
            Assert.That(restored.Appearance.ToYaml().ToString(), Is.EqualTo(settings.Appearance.ToYaml().ToString()));
            Assert.That(restored.Appearance.Image, Is.Empty, "PNG bytes must not be sent to or stored on the server.");
            Assert.That(settings.Serialize(), Does.Not.Contain(settings.Appearance.Image));
        });
    }

    [TestCase(false, "капитан", "капитанский", true)]
    [TestCase(true, "капитан", "капитанский", false)]
    [TestCase(true, "капитан", "Капитан!", true)]
    [TestCase(true, "капитан", "капитан_1", false)]
    public void HighlightRespectsCyrillicWordBoundaries(bool whole, string term, string text, bool marked)
    {
        var result = ChatHighlight.Apply(FormattedMessage.FromUnformatted(text), term, whole, Color.Yellow);
        Assert.That(ChatHighlight.PlainText(result), Is.EqualTo(text));
        Assert.That(result.Nodes.Any(node => node.Name == "color"), Is.EqualTo(marked));
    }

    [Test]
    public void SearchMatchesAcrossMarkupAndDoesNotInterpretTypedTags()
    {
        var source = FormattedMessage.FromMarkupOrThrow("[bold]Капи[/bold]тан [color=red]здесь[/color]");
        var result = ChatHighlight.Apply(source, "", true, Color.Yellow, "капитан");
        Assert.That(ChatHighlight.PlainText(result), Is.EqualTo("Капитан здесь"));
        Assert.That(result.Nodes.Count(n => n.Name == "bold"), Is.EqualTo(source.Nodes.Count(n => n.Name == "bold")));
        Assert.That(result.Nodes.Count(n => n.Name == "color"), Is.GreaterThan(source.Nodes.Count(n => n.Name == "color")));
        var literal = ChatHighlight.Apply(source, "[color=red]", false, Color.Yellow);
        Assert.That(literal.ToMarkup(), Is.EqualTo(source.ToMarkup()));
    }
}
