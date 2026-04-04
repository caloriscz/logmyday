using System.Text.RegularExpressions;

namespace LogMyDay.Shared.Ai;

public static class MarkdownHelper
{
    public static string ToHtml(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
        {
            return string.Empty;
        }

        var html = markdown;

        // Markdown links [text](url) - convert to clickable HTML links
        html = Regex.Replace(html, @"\[([^\]]+)\]\(([^\)]+)\)", "<a href=\"$2\" class=\"text-primary-400 underline hover:text-primary-300\">$1</a>");

        // Bold
        html = Regex.Replace(html, @"\*\*(.+?)\*\*", "<strong>$1</strong>");

        // Italic
        html = Regex.Replace(html, @"\*(.+?)\*", "<em>$1</em>");

        // Inline code
        html = Regex.Replace(html, @"`(.+?)`", "<code class=\"rounded bg-gray-100 px-1 py-0.5 text-xs dark:bg-slate-700\">$1</code>");

        // Line breaks
        html = html.Replace("\n\n", "</p><p class=\"mt-2\">").Replace("\n", "<br />");

        // Unordered lists
        html = Regex.Replace(html, @"(?:^|\<br \/\>)\s*[-•]\s+(.+?)(?=\<br \/\>|$)", "<li class=\"ml-4 list-disc\">$1</li>");

        if (!html.StartsWith("<"))
        {
            html = $"<p>{html}</p>";
        }

        return html;
    }
}
