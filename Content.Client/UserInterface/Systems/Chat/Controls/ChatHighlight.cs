using System.Linq;
using System.Text;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.Systems.Chat.Controls;

public static class ChatHighlight
{
    public static string PlainText(FormattedMessage message)
    {
        var text = new StringBuilder();
        foreach (var node in message.Nodes)
            if (node.Name == null) text.Append(node.Value.StringValue);
        return text.ToString();
    }

    public static FormattedMessage Apply(FormattedMessage source, string words, bool wholeWords, Color color, string search = "")
    {
        var plain = PlainText(source);
        var marked = new bool[plain.Length];
        var terms = words.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var term in terms) Mark(term, wholeWords);
        if (!string.IsNullOrWhiteSpace(search)) Mark(search.Trim(), false);
        void Mark(string term, bool whole)
        {
            for (var start = 0; start <= plain.Length - term.Length;)
            {
                var index = plain.IndexOf(term, start, StringComparison.OrdinalIgnoreCase);
                if (index < 0) break;
                var end = index + term.Length;
                bool IsWord(char c) => char.IsLetterOrDigit(c) || c == '_';
                if (!whole || (index == 0 || !IsWord(plain[index - 1])) && (end == plain.Length || !IsWord(plain[end])))
                    for (var i = index; i < end; i++) marked[i] = true;
                start = index + 1;
            }
        }
        var result = new FormattedMessage();
        var offset = 0;
        foreach (var node in source.Nodes)
        {
            if (node.Name != null || node.Value.StringValue is not { } text)
            {
                result.PushTag(node);
                continue;
            }
            for (var i = 0; i < text.Length;)
            {
                var highlighted = marked[offset + i];
                var end = i + 1;
                while (end < text.Length && marked[offset + end] == highlighted) end++;
                if (highlighted) result.PushColor(color);
                result.AddText(text[i..end]);
                if (highlighted) result.Pop();
                i = end;
            }
            offset += text.Length;
        }
        return result;
    }
}
