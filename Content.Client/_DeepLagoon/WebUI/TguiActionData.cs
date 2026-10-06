using System.Globalization;
using System.IO;
using YamlDotNet.RepresentationModel;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Utility;

namespace Content.Client._DeepLagoon.WebUI;

/// <summary>Sandbox-compatible scalar action arguments. Consumers validate their own fields.</summary>
public sealed class TguiActionData
{
    private readonly MappingDataNode _root;
    private TguiActionData(MappingDataNode root) => _root = root;
    public string? String(string key) => _root.TryGet<ValueDataNode>(key, out var node) ? node.Value : null;
    public bool TryInt(string key, out int value) => int.TryParse(String(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    public bool TryFloat(string key, out float value) => float.TryParse(String(key), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
        && float.IsFinite(value);

    public static bool TryParse(string payload, out TguiActionData? data)
    {
        data = null;
        if (payload.Length > 8192 || !payload.TrimStart().StartsWith('{')) return false;
        try
        {
            using var reader = new StringReader(payload);
            var stream = new YamlStream();
            stream.Load(reader);
            if (stream.Documents.Count != 1 || stream.Documents[0].RootNode.ToDataNode() is not MappingDataNode root) return false;
            data = new TguiActionData(root);
            return true;
        }
        catch (Exception) { return false; }
    }
}
