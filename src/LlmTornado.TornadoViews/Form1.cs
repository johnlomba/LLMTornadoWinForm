using System;
using System.Drawing;
using System.Windows.Forms;

namespace LlmTornado.TornadoViews;

/// <summary>
/// Main form for the TornadoViews chat application.
/// Demonstrates all four fixes: auto-sizing messages, incremental streaming,
/// proper async handling, and cancellation support.
/// </summary>
public partial class Form1 : Form
{
    private ChatWindowControl? _chatWindow;
    private TextBox? _apiKeyTextBox;
    private Button? _connectButton;
    private Panel? _setupPanel;

    public Form1()
    {
        InitializeComponent();
        SetupForm();
    }

    private void SetupForm()
    {
        this.Text = "TornadoViews - LLM Chat Application";
        this.Size = new Size(900, 700);
        this.StartPosition = FormStartPosition.CenterScreen;

        // Create setup panel
        _setupPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 80,
            BackColor = SystemColors.ControlLight,
            Padding = new Padding(10)
        };

        var apiKeyLabel = new Label
        {
            Text = "OpenAI API Key:",
            AutoSize = true,
            Location = new Point(10, 10)
        };

        _apiKeyTextBox = new TextBox
        {
            Location = new Point(10, 35),
            Width = 400,
            UseSystemPasswordChar = false,
            PlaceholderText = "Enter your OpenAI API key here"
        };

        _connectButton = new Button
        {
            Text = "Connect",
            Location = new Point(420, 33),
            Width = 100,
            Height = 25
        };
        _connectButton.Click += ConnectButton_Click;

        var instructionLabel = new Label
        {
            Text = "This demo showcases: 1) Auto-sizing messages, 2) Efficient streaming, 3) Non-blocking UI, 4) Cancellation support",
            AutoSize = true,
            Location = new Point(10, 60),
            ForeColor = Color.DarkGreen
        };

        _setupPanel.Controls.Add(apiKeyLabel);
        _setupPanel.Controls.Add(_apiKeyTextBox);
        _setupPanel.Controls.Add(_connectButton);
        _setupPanel.Controls.Add(instructionLabel);

        // Create chat window
        _chatWindow = new ChatWindowControl
        {
            Dock = DockStyle.Fill
        };

        this.Controls.Add(_chatWindow);
        this.Controls.Add(_setupPanel);
    }

    private void ConnectButton_Click(object? sender, EventArgs e)
    {
        var apiKey = _apiKeyTextBox?.Text.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            MessageBox.Show("Please enter your OpenAI API key.",
                "API Key Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            // Initialize the Tornado API
            var api = new TornadoApi(new TornadoAuthOptions(LLmProviders.OpenAi, apiKey));
            _chatWindow?.SetRuntime(api);

            MessageBox.Show("Connected successfully! You can now start chatting.",
                "Connection Successful", MessageBoxButtons.OK, MessageBoxIcon.Information);

            // Add welcome message
            _chatWindow?.AddAssistantMessage("Hello! I'm ready to chat. All four issues are addressed:\n\n" +
                "1. **Auto-sizing messages** - Messages grow to fit content without internal scrolling\n" +
                "2. **Efficient streaming** - Text is appended incrementally, not re-rendered\n" +
                "3. **Non-blocking UI** - Proper async/await prevents UI freezes\n" +
                "4. **Cancellation support** - Click 'Cancel' to stop ongoing requests\n\n" +
                "Try sending a message!");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error connecting: {ex.Message}",
                "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
