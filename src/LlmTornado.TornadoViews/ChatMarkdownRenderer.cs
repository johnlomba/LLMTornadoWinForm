using System.Drawing;
using System.Windows.Forms;
using System.Text;

namespace LlmTornado.TornadoViews;

/// <summary>
/// Renders markdown text to a RichTextBox control with basic formatting support.
/// Optimized for incremental updates during streaming.
/// </summary>
public static class ChatMarkdownRenderer
{
    /// <summary>
    /// Renders markdown text to a RichTextBox. This method performs a full render
    /// and should only be used for initial rendering or complete updates.
    /// For streaming scenarios, use AppendMarkdownToRichTextBox instead.
    /// </summary>
    public static void RenderToRichTextBox(RichTextBox richTextBox, string markdown)
    {
        if (richTextBox == null || markdown == null)
            return;

        richTextBox.Clear();
        AppendMarkdownToRichTextBox(richTextBox, markdown);
    }

    /// <summary>
    /// Appends markdown text to a RichTextBox incrementally.
    /// This is optimized for streaming scenarios where text is added progressively.
    /// </summary>
    public static void AppendMarkdownToRichTextBox(RichTextBox richTextBox, string markdown)
    {
        if (richTextBox == null || string.IsNullOrEmpty(markdown))
            return;

        // Suspend layout for better performance
        richTextBox.SuspendLayout();

        try
        {
            var lines = markdown.Split('\n');
            foreach (var line in lines)
            {
                AppendFormattedLine(richTextBox, line);
            }
        }
        finally
        {
            richTextBox.ResumeLayout();
        }
    }

    private static void AppendFormattedLine(RichTextBox richTextBox, string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            richTextBox.AppendText("\n");
            return;
        }

        // Handle headers (# ## ###)
        if (line.StartsWith("### "))
        {
            AppendStyledText(richTextBox, line.Substring(4), FontStyle.Bold, 11);
            richTextBox.AppendText("\n");
            return;
        }
        if (line.StartsWith("## "))
        {
            AppendStyledText(richTextBox, line.Substring(3), FontStyle.Bold, 12);
            richTextBox.AppendText("\n");
            return;
        }
        if (line.StartsWith("# "))
        {
            AppendStyledText(richTextBox, line.Substring(2), FontStyle.Bold, 14);
            richTextBox.AppendText("\n");
            return;
        }

        // Handle code blocks (```)
        if (line.StartsWith("```"))
        {
            richTextBox.SelectionBackColor = Color.LightGray;
            richTextBox.SelectionFont = new Font("Consolas", 9, FontStyle.Regular);
            richTextBox.AppendText(line + "\n");
            richTextBox.SelectionBackColor = richTextBox.BackColor;
            richTextBox.SelectionFont = richTextBox.Font;
            return;
        }

        // Handle inline code (`code`)
        if (line.Contains("`"))
        {
            AppendLineWithInlineCode(richTextBox, line);
            return;
        }

        // Handle bold (**text**)
        if (line.Contains("**"))
        {
            AppendLineWithBold(richTextBox, line);
            return;
        }

        // Plain text
        richTextBox.AppendText(line + "\n");
    }

    private static void AppendLineWithInlineCode(RichTextBox richTextBox, string line)
    {
        var parts = line.Split('`');
        for (int i = 0; i < parts.Length; i++)
        {
            if (i % 2 == 1) // Inside backticks
            {
                richTextBox.SelectionBackColor = Color.LightGray;
                richTextBox.SelectionFont = new Font("Consolas", 9, FontStyle.Regular);
                richTextBox.AppendText(parts[i]);
                richTextBox.SelectionBackColor = richTextBox.BackColor;
                richTextBox.SelectionFont = richTextBox.Font;
            }
            else
            {
                richTextBox.AppendText(parts[i]);
            }
        }
        richTextBox.AppendText("\n");
    }

    private static void AppendLineWithBold(RichTextBox richTextBox, string line)
    {
        var parts = line.Split(new[] { "**" }, StringSplitOptions.None);
        for (int i = 0; i < parts.Length; i++)
        {
            if (i % 2 == 1) // Inside bold markers
            {
                AppendStyledText(richTextBox, parts[i], FontStyle.Bold, null);
            }
            else
            {
                richTextBox.AppendText(parts[i]);
            }
        }
        richTextBox.AppendText("\n");
    }

    private static void AppendStyledText(RichTextBox richTextBox, string text, FontStyle style, float? fontSize = null)
    {
        var originalFont = richTextBox.SelectionFont ?? richTextBox.Font;
        var size = fontSize ?? originalFont.Size;
        richTextBox.SelectionFont = new Font(originalFont.FontFamily, size, style);
        richTextBox.AppendText(text);
        richTextBox.SelectionFont = originalFont;
    }
}
