using System;
using System.Collections.Generic;
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
        // Pre-compiled regex patterns for inline formatting
        private static readonly Regex BoldItalicRegex = new Regex(@"\*\*\*(.+?)\*\*\*", RegexOptions.Compiled);
        private static readonly Regex BoldRegex = new Regex(@"\*\*(.+?)\*\*", RegexOptions.Compiled);
        private static readonly Regex ItalicRegex = new Regex(@"\*([^*]+)\*", RegexOptions.Compiled);
        private static readonly Regex ItalicUnderscoreRegex = new Regex(@"_([^_]+)_", RegexOptions.Compiled);
        private static readonly Regex StrikeRegex = new Regex(@"~~(.+?)~~", RegexOptions.Compiled);
        private static readonly Regex CodeRegex = new Regex(@"`([^`]+)`", RegexOptions.Compiled);
        private static readonly Regex LinkRegex = new Regex(@"\[([^\]]+)\]\(([^)]+)\)", RegexOptions.Compiled);
        private static readonly Regex OrderedListRegex = new Regex(@"^(\d+)\.\s+(.*)$", RegexOptions.Compiled);

        public static void RenderToRichTextBox(RichTextBox rtb, string markdown)
        {
            if (rtb == null) return;
            markdown ??= string.Empty;

            rtb.SuspendLayout();
            rtb.Clear();

            var lines = markdown.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            
            // Use a font family that supports all styles
            var baseFontFamily = rtb.Font.FontFamily;
            var baseSize = rtb.Font.Size;
            
            // Create fonts
            var defaultFont = rtb.Font;
            var boldFont = CreateFontSafe(baseFontFamily, baseSize, FontStyle.Bold, defaultFont);
            var italicFont = CreateFontSafe(baseFontFamily, baseSize, FontStyle.Italic, defaultFont);
            var boldItalicFont = CreateFontSafe(baseFontFamily, baseSize, FontStyle.Bold | FontStyle.Italic, defaultFont);
            var strikeFont = CreateFontSafe(baseFontFamily, baseSize, FontStyle.Strikeout, defaultFont);
            var codeFont = new Font(FontFamily.GenericMonospace, baseSize, FontStyle.Regular);
            var h1Font = CreateFontSafe(baseFontFamily, baseSize + 6, FontStyle.Bold, defaultFont);
            var h2Font = CreateFontSafe(baseFontFamily, baseSize + 4, FontStyle.Bold, defaultFont);
            var h3Font = CreateFontSafe(baseFontFamily, baseSize + 2, FontStyle.Bold, defaultFont);
            var linkFont = CreateFontSafe(baseFontFamily, baseSize, FontStyle.Underline, defaultFont);

            var fonts = new FontSet(defaultFont, boldFont, italicFont, boldItalicFont, strikeFont, codeFont, linkFont);

            bool inCodeBlock = false;
            bool firstLine = true;

            foreach (var rawLine in lines)
            {
                string line = rawLine;

                // Code block toggle
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

                // Inside code block - no formatting
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
                    RenderInlineFormatting(rtb, line.Substring(2), fonts);
                    continue;
                }
                if (line.StartsWith(">"))
                {
                    AppendText(rtb, "│ ", defaultFont, Color.Gray, rtb.BackColor);
                    RenderInlineFormatting(rtb, line.Substring(1), fonts);
                    continue;
                }

                // Nested unordered lists
                if (line.StartsWith("  - ") || line.StartsWith("  * "))
                {
                    AppendText(rtb, "    ◦ ", defaultFont, rtb.ForeColor, rtb.BackColor);
                    RenderInlineFormatting(rtb, line.Substring(4), fonts);
                    continue;
                }

                // Unordered lists
                if (line.StartsWith("- ") || line.StartsWith("* "))
                {
                    AppendText(rtb, "  • ", defaultFont, rtb.ForeColor, rtb.BackColor);
                    RenderInlineFormatting(rtb, line.Substring(2), fonts);
                    continue;
                }

                // Ordered lists
                var orderedMatch = OrderedListRegex.Match(line);
                if (orderedMatch.Success)
                {
                    AppendText(rtb, $"  {orderedMatch.Groups[1].Value}. ", defaultFont, rtb.ForeColor, rtb.BackColor);
                    RenderInlineFormatting(rtb, orderedMatch.Groups[2].Value, fonts);
                    continue;
                }

                // Table separator line (skip)
                if (line.TrimStart().StartsWith("|") && line.Contains("-"))
                {
                    var stripped = line.Replace("|", "").Replace("-", "").Replace(" ", "").Replace(":", "");
                    if (string.IsNullOrEmpty(stripped))
                    {
                        continue;
                    }
                }

                // Table rows
                if (line.TrimStart().StartsWith("|") && line.TrimEnd().EndsWith("|"))
                {
                    var cells = line.Trim().Trim('|').Split('|');
                    foreach (var cell in cells)
                    {
                        AppendText(rtb, "│ ", defaultFont, Color.Gray, rtb.BackColor);
                        RenderInlineFormatting(rtb, cell.Trim(), fonts);
                        AppendText(rtb, " ", defaultFont, rtb.ForeColor, rtb.BackColor);
                    }
                    AppendText(rtb, "│", defaultFont, Color.Gray, rtb.BackColor);
                    continue;
                }

                // Regular line with inline formatting
                RenderInlineFormatting(rtb, line, fonts);
            }

            rtb.ResumeLayout();
        }

        private record FontSet(Font Default, Font Bold, Font Italic, Font BoldItalic, Font Strike, Font Code, Font Link);

        private static Font CreateFontSafe(FontFamily family, float size, FontStyle style, Font fallback)
        {
            try
            {
                if (family.IsStyleAvailable(style))
                {
                    return new Font(family, size, style);
                }
                return new Font(FontFamily.GenericSansSerif, size, style);
            }
            catch
            {
                return fallback;
            }
        }

        private static void RenderInlineFormatting(RichTextBox rtb, string text, FontSet fonts)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            // Process text character by character, looking for markdown patterns
            int i = 0;
            while (i < text.Length)
            {
                // Check for bold+italic (***text***)
                if (i + 2 < text.Length && text.Substring(i, 3) == "***")
                {
                    int end = text.IndexOf("***", i + 3);
                    if (end > i + 3)
                    {
                        string content = text.Substring(i + 3, end - i - 3);
                        AppendText(rtb, content, fonts.BoldItalic, rtb.ForeColor, rtb.BackColor);
                        i = end + 3;
                        continue;
                    }
                }

                // Check for bold (**text**)
                if (i + 1 < text.Length && text.Substring(i, 2) == "**")
                {
                    int end = text.IndexOf("**", i + 2);
                    if (end > i + 2)
                    {
                        string content = text.Substring(i + 2, end - i - 2);
                        AppendText(rtb, content, fonts.Bold, rtb.ForeColor, rtb.BackColor);
                        i = end + 2;
                        continue;
                    }
                }

                // Check for strikethrough (~~text~~)
                if (i + 1 < text.Length && text.Substring(i, 2) == "~~")
                {
                    int end = text.IndexOf("~~", i + 2);
                    if (end > i + 2)
                    {
                        string content = text.Substring(i + 2, end - i - 2);
                        AppendText(rtb, content, fonts.Strike, rtb.ForeColor, rtb.BackColor);
                        i = end + 2;
                        continue;
                    }
                }

                // Check for inline code (`code`)
                if (text[i] == '`')
                {
                    int end = text.IndexOf('`', i + 1);
                    if (end > i + 1)
                    {
                        string content = text.Substring(i + 1, end - i - 1);
                        AppendText(rtb, content, fonts.Code, Color.FromArgb(40, 40, 40), Color.FromArgb(230, 230, 230));
                        i = end + 1;
                        continue;
                    }
                }

                // Check for link ([text](url))
                if (text[i] == '[')
                {
                    int closeBracket = text.IndexOf(']', i + 1);
                    if (closeBracket > i + 1 && closeBracket + 1 < text.Length && text[closeBracket + 1] == '(')
                    {
                        int closeParen = text.IndexOf(')', closeBracket + 2);
                        if (closeParen > closeBracket + 2)
                        {
                            string linkText = text.Substring(i + 1, closeBracket - i - 1);
                            AppendText(rtb, linkText, fonts.Link, Color.Blue, rtb.BackColor);
                            i = closeParen + 1;
                            continue;
                        }
                    }
                }

                // Check for italic (*text* or _text_) - must check after ** to avoid conflict
                if (text[i] == '*' && (i + 1 >= text.Length || text[i + 1] != '*'))
                {
                    int end = -1;
                    for (int j = i + 1; j < text.Length; j++)
                    {
                        if (text[j] == '*' && (j + 1 >= text.Length || text[j + 1] != '*') && (j == 0 || text[j - 1] != '*'))
                        {
                            end = j;
                            break;
                        }
                    }
                    if (end > i + 1)
                    {
                        string content = text.Substring(i + 1, end - i - 1);
                        AppendText(rtb, content, fonts.Italic, rtb.ForeColor, rtb.BackColor);
                        i = end + 1;
                        continue;
                    }
                }

                if (text[i] == '_')
                {
                    int end = text.IndexOf('_', i + 1);
                    if (end > i + 1)
                    {
                        string content = text.Substring(i + 1, end - i - 1);
                        AppendText(rtb, content, fonts.Italic, rtb.ForeColor, rtb.BackColor);
                        i = end + 1;
                        continue;
                    }
                }

                // No pattern matched - append single character
                AppendText(rtb, text[i].ToString(), fonts.Default, rtb.ForeColor, rtb.BackColor);
                i++;
            }
        }

        private static void AppendText(RichTextBox rtb, string text, Font font, Color foreColor, Color backColor)
        {
            if (string.IsNullOrEmpty(text)) return;
            
            int start = rtb.TextLength;
            rtb.AppendText(text);
            int end = rtb.TextLength;

            if (end > start)
            {
                rtb.Select(start, end - start);
                rtb.SelectionFont = font;
                rtb.SelectionColor = foreColor;
                rtb.SelectionBackColor = backColor;
                rtb.SelectionLength = 0;
            }
        }
    }
}
