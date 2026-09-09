using System.Text;
using System.Text.RegularExpressions;

namespace SQLErrorAtlas.Services;

/// <summary>
/// Just enough Markdown -> HTML for the offline export documents (headings, bold,
/// inline code, fenced code, ordered / unordered lists, links, paragraphs).
/// Not a general Markdown implementation.
/// </summary>
public static partial class MiniMarkdown
{
    public static string ToHtml(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return "";

        var lines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var sb = new StringBuilder();
        var listStack = new Stack<string>();
        bool inCode = false;
        var para = new List<string>();

        void FlushPara()
        {
            if (para.Count == 0) return;
            sb.Append("<p>").Append(Inline(string.Join(" ", para))).Append("</p>\n");
            para.Clear();
        }

        void CloseLists()
        {
            while (listStack.Count > 0) sb.Append("</").Append(listStack.Pop()).Append(">\n");
        }

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();

            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                FlushPara();
                CloseLists();
                if (inCode) { sb.Append("</code></pre>\n"); inCode = false; }
                else { sb.Append("<pre><code>"); inCode = true; }
                continue;
            }
            if (inCode) { sb.Append(Escape(raw)).Append('\n'); continue; }

            if (line.Length == 0) { FlushPara(); CloseLists(); continue; }

            var heading = HeadingRe().Match(line);
            if (heading.Success)
            {
                FlushPara();
                CloseLists();
                var level = heading.Groups[1].Value.Length;
                sb.Append($"<h{level}>").Append(Inline(heading.Groups[2].Value)).Append($"</h{level}>\n");
                continue;
            }

            var ol = OrderedRe().Match(line);
            var ul = UnorderedRe().Match(line);
            if (ol.Success || ul.Success)
            {
                FlushPara();
                var want = ol.Success ? "ol" : "ul";
                if (listStack.Count == 0 || listStack.Peek() != want)
                {
                    CloseLists();
                    sb.Append('<').Append(want).Append(">\n");
                    listStack.Push(want);
                }
                var content = (ol.Success ? ol.Groups[2].Value : ul.Groups[2].Value);
                sb.Append("<li>").Append(Inline(content)).Append("</li>\n");
                continue;
            }

            CloseLists();
            para.Add(line.Trim());
        }

        FlushPara();
        CloseLists();
        if (inCode) sb.Append("</code></pre>\n");
        return sb.ToString();
    }

    private static string Inline(string text)
    {
        text = Escape(text);
        text = LinkRe().Replace(text, m => $"<a href=\"{m.Groups[2].Value}\">{m.Groups[1].Value}</a>");
        text = BoldRe().Replace(text, "<strong>$1</strong>");
        text = CodeRe().Replace(text, "<code>$1</code>");
        return text;
    }

    private static string Escape(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    [GeneratedRegex(@"^(#{1,6})\s+(.*)$")] private static partial Regex HeadingRe();
    [GeneratedRegex(@"^(\s*)\d+[.)]\s+(.*)$")] private static partial Regex OrderedRe();
    [GeneratedRegex(@"^(\s*)[-*+]\s+(.*)$")] private static partial Regex UnorderedRe();
    [GeneratedRegex(@"\*\*(.+?)\*\*")] private static partial Regex BoldRe();
    [GeneratedRegex("`([^`]+?)`")] private static partial Regex CodeRe();
    [GeneratedRegex(@"\[([^\]]+)\]\((https?://[^)]+)\)")] private static partial Regex LinkRe();
}
