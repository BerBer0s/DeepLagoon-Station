using System.Text;
using Content.Shared.Chat;
using Robust.Shared.Utility;

namespace Content.Client._DeepLagoon.WebUI;

/// <summary>Maps SS14 channels and rich text to TGUI's existing message styles.</summary>
public static class WebChatMessageFormatter
{
    // These classes come from tgui-panel/styles/goon/chat-dark.scss and chat-light.scss.
    // Type controls tab filtering; the CSS class controls presentation. Never infer a channel
    // from message text: a player can type OOC, ADMIN or a radio prefix in ordinary speech.
    public static (string Type, string CssClass) DescribeChannel(ChatChannel channel) => channel switch
    {
        ChatChannel.Local => ("localchat", "say"),
        ChatChannel.Whisper => ("localchat", "whisper"),
        ChatChannel.Emotes => ("localchat", "emote"),
        ChatChannel.Radio => ("radio", "radio"),
        ChatChannel.OOC => ("ooc", "ooc"),
        ChatChannel.LOOC => ("looc", "looc"),
        ChatChannel.Dead => ("deadchat", "deadsay"),
        ChatChannel.Damage => ("combat", "danger"),
        ChatChannel.Server => ("system", "boldannounce"),
        ChatChannel.Visual => ("info", "notice"),
        ChatChannel.Notifications => ("info", "notice"),
        ChatChannel.Admin => ("adminpm", "adminhelp"),
        ChatChannel.AdminAlert => ("adminlog", "adminnotice"),
        ChatChannel.AdminChat => ("adminchat", "adminsay"),
        ChatChannel.CollectiveMind => ("localchat", "telepathy"),
        ChatChannel.Telepathic => ("localchat", "telepathy"),
        _ => ("unknown", "infoplain"),
    };

    public static string BuildPayload(ChatMessage message)
    {
        var (type, cssClass) = DescribeChannel(message.Channel);
        var markup = FormattedMessage.FromMarkupPermissive(message.WrappedMessage);
        var color = message.MessageColorOverride is { } custom
            ? " style=\"color:" + custom.ToHex() + "\"" : "";
        var html = "<span class=\"" + cssClass + "\"" + color + ">" + ToHtml(markup) + "</span>";
        return "{\"type\":" + GameWebView.Quote(type) + ",\"text\":" +
            GameWebView.Quote(markup.ToString()) + ",\"html\":" + GameWebView.Quote(html) + "}";
    }

    public static string ToHtml(FormattedMessage markup)
    {
        var html = new StringBuilder();
        var active = new List<MarkupNode>();
        foreach (var node in markup)
        {
            if (node.IsPlainText)
            {
                AppendText(html, node.Value.StringValue ?? "", active);
                continue;
            }
            var name = node.Name!.ToLowerInvariant();
            if (name is not ("bold" or "italic" or "bolditalic" or "color" or "font" or "underline" or "name"))
                continue;
            if (!node.Closing)
                active.Add(node);
            else
            {
                // Existing SS14 templates can close font/bold tags in a different order.
                // Render each text run separately instead of producing crossed HTML tags.
                for (var i = active.Count - 1; i >= 0; i--)
                {
                    if (!string.Equals(active[i].Name, node.Name, StringComparison.OrdinalIgnoreCase))
                        continue;
                    active.RemoveAt(i);
                    break;
                }
            }
        }
        return html.ToString();
    }

    private static void AppendText(StringBuilder html, string text, List<MarkupNode> active)
    {
        var bold = false;
        var italic = false;
        var underline = false;
        var name = false;
        string? color = null;
        long? size = null;
        foreach (var tag in active)
        {
            switch (tag.Name!.ToLowerInvariant())
            {
                case "bold": bold = true; break;
                case "italic": italic = true; break;
                case "bolditalic": bold = italic = true; break;
                case "underline": underline = true; break;
                case "name": name = true; break;
                case "color":
                    if (tag.Value.ColorValue is { } value) color = value.ToHex();
                    break;
                case "font":
                    var font = tag.Value.StringValue ?? "";
                    bold |= font.Contains("Bold", StringComparison.OrdinalIgnoreCase);
                    italic |= font.Contains("Italic", StringComparison.OrdinalIgnoreCase);
                    if (tag.Attributes.TryGetValue("size", out var parameter) && parameter.LongValue is { } fontSize)
                        size = Math.Clamp(fontSize, 8, 36);
                    break;
            }
        }
        var style = new StringBuilder();
        if (bold) style.Append("font-weight:bold;");
        if (italic) style.Append("font-style:italic;");
        if (underline) style.Append("text-decoration:underline;");
        if (color != null) style.Append("color:").Append(color).Append(';');
        // SS14 defaults to 12px. Relative sizing keeps TGUI's font-size setting effective.
        if (size != null) style.Append("font-size:").Append(size * 100 / 12).Append("%;");
        if (style.Length > 0 || name)
        {
            html.Append("<span");
            if (name) html.Append(" class=\"name\"");
            if (style.Length > 0) html.Append(" style=\"").Append(style).Append('"');
            html.Append('>');
        }
        html.Append(text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
            .Replace("\"", "&quot;").Replace("'", "&#39;").Replace("\r\n", "\n").Replace("\n", "<br>"));
        if (style.Length > 0 || name) html.Append("</span>");
    }
}
