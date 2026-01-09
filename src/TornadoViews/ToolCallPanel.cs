using System;
using System.Drawing;
using System.Text.Json;
using System.Windows.Forms;

namespace TornadoViews
{
    /// <summary>
    /// A dedicated panel for displaying tool call information with collapsible arguments.
    /// Handles large JSON arguments gracefully with scrolling and formatted display.
    /// </summary>
    public class ToolCallPanel : UserControl
    {
        public event EventHandler<ToolUseDecision>? ToolUseDecisionChanged;

        private Panel headerPanel = null!;
        private Label toolNameLabel = null!;
        private Button toggleButton = null!;
        private Panel argumentsPanel = null!;
        private RichTextBox argumentsTextBox = null!;
        private FlowLayoutPanel buttonPanel = null!;
        private Button acceptButton = null!;
        private Button denyButton = null!;

        private bool _isExpanded = true;
        private string _toolName = string.Empty;
        private string _arguments = string.Empty;
        private const int CollapsedHeight = 80;
        private const int ExpandedArgumentsHeight = 150;

        public string ToolName
        {
            get => _toolName;
            set
            {
                _toolName = value;
                toolNameLabel.Text = $"?? Tool: {value}";
            }
        }

        public string Arguments
        {
            get => _arguments;
            set
            {
                _arguments = value;
                argumentsTextBox.Text = FormatJson(value);
            }
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                _isExpanded = value;
                UpdateExpandedState();
            }
        }

        public ToolCallPanel()
        {
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            this.Dock = DockStyle.Top;
            this.AutoSize = true;
            this.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            this.Padding = new Padding(4);
            this.Margin = new Padding(0, 8, 0, 8);
            this.BackColor = Color.FromArgb(255, 245, 235);
            this.BorderStyle = BorderStyle.FixedSingle;

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };

            // Header panel with tool name and toggle button
            headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 30,
                Padding = new Padding(4)
            };

            toolNameLabel = new Label
            {
                Text = "?? Tool: ",
                Font = new Font(Font.FontFamily, 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(139, 69, 19),
                AutoSize = true,
                Dock = DockStyle.Left
            };

            toggleButton = new Button
            {
                Text = "? Hide Args",
                Width = 90,
                Height = 24,
                Dock = DockStyle.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(255, 228, 196),
                ForeColor = Color.FromArgb(139, 69, 19)
            };
            toggleButton.FlatAppearance.BorderColor = Color.FromArgb(210, 180, 140);
            toggleButton.Click += (s, e) => IsExpanded = !IsExpanded;

            headerPanel.Controls.Add(toolNameLabel);
            headerPanel.Controls.Add(toggleButton);

            // Arguments panel (collapsible)
            argumentsPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = ExpandedArgumentsHeight,
                Padding = new Padding(4)
            };

            var argsLabel = new Label
            {
                Text = "Arguments:",
                Font = new Font(Font.FontFamily, 9f, FontStyle.Italic),
                ForeColor = Color.FromArgb(100, 100, 100),
                Dock = DockStyle.Top,
                Height = 18
            };

            argumentsTextBox = new RichTextBox
            {
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(255, 250, 245),
                Font = new Font("Consolas", 9f),
                Dock = DockStyle.Fill,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                WordWrap = true
            };

            argumentsPanel.Controls.Add(argumentsTextBox);
            argumentsPanel.Controls.Add(argsLabel);

            // Button panel
            buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                Padding = new Padding(4),
                BackColor = Color.Transparent
            };

            acceptButton = new Button
            {
                Text = "? Accept",
                Width = 100,
                Height = 30,
                BackColor = Color.FromArgb(144, 238, 144),
                FlatStyle = FlatStyle.Flat,
                Font = new Font(Font.FontFamily, 9f, FontStyle.Bold)
            };
            acceptButton.FlatAppearance.BorderColor = Color.FromArgb(60, 179, 113);
            acceptButton.Click += (s, e) => OnDecision(ToolUseDecision.Accept);

            denyButton = new Button
            {
                Text = "? Deny",
                Width = 100,
                Height = 30,
                BackColor = Color.FromArgb(255, 182, 193),
                FlatStyle = FlatStyle.Flat,
                Font = new Font(Font.FontFamily, 9f, FontStyle.Bold)
            };
            denyButton.FlatAppearance.BorderColor = Color.FromArgb(220, 20, 60);
            denyButton.Click += (s, e) => OnDecision(ToolUseDecision.Deny);

            buttonPanel.Controls.Add(acceptButton);
            buttonPanel.Controls.Add(denyButton);

            mainLayout.Controls.Add(headerPanel, 0, 0);
            mainLayout.Controls.Add(argumentsPanel, 0, 1);
            mainLayout.Controls.Add(buttonPanel, 0, 2);

            Controls.Add(mainLayout);
        }

        private void UpdateExpandedState()
        {
            argumentsPanel.Visible = _isExpanded;
            toggleButton.Text = _isExpanded ? "? Hide Args" : "? Show Args";
        }

        private void OnDecision(ToolUseDecision decision)
        {
            acceptButton.Enabled = decision != ToolUseDecision.Accept;
            denyButton.Enabled = decision != ToolUseDecision.Deny;
            ToolUseDecisionChanged?.Invoke(this, decision);
        }

        /// <summary>
        /// Sets the tool request from a formatted string (e.g., "Tool: Name\nArguments: {...}")
        /// </summary>
        public void SetToolRequest(string request)
        {
            if (string.IsNullOrEmpty(request))
            {
                ToolName = string.Empty;
                Arguments = string.Empty;
                return;
            }

            var lines = request.Split('\n');
            foreach (var line in lines)
            {
                if (line.StartsWith("Tool:", StringComparison.OrdinalIgnoreCase))
                {
                    ToolName = line.Substring(5).Trim();
                }
                else if (line.StartsWith("Arguments:", StringComparison.OrdinalIgnoreCase))
                {
                    Arguments = line.Substring(10).Trim();
                }
            }

            // If no "Arguments:" prefix found, check if remaining content is JSON
            if (string.IsNullOrEmpty(Arguments) && lines.Length > 1)
            {
                var remainingContent = string.Join("\n", lines, 1, lines.Length - 1).Trim();
                if (remainingContent.StartsWith("{") || remainingContent.StartsWith("["))
                {
                    Arguments = remainingContent;
                }
            }
        }

        /// <summary>
        /// Attempts to format JSON for better readability
        /// </summary>
        private static string FormatJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return string.Empty;

            try
            {
                using var doc = JsonDocument.Parse(json);
                return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
            }
            catch
            {
                // If not valid JSON, return as-is
                return json;
            }
        }

        public void Reset()
        {
            ToolName = string.Empty;
            Arguments = string.Empty;
            acceptButton.Enabled = true;
            denyButton.Enabled = true;
            IsExpanded = true;
        }
    }
}
