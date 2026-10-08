using System.Linq;
using Content.Client._DeepLagoon.WebUI;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.Client.Lobby.UI;

/// <summary>Local editor appearance. Never written into the chat or character preferences.</summary>
public sealed class CharacterEditorAppearance
{
    private static readonly ResPath Path = new("/character_editor_appearance.json");
    private readonly Dictionary<string, string> _text = new()
    {
        ["theme"] = "dark", ["fontFamily"] = "Default", ["chatBgAnimation"] = "none",
        ["chatBgColor"] = "", ["chatTextColor"] = "", ["chatAccentColor"] = "",
        ["textGlow"] = "none", ["textGlowColor"] = "",
    };
    private readonly Dictionary<string, float> _numbers = new()
    {
        ["fontSize"] = 13, ["lineHeight"] = 1.2f, ["fontWeight"] = 400,
        ["letterSpacing"] = 0, ["borderRadius"] = 6, ["chatBgAnimOpacity"] = 0.5f,
    };
    private readonly Dictionary<string, bool> _flags = new() { ["smoothScroll"] = false, ["hoverEffect"] = false };

    public void Load(IResourceManager resources)
    {
        if (!resources.UserData.TryReadAllText(Path, out var json) || !TguiActionData.TryParse(json, out var data)) return;
        foreach (var key in _text.Keys.ToArray()) Update(key, data!.String(key) ?? _text[key]);
        foreach (var key in _numbers.Keys.ToArray())
            if (data!.String(key) is { } value) Update(key, value);
        foreach (var key in _flags.Keys.ToArray())
            if (data!.String(key) is { } value) Update(key, value);
    }

    public void Save(IResourceManager resources)
    {
        using var writer = resources.UserData.OpenWriteText(Path);
        writer.Write(Data().ToString());
    }

    public TguiData Data()
    {
        var data = new TguiData();
        foreach (var (key, value) in _text) data.String(key, value);
        foreach (var (key, value) in _numbers) data.Number(key, value);
        foreach (var (key, value) in _flags) data.Bool(key, value);
        return data;
    }

    public bool Update(string key, string value)
    {
        if (_flags.ContainsKey(key) && bool.TryParse(value, out var flag)) { _flags[key] = flag; return true; }
        if (_numbers.ContainsKey(key) && float.TryParse(value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var number) && float.IsFinite(number))
        {
            var range = key switch
            {
                "fontSize" => (8f, 32f), "lineHeight" => (1f, 3f), "fontWeight" => (100f, 900f),
                "letterSpacing" => (-0.5f, 3f), "borderRadius" => (0f, 16f), _ => (0.05f, 1f),
            };
            _numbers[key] = Math.Clamp(number, range.Item1, range.Item2); return true;
        }
        if (!_text.ContainsKey(key) || value.Length > 100) return false;
        var valid = key switch
        {
            "theme" => value is "light" or "dark" or "default",
            "chatBgAnimation" => value is "none" or "cosmos" or "nebula" or "matrix" or "aurora" or "pulse" or "waves" or "fireflies" or "sakura" or "gradient" or "rain" or "embers",
            "textGlow" => value is "none" or "subtle" or "strong",
            "fontFamily" => value.Length > 0 && value.All(c => char.IsLetterOrDigit(c) || c is ' ' or '-'),
            _ => value.Length == 0 || value.Length == 7 && value[0] == '#' && value[1..].All(Uri.IsHexDigit),
        };
        if (valid) _text[key] = value;
        return valid;
    }
}
