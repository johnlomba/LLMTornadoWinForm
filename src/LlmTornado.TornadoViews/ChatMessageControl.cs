using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace LlmTornado.TornadoViews;

/// <summary>
/// A control that displays a single chat message with proper auto-sizing.
/// Addresses Issue 1: Uses AutoSize instead of fixed width, no internal scrolling.
/// </summary>
public class ChatMessageControl : UserControl
{
    private readonly RichTextBox _contentTextBox;
    private readonly Label _roleLabel;
    private readonly Panel _contentPanel;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Role
    {
        get => _roleLabel.Text;
        set => _roleLabel.Text = value;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string MarkdownText
    {
        get => _contentTextBox.Text;
        set
        {
            _contentTextBox.Clear();
            ChatMarkdownRenderer.RenderToRichTextBox(_contentTextBox, value);
            AdjustHeight();
        }
    }

    public ChatMessageControl()
    {
        // Initialize components
        _roleLabel = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Location = new Point(10, 10),
            Padding = new Padding(0, 0, 0, 5)
        };

        _contentTextBox = new RichTextBox
        {
            BorderStyle = BorderStyle.None,
            ReadOnly = true,
            Location = new Point(10, 35),
            ScrollBars = RichTextBoxScrollBars.None, // No internal scrolling
            Font = new Font("Segoe UI", 9F),
            BackColor = SystemColors.Control,
            WordWrap = true
        };

        _contentPanel = new Panel
        {
            Location = new Point(0, 0),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(5)
        };

        // Configure this control
        this.AutoSize = true;
        this.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        this.Padding = new Padding(5);
        this.BackColor = Color.White;
        this.BorderStyle = BorderStyle.FixedSingle;
        this.Margin = new Padding(5);

        // Add controls to panel
        _contentPanel.Controls.Add(_roleLabel);
        _contentPanel.Controls.Add(_contentTextBox);
        this.Controls.Add(_contentPanel);

        // Subscribe to resize events
        _contentTextBox.ContentsResized += ContentTextBox_ContentsResized;
    }

    private void ContentTextBox_ContentsResized(object? sender, ContentsResizedEventArgs e)
    {
        // Use the actual measured size from the event
        if (e.NewRectangle.Height > 0)
        {
            _contentTextBox.Height = e.NewRectangle.Height + 5; // Small padding
            _contentPanel.Height = _contentTextBox.Bottom + 10;
            this.Height = _contentPanel.Height + 10;
        }
    }

    private void AdjustHeight()
    {
        if (_contentTextBox.Text.Length == 0)
        {
            _contentTextBox.Height = 20;
            _contentPanel.Height = _contentTextBox.Bottom + 10;
            this.Height = _contentPanel.Height + 10;
        }
        // Otherwise let ContentsResized handle it
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (_contentTextBox != null && this.Width > 20)
        {
            _contentTextBox.Width = this.Width - 30;
            AdjustHeight();
        }
    }

    /// <summary>
    /// Appends text incrementally to the message content.
    /// Used for streaming scenarios.
    /// </summary>
    public void AppendText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        _contentTextBox.AppendText(text);
        AdjustHeight();
    }

    /// <summary>
    /// Appends markdown text incrementally to the message content.
    /// Used for streaming scenarios.
    /// </summary>
    public void AppendMarkdown(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return;

        ChatMarkdownRenderer.AppendMarkdownToRichTextBox(_contentTextBox, markdown);
        AdjustHeight();
    }
}
