using System;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace TornadoViews
{
    /// <summary>
    /// Renders simplified markdown to a RichTextBox.
    /// Supports: headings (#, ##, ###), bold (**text**), italic (*text*),
    /// bold+italic (***text***), strikethrough (~~text~~), inline code (`code`),
    /// code blocks (```), bullet lists (- item), ordered lists (1. item),
    /// blockquotes (> text), and links ([text](url)).
    /// </summary>
    public static class ChatMarkdownRenderer
    {
        private static readonly Regex BoldItalicPattern = new(@"\*\*\*(.+?)\*\*\*", RegexOptions.Compiled);
        private static readonly Regex BoldPattern = new(@"\*\*(.+?)\*\*", RegexOptions.Compiled);
        private static readonly Regex ItalicPattern = new(@"(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)", RegexOptions.Compiled);
        private static readonly Regex ItalicUnderscorePattern = new(@"(?<!_)_(?!_)(.+?)(?<!_)_(?!_)", RegexOptions.Compiled);
        private static readonly Regex StrikethroughPattern = new(@"~~(.+?)~~", RegexOptions.Compiled);
        private static readonly Regex InlineCodePattern = new(@"`([^`]+)`", RegexOptions.Compiled);
        private static readonly Regex LinkPattern = new(@"\[([^\]]+)\]\(([^)]+)\)", RegexOptions.Compiled);
        private static readonly Regex OrderedListPattern = new(@"^\d+\.\s+(.*)$", RegexOptions.Compiled);

        public static void RenderToRichTextBox(RichTextBox rtb, string markdown)
        {
            if (rtb == null) return;
            markdown ??= string.Empty;

            rtb.SuspendLayout();
            rtb.Clear();

            var lines = markdown.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            var defaultFont = rtb.Font;
            var boldFont = new Font(defaultFont, FontStyle.Bold);
            var italicFont = new Font(defaultFont, FontStyle.Italic);
            var boldItalicFont = new Font(defaultFont, FontStyle.Bold | FontStyle.Italic);
            var strikeFont = new Font(defaultFont, FontStyle.Strikeout);
            var codeFont = new Font(FontFamily.GenericMonospace, defaultFont.Size);
            var h1Font = new Font(defaultFont.FontFamily, defaultFont.Size + 6, FontStyle.Bold);
            var h2Font = new Font(defaultFont.FontFamily, defaultFont.Size + 4, FontStyle.Bold);
            var h3Font = new Font(defaultFont.FontFamily, defaultFont.Size + 2, FontStyle.Bold);

            bool inCodeBlock = false;
            bool firstLine = true;

            foreach (var rawLine in lines)
            {
                string line = rawLine;

                if (line.TrimStart().StartsWith("```"))
                {
                    inCodeBlock = !inCodeBlock;
                    continue;
                }

                if (!firstLine)
                {
                    rtb.AppendText(Environment.NewLine);
                }
                firstLine = false;

                if (inCodeBlock)
                {
                    AppendText(rtb, line, codeFont, Color.FromArgb(40, 40, 40), Color.FromArgb(245, 245, 245));
                    continue;
                }

                // Headings
                if (line.StartsWith("### "))
                {
                    AppendText(rtb, line.Substring(4), h3Font, rtb.ForeColor, rtb.BackColor);
                    continue;
                }
                if (line.StartsWith("## "))
                {
                    AppendText(rtb, line.Substring(3), h2Font, rtb.ForeColor, rtb.BackColor);
                    continue;
                }
                if (line.StartsWith("# "))
                {
                    AppendText(rtb, line.Substring(2), h1Font, rtb.ForeColor, rtb.BackColor);
                    continue;
                }

                // Blockquotes
                if (line.StartsWith("> "))
                {
                    AppendText(rtb, "│ ", defaultFont, Color.Gray, rtb.BackColor);
                    RenderInlineFormatting(rtb, line.Substring(2), defaultFont, boldFont, italicFont, boldItalicFont, strikeFont, codeFont);
                    continue;
                }
                if (line.StartsWith(">"))
                {
                    AppendText(rtb, "│ ", defaultFont, Color.Gray, rtb.BackColor);
                    RenderInlineFormatting(rtb, line.Substring(1), defaultFont, boldFont, italicFont, boldItalicFont, strikeFont, codeFont);
                    continue;
                }

                // Unordered lists
                if (line.StartsWith("- ") || line.StartsWith("* "))
                {
                    rtb.AppendText("  • ");
                    RenderInlineFormatting(rtb, line.Substring(2), defaultFont, boldFont, italicFont, boldItalicFont, strikeFont, codeFont);
                    continue;
                }

                // Nested unordered lists
                if (line.StartsWith("  - ") || line.StartsWith("  * "))
                {
                    rtb.AppendText("    ◦ ");
                    RenderInlineFormatting(rtb, line.Substring(4), defaultFont, boldFont, italicFont, boldItalicFont, strikeFont, codeFont);
                    continue;
                }

                // Ordered lists
                var orderedMatch = OrderedListPattern.Match(line);
                if (orderedMatch.Success)
                {
                    var prefix = line.Substring(0, line.IndexOf('.') + 1);
                    rtb.AppendText($"  {prefix} ");
                    RenderInlineFormatting(rtb, orderedMatch.Groups[1].Value, defaultFont, boldFont, italicFont, boldItalicFont, strikeFont, codeFont);
                    continue;
                }

                // Table separator line (skip rendering)
                if (line.TrimStart().StartsWith("|") && line.Contains("-"))
                {
                    var stripped = line.Replace("|", "").Replace("-", "").Replace(" ", "");
                    if (string.IsNullOrEmpty(stripped))
                    {
                        continue; // Skip table separator lines like | --- | --- |
                    }
                }

                // Table rows
                if (line.TrimStart().StartsWith("|") && line.TrimEnd().EndsWith("|"))
                {
                    var cells = line.Trim().Trim('|').Split('|');
                    foreach (var cell in cells)
                    {
                        AppendText(rtb, "│ ", defaultFont, Color.Gray, rtb.BackColor);
                        RenderInlineFormatting(rtb, cell.Trim(), defaultFont, boldFont, italicFont, boldItalicFont, strikeFont, codeFont);
                        AppendText(rtb, " ", defaultFont, rtb.ForeColor, rtb.BackColor);
                    }
                    AppendText(rtb, "│", defaultFont, Color.Gray, rtb.BackColor);
                    continue;
                }

                RenderInlineFormatting(rtb, line, defaultFont, boldFont, italicFont, boldItalicFont, strikeFont, codeFont);
            }

            rtb.ResumeLayout();
        }

        private static void RenderInlineFormatting(RichTextBox rtb, string text, Font defaultFont, Font boldFont, Font italicFont, Font boldItalicFont, Font strikeFont, Font codeFont)
        {
            int pos = 0;

            while (pos < text.Length)
            {
                var boldItalicMatch = BoldItalicPattern.Match(text, pos);
                var boldMatch = BoldPattern.Match(text, pos);
                var italicMatch = ItalicPattern.Match(text, pos);
                var italicUnderscoreMatch = ItalicUnderscorePattern.Match(text, pos);
                var strikeMatch = StrikethroughPattern.Match(text, pos);
                var codeMatch = InlineCodePattern.Match(text, pos);
                var linkMatch = LinkPattern.Match(text, pos);

                // Find the earliest match
                Match? nextMatch = null;
                string matchType = "";
                int earliestIndex = int.MaxValue;

                if (boldItalicMatch.Success && boldItalicMatch.Index < earliestIndex)
                {
                    earliestIndex = boldItalicMatch.Index;
                    nextMatch = boldItalicMatch;
                    matchType = "bolditalic";
                }
                if (boldMatch.Success && boldMatch.Index < earliestIndex)
                {
                    earliestIndex = boldMatch.Index;
                    nextMatch = boldMatch;
                    matchType = "bold";
                }
                if (italicMatch.Success && italicMatch.Index < earliestIndex)
                {
                    earliestIndex = italicMatch.Index;
                    nextMatch = italicMatch;
                    matchType = "italic";
                }
                if (italicUnderscoreMatch.Success && italicUnderscoreMatch.Index < earliestIndex)
                {
                    earliestIndex = italicUnderscoreMatch.Index;
                    nextMatch = italicUnderscoreMatch;
                    matchType = "italic";
                }
                if (strikeMatch.Success && strikeMatch.Index < earliestIndex)
                {
                    earliestIndex = strikeMatch.Index;
                    nextMatch = strikeMatch;
                    matchType = "strike";
                }
                if (codeMatch.Success && codeMatch.Index < earliestIndex)
                {
                    earliestIndex = codeMatch.Index;
                    nextMatch = codeMatch;
                    matchType = "code";
                }
                if (linkMatch.Success && linkMatch.Index < earliestIndex)
                {
                    earliestIndex = linkMatch.Index;
                    nextMatch = linkMatch;
                    matchType = "link";
                }

                if (nextMatch == null)
                {
                    AppendText(rtb, text.Substring(pos), defaultFont, rtb.ForeColor, rtb.BackColor);
                    break;
                }

                if (nextMatch.Index > pos)
                {
                    AppendText(rtb, text.Substring(pos, nextMatch.Index - pos), defaultFont, rtb.ForeColor, rtb.BackColor);
                }

                switch (matchType)
                {
                    case "bolditalic":
                        AppendText(rtb, nextMatch.Groups[1].Value, boldItalicFont, rtb.ForeColor, rtb.BackColor);
                        break;
                    case "bold":
                        AppendText(rtb, nextMatch.Groups[1].Value, boldFont, rtb.ForeColor, rtb.BackColor);
                        break;
                    case "italic":
                        AppendText(rtb, nextMatch.Groups[1].Value, italicFont, rtb.ForeColor, rtb.BackColor);
                        break;
                    case "strike":
                        AppendText(rtb, nextMatch.Groups[1].Value, strikeFont, rtb.ForeColor, rtb.BackColor);
                        break;
                    case "code":
                        AppendText(rtb, nextMatch.Groups[1].Value, codeFont, Color.FromArgb(40, 40, 40), Color.FromArgb(230, 230, 230));
                        break;
                    case "link":
                        // Render link text in blue with underline
                        var linkText = nextMatch.Groups[1].Value;
                        var linkFont = new Font(defaultFont, FontStyle.Underline);
                        AppendText(rtb, linkText, linkFont, Color.Blue, rtb.BackColor);
                        break;
                }

                pos = nextMatch.Index + nextMatch.Length;
            }
        }

        private static void AppendText(RichTextBox rtb, string text, Font font, Color foreColor, Color backColor)
        {
            int start = rtb.TextLength;
            rtb.AppendText(text);
            int end = rtb.TextLength;

            rtb.Select(start, end - start);
            rtb.SelectionFont = font;
            rtb.SelectionColor = foreColor;
            rtb.SelectionBackColor = backColor;
            rtb.Select(end, 0);
        }
    }
}
