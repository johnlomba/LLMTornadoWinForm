using System;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TornadoViews
{
    public class ChatWindowControl : UserControl
    {
        private FlowLayoutPanel chatPanel = null!;
        private TextBox inputBox = null!;
        private Button sendButton = null!;

        public event EventHandler<string>? SendRequested;
        public event EventHandler<ToolUseDecision>? ToolUseDecisionChanged;

        private ChatMessageControl? streamingAssistant;
        private string streamingBuffer = string.Empty;

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
            sendButton.Click += async (s, e) => await SendAsync();

            inputPanel.Controls.Add(inputBox);
            inputPanel.Controls.Add(sendButton);

            Controls.Add(chatPanel);
            Controls.Add(inputPanel);
        }

        public async Task SendAsync()
        {
            string text = inputBox.Text;
            if (string.IsNullOrWhiteSpace(text))
                return;

            var userControl = new ChatMessageControl
            {
                Role = "You",
                MarkdownText = text
            };
            userControl.CopyRequested += (s, _) => Clipboard.SetText(userControl.GetPlainText());
            userControl.RetryRequested += async (s, _) => await SendAsync();
            userControl.ToolUseDecisionChanged += (s, d) => ToolUseDecisionChanged?.Invoke(this, d);
            chatPanel.Controls.Add(userControl);

            SendRequested?.Invoke(this, text);
            inputBox.Clear();
        }

        public void AppendAssistantMessage(string markdown)
        {
            var assistant = new ChatMessageControl
            {
                Role = "Assistant",
                MarkdownText = markdown
            };
            assistant.CopyRequested += (s, _) => Clipboard.SetText(assistant.GetPlainText());
            assistant.RetryRequested += (s, _) => { /* no-op for assistant */ };
            assistant.ToolUseDecisionChanged += (s, d) => ToolUseDecisionChanged?.Invoke(this, d);
            chatPanel.Controls.Add(assistant);
        }

        // Streaming helpers used by controller
        public void BeginAssistantStream()
        {
            streamingBuffer = string.Empty;
            streamingAssistant = new ChatMessageControl { Role = "Assistant", MarkdownText = string.Empty };
            streamingAssistant.ToolUseDecisionChanged += (s, d) => ToolUseDecisionChanged?.Invoke(this, d);
            chatPanel.Controls.Add(streamingAssistant);
        }

        public void AppendAssistantStream(string delta)
        {
            if (string.IsNullOrEmpty(delta)) return;
            if (streamingAssistant == null)
            {
                BeginAssistantStream();
            }
            streamingBuffer += delta;
            if (streamingAssistant != null)
            {
                streamingAssistant.MarkdownText = streamingBuffer;
            }
        }

        public void SetStreamingToolDecisionVisible(bool visible)
        {
            if (streamingAssistant == null) return;
            streamingAssistant.ShowToolDecision = visible;
        }

        public void SetStreamingToolRequest(string request)
        {
            if (streamingAssistant == null) return;
            streamingAssistant.ToolRequestText = request;
        }

        public void EndAssistantStream(string? final = null)
        {
            if (!string.IsNullOrEmpty(final))
            {
                streamingBuffer = final;
                if (streamingAssistant != null)
                {
                    streamingAssistant.MarkdownText = streamingBuffer;
                }
            }
            streamingAssistant = null;
            streamingBuffer = string.Empty;
        }
    }
}
