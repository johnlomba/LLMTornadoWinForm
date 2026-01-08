using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Linq;

namespace TornadoViews
{
    public static class ChatMarkdownRenderer
    {
        // Very lightweight markdown renderer for headings, bold, code blocks and lists
        public static void RenderToRichTextBox(RichTextBox rtb, string markdown)
        {
            rtb.SuspendLayout();
            rtb.Clear();

            var lines = markdown.Replace("\r\n", "\n").Split('\n');
            bool inCode = false;
            var defaultFont = rtb.Font;
            var codeFont = new Font(FontFamily.GenericMonospace, defaultFont.Size);

            foreach (var raw in lines)
            {
                var line = raw;
                if (line.TrimStart().StartsWith("```"))
                {
                    inCode = !inCode;
                    continue;
                }

                if (inCode)
                {
                    AppendLine(rtb, line, codeFont, Color.FromArgb(245, 245, 245));
                    continue;
                }

                // headings
                if (Regex.IsMatch(line, "^#{1,6} "))
                {
                    int level = line.TakeWhile(c => c == '#').Count();
                    string text = line.Substring(level).TrimStart();
                    var font = new Font(defaultFont.FontFamily, Math.Max(12, defaultFont.Size + (6 - level)), FontStyle.Bold);
                    AppendLine(rtb, text, font, Color.Transparent);
                    continue;
                }

                // bullets
                if (Regex.IsMatch(line.TrimStart(), "^[-*] "))
                {
                    AppendLine(rtb, "• " + line.TrimStart().Substring(2), defaultFont, Color.Transparent);
                    continue;
                }

                // inline code `code`
                var parts = Regex.Split(line, @"(`[^`]+`)");
                foreach (var part in parts)
                {
                    if (Regex.IsMatch(part, @"^`.*`$"))
                    {
                        Append(rtb, part.Trim('`'), codeFont, Color.FromArgb(245, 245, 245));
                    }
                    else
                    {
                        // bold **text**
                        var boldParts = Regex.Split(part, @"(\*\*[^*]+\*\*)");
                        foreach (var bp in boldParts)
                        {
                            if (Regex.IsMatch(bp, @"^\*\*.*\*\*$"))
                            {
                                Append(rtb, bp.Trim('*'), new Font(defaultFont, FontStyle.Bold), Color.Transparent);
                            }
                            else
                            {
                                Append(rtb, bp, defaultFont, Color.Transparent);
                            }
                        }
                    }
                }
                rtb.AppendText("\n");
            }

            rtb.ResumeLayout();
        }

        private static void AppendLine(RichTextBox rtb, string text, Font font, Color backColor)
        {
            int start = rtb.TextLength;
            rtb.AppendText(text + "\n");
            rtb.Select(start, text.Length);
            rtb.SelectionFont = font;
            if (backColor != Color.Transparent)
            {
                rtb.SelectionBackColor = backColor;
            }
            rtb.Select(rtb.TextLength, 0);
        }

        private static void Append(RichTextBox rtb, string text, Font font, Color backColor)
        {
            int start = rtb.TextLength;
            rtb.AppendText(text);
            rtb.Select(start, text.Length);
            rtb.SelectionFont = font;
            if (backColor != Color.Transparent)
            {
                rtb.SelectionBackColor = backColor;
            }
            rtb.Select(rtb.TextLength, 0);
        }
    }
}
