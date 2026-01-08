using System;
using System.Drawing;
using System.Windows.Forms;

namespace TornadoViews
{
    public class ChatWindowControl : UserControl
    {
        private FlowLayoutPanel chatPanel;
        private TextBox inputBox;
        private Button sendButton;

        public event EventHandler<string> SendRequested;
        public event EventHandler<ToolUseDecision> ToolUseDecisionChanged;

        public ChatWindowControl()
        {
            InitializeChatUi();
        }

        private void InitializeChatUi()
        {
            Dock = DockStyle.Fill;

            chatPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false
            };

            var inputPanel = new Panel { Dock = DockStyle.Bottom, Height = 80 };
            inputBox = new TextBox { Multiline = true, Dock = DockStyle.Fill };
            sendButton = new Button { Text = "Send", Dock = DockStyle.Right, Width = 100 };
            sendButton.Click += SendButton_Click;

            inputPanel.Controls.Add(inputBox);
            inputPanel.Controls.Add(sendButton);

            Controls.Add(chatPanel);
            Controls.Add(inputPanel);
        }

        private void SendButton_Click(object? sender, EventArgs e)
        {
            var userControl = new ChatMessageControl
            {
                Role = "You",
                MarkdownText = inputBox.Text
            };
            userControl.CopyRequested += (s, _) => Clipboard.SetText(userControl.GetPlainText());
            userControl.RetryRequested += (s, _) => MessageBox.Show("Retrying: " + userControl.GetPlainText());
            userControl.ToolUseDecisionChanged += (s, d) => ToolUseDecisionChanged?.Invoke(this, d);
            chatPanel.Controls.Add(userControl);

            SendRequested?.Invoke(this, inputBox.Text);
            inputBox.Clear();
        }

        private void InitializeComponent()
        {

        }

        public void AppendAssistantMessage(string markdown)
        {
            var assistant = new ChatMessageControl
            {
                Role = "Assistant",
                MarkdownText = markdown
            };
            assistant.CopyRequested += (s, _) => Clipboard.SetText(assistant.GetPlainText());
            assistant.RetryRequested += (s, _) => MessageBox.Show("Retrying: " + assistant.GetPlainText());
            assistant.ToolUseDecisionChanged += (s, d) => ToolUseDecisionChanged?.Invoke(this, d);
            chatPanel.Controls.Add(assistant);
        }
    }
}
