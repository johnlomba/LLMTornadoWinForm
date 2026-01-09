using System;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace TornadoViews
{
    /// <summary>
    /// Renders simplified markdown to a RichTextBox.
    /// Supports: headings (#, ##, ###), bold (**text**), inline code (`code`),
    /// code blocks (```), and bullet lists (- item).
    /// </summary>
    public static class ChatMarkdownRenderer
    {
        private static readonly Regex BoldPattern = new(@"\*\*(.+?)\*\*", RegexOptions.Compiled);
        private static readonly Regex InlineCodePattern = new(@"`([^`]+)`", RegexOptions.Compiled);

        public static void RenderToRichTextBox(RichTextBox rtb, string markdown)
        {
            if (rtb == null) return;
            markdown ??= string.Empty;

            rtb.SuspendLayout();
            rtb.Clear();

            var lines = markdown.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            var defaultFont = rtb.Font;
            var boldFont = new Font(defaultFont, FontStyle.Bold);
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

                if (line.StartsWith("- ") || line.StartsWith("* "))
                {
                    rtb.AppendText("  • ");
                    RenderInlineFormatting(rtb, line.Substring(2), defaultFont, boldFont, codeFont);
                    continue;
                }

                RenderInlineFormatting(rtb, line, defaultFont, boldFont, codeFont);
            }

            rtb.ResumeLayout();
        }

        private static void RenderInlineFormatting(RichTextBox rtb, string text, Font defaultFont, Font boldFont, Font codeFont)
        {
            int pos = 0;

            while (pos < text.Length)
            {
                var boldMatch = BoldPattern.Match(text, pos);
                var codeMatch = InlineCodePattern.Match(text, pos);

                Match? nextMatch = null;
                bool isBold = false;
                bool isCode = false;

                if (boldMatch.Success && (!codeMatch.Success || boldMatch.Index <= codeMatch.Index))
                {
                    nextMatch = boldMatch;
                    isBold = true;
                }
                else if (codeMatch.Success)
                {
                    nextMatch = codeMatch;
                    isCode = true;
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

                if (isBold)
                {
                    AppendText(rtb, nextMatch.Groups[1].Value, boldFont, rtb.ForeColor, rtb.BackColor);
                }
                else if (isCode)
                {
                    AppendText(rtb, nextMatch.Groups[1].Value, codeFont, Color.FromArgb(40, 40, 40), Color.FromArgb(230, 230, 230));
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
