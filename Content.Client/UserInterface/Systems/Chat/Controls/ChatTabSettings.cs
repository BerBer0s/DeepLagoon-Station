using System.Globalization;
using System.IO;
using Content.Shared.Chat;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;
using YamlDotNet.RepresentationModel;

namespace Content.Client.UserInterface.Systems.Chat.Controls;

public sealed class ChatTabSettings
{
    public string Name { get; set; } = "";
    public ChatChannel Channels { get; set; }
}

public sealed class ChatTabsSettings
{
    public List<ChatTabSettings> Tabs { get; set; } = new();
    public int SelectedIndex { get; set; }
    public ChatAppearanceSettings Appearance { get; set; } = new();

    public string Serialize()
    {
        var tabs = new YamlSequenceNode();
        foreach (var tab in Tabs)
        {
            tabs.Add(new YamlMappingNode
            {
                { "Name", tab.Name },
                { "Channels", ((uint) tab.Channels).ToString(CultureInfo.InvariantCulture) }
            });
        }
        var root = new YamlMappingNode
        {
            { "SelectedIndex", SelectedIndex.ToString(CultureInfo.InvariantCulture) },
            { "Tabs", tabs },
            { "Appearance", Appearance.ToYaml() }
        };
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        new YamlStream(new YamlDocument(root)).Save(writer, false);
        return writer.ToString();
    }

    public static ChatTabsSettings? Deserialize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        try
        {
            // YAML also accepts the JSON mappings saved by the previous version.
            using var reader = new StringReader(text);
            var stream = new YamlStream();
            stream.Load(reader);
            if (stream.Documents.Count != 1 || stream.Documents[0].RootNode.ToDataNode() is not MappingDataNode root ||
                !root.TryGet<SequenceDataNode>("Tabs", out var tabs) ||
                tabs.Sequence.Count is < 1 or > 16)
                return null;
            var settings = new ChatTabsSettings();
            foreach (var node in tabs.Sequence)
            {
                if (node is not MappingDataNode tab ||
                    !tab.TryGet<ValueDataNode>("Name", out var name) ||
                    string.IsNullOrWhiteSpace(name.Value) || name.Value.Length > 32 ||
                    !tab.TryGet<ValueDataNode>("Channels", out var channels) ||
                    !uint.TryParse(channels.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var mask))
                    return null;
                settings.Tabs.Add(new ChatTabSettings { Name = name.Value, Channels = (ChatChannel) mask });
            }
            if (root.TryGet<ValueDataNode>("SelectedIndex", out var index) &&
                int.TryParse(index.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var selectedIndex))
                settings.SelectedIndex = selectedIndex;
            settings.SelectedIndex = Math.Clamp(settings.SelectedIndex, 0, settings.Tabs.Count - 1);
            if (root.TryGet<MappingDataNode>("Appearance", out var appearance))
                settings.Appearance = ChatAppearanceSettings.FromYaml(appearance);
            return settings;
        }
        catch (Exception)
        {
            // Malformed saved settings must not prevent the chat from opening.
            return null;
        }
    }
}
