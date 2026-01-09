using System;
using System.Drawing;
using System.Windows.Forms;

namespace TornadoViews
{
    public enum ToolUseDecision
    {
        None,
        Accept,
        Deny
    }

    public class ChatMessageControl : UserControl
    {
        public event EventHandler? CopyRequested;
        public event EventHandler? RetryRequested;
        public event EventHandler<ToolUseDecision>? ToolUseDecisionChanged;

        private Label roleLabel = null!;
        private RichTextBox messageBox = null!;
        private FlowLayoutPanel actionPanel = null!;
        private Button copyButton = null!;
        private Button retryButton = null!;
        private ToolCallPanel toolCallPanel = null!;

        private ToolUseDecision decision = ToolUseDecision.None;

        public bool ShowToolDecision
        {
            get => toolCallPanel.Visible;
            set => toolCallPanel.Visible = value;
        }

        public string Role
        {
            get => roleLabel.Text;
            set => roleLabel.Text = value;
        }

        public string MarkdownText
        {
            get => messageBox.Text;
            set => ChatMarkdownRenderer.RenderToRichTextBox(messageBox, value);
        }

        public string ToolRequestText
        {
            get => $"Tool: {toolCallPanel.ToolName}\nArguments: {toolCallPanel.Arguments}";
            set => toolCallPanel.SetToolRequest(value);
        }

        public ChatMessageControl()
        {
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            // layout
            this.Dock = DockStyle.Top;
            this.Padding = new Padding(8);
            this.BackColor = Color.Transparent;
            this.AutoSize = true;
            this.AutoSizeMode = AutoSizeMode.GrowAndShrink;

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                AutoSize = true
            };

            roleLabel = new Label
            {
                AutoSize = true,
                Font = new Font(Font, FontStyle.Bold),
                ForeColor = SystemColors.GrayText
            };

            messageBox = new RichTextBox
            {
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                DetectUrls = true,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                BackColor = SystemColors.Window,
                Dock = DockStyle.Top,
                Width = 600
            };

            // Tool call panel (replaces simple label)
            toolCallPanel = new ToolCallPanel
            {
                Visible = false,
                Width = 580
            };
            toolCallPanel.ToolUseDecisionChanged += (s, d) =>
            {
                decision = d;
                ToolUseDecisionChanged?.Invoke(this, d);
            };

            actionPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };

            copyButton = new Button { Text = "Copy" };
            retryButton = new Button { Text = "Try again" };

            copyButton.Click += (s, e) => CopyRequested?.Invoke(this, EventArgs.Empty);
            retryButton.Click += (s, e) => RetryRequested?.Invoke(this, EventArgs.Empty);

            actionPanel.Controls.Add(copyButton);
            actionPanel.Controls.Add(retryButton);

            mainLayout.Controls.Add(roleLabel, 0, 0);
            mainLayout.Controls.Add(messageBox, 0, 1);
            mainLayout.Controls.Add(toolCallPanel, 0, 2);
            mainLayout.Controls.Add(actionPanel, 0, 3);

            Controls.Add(mainLayout);
        }

        private void SetDecision(ToolUseDecision newDecision)
        {
            decision = newDecision;
            ToolUseDecisionChanged?.Invoke(this, newDecision);
        }

        public ToolUseDecision GetDecision() => decision;

        public string GetPlainText() => messageBox.Text;
    }
}
