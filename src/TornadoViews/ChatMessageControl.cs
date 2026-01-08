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
        public event EventHandler CopyRequested;
        public event EventHandler RetryRequested;
        public event EventHandler<ToolUseDecision> ToolUseDecisionChanged;

        private Label roleLabel;
        private RichTextBox messageBox;
        private FlowLayoutPanel actionPanel;
        private Button copyButton;
        private Button retryButton;
        private Button acceptToolButton;
        private Button denyToolButton;

        private ToolUseDecision decision = ToolUseDecision.None;

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
                RowCount = 3,
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
            acceptToolButton = new Button { Text = "Accept tool" };
            denyToolButton = new Button { Text = "Deny tool" };

            copyButton.Click += (s, e) => CopyRequested?.Invoke(this, EventArgs.Empty);
            retryButton.Click += (s, e) => RetryRequested?.Invoke(this, EventArgs.Empty);
            acceptToolButton.Click += (s, e) => SetDecision(ToolUseDecision.Accept);
            denyToolButton.Click += (s, e) => SetDecision(ToolUseDecision.Deny);

            actionPanel.Controls.Add(copyButton);
            actionPanel.Controls.Add(retryButton);
            actionPanel.Controls.Add(acceptToolButton);
            actionPanel.Controls.Add(denyToolButton);

            mainLayout.Controls.Add(roleLabel, 0, 0);
            mainLayout.Controls.Add(messageBox, 0, 1);
            mainLayout.Controls.Add(actionPanel, 0, 2);

            Controls.Add(mainLayout);
        }

        private void SetDecision(ToolUseDecision newDecision)
        {
            decision = newDecision;
            ToolUseDecisionChanged?.Invoke(this, newDecision);

            // visual feedback
            acceptToolButton.Enabled = decision != ToolUseDecision.Accept;
            denyToolButton.Enabled = decision != ToolUseDecision.Deny;
        }

        public ToolUseDecision GetDecision() => decision;

        public string GetPlainText() => messageBox.Text;
    }
}
