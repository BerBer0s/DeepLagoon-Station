using System.IO;
using YamlDotNet.RepresentationModel;

namespace Content.Server.Database;

public static class ChatPanelSettingsStorage
{
    /// <summary>Older clients included PNG bytes in their preferences. Never persist those bytes.</summary>
    public static string WithoutImage(string settings)
    {
        using var reader = new StringReader(settings);
        var yaml = new YamlStream();
        yaml.Load(reader);
        if (yaml.Documents.Count != 1 || yaml.Documents[0].RootNode is not YamlMappingNode root ||
            !root.Children.TryGetValue(new YamlScalarNode("Appearance"), out var node) || node is not YamlMappingNode appearance ||
            !appearance.Children.Remove(new YamlScalarNode("Image")))
            return settings;
        using var writer = new StringWriter();
        yaml.Save(writer, false);
        return writer.ToString();
    }
}
