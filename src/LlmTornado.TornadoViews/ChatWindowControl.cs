using System;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using LlmTornado.Chat;
using LlmTornado.ChatFunctions;

namespace LlmTornado.TornadoViews;

/// <summary>
/// Main chat window control with support for streaming, proper async handling, and cancellation.
/// Addresses Issues 2, 3, and 4 from the problem statement.
/// </summary>
public class ChatWindowControl : UserControl
{
    private readonly FlowLayoutPanel _messagesPanel;
    private readonly TextBox _inputTextBox;
    private readonly Button _sendButton;
    private readonly Button _cancelButton;
    private readonly Panel _inputPanel;
    
    private TornadoApi? _runtime;
    private ChatMessageControl? _streamingAssistant;
    private StringBuilder _streamingBuffer = new StringBuilder();
    private CancellationTokenSource? _currentRequestCts;
    private string _lastStreamedContent = string.Empty;

    public event EventHandler<SendMessageEventArgs>? SendRequested;

    public ChatWindowControl()
    {
        InitializeComponents();
    }

    private void InitializeComponents()
    {
        // Messages panel with auto-scroll
        _messagesPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(10),
            BackColor = Color.WhiteSmoke
        };

        // Input panel
        _inputPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 80,
            Padding = new Padding(10),
            BackColor = SystemColors.Control
        };

        // Input text box
        _inputTextBox = new TextBox
        {
            Multiline = true,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10F),
            PlaceholderText = "Type your message here..."
        };

        // Send button
        _sendButton = new Button
        {
            Text = "Send",
            Dock = DockStyle.Right,
            Width = 80,
            Height = 60,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold)
        };
        _sendButton.Click += OnSendButtonClick;

        // Cancel button - Addresses Issue 4
        _cancelButton = new Button
        {
            Text = "Cancel",
            Dock = DockStyle.Right,
            Width = 80,
            Height = 60,
            Font = new Font("Segoe UI", 10F),
            Enabled = false,
            BackColor = Color.LightCoral
        };
        _cancelButton.Click += OnCancelButtonClick;

        // Add controls to input panel
        _inputPanel.Controls.Add(_inputTextBox);
        _inputPanel.Controls.Add(_cancelButton);
        _inputPanel.Controls.Add(_sendButton);

        // Add panels to main control
        this.Controls.Add(_messagesPanel);
        this.Controls.Add(_inputPanel);

        // Handle Enter key in text box
        _inputTextBox.KeyDown += InputTextBox_KeyDown;
    }

    public void SetRuntime(TornadoApi runtime)
    {
        _runtime = runtime;
    }

    private void InputTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter && e.Control && _sendButton.Enabled)
        {
            OnSendButtonClick(sender, e);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }

    private void OnSendButtonClick(object? sender, EventArgs e)
    {
        var message = _inputTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(message))
            return;

        // Clear input
        _inputTextBox.Clear();

        // Add user message
        AddUserMessage(message);

        // Send message (fire and forget with proper error handling)
        _ = SendMessageAsync(message);
    }

    private void OnCancelButtonClick(object? sender, EventArgs e)
    {
        // Cancel the current request - Addresses Issue 4
        _currentRequestCts?.Cancel();
    }

    private async Task SendMessageAsync(string message)
    {
        if (_runtime == null)
        {
            MessageBox.Show("Runtime not initialized. Please set up the API client first.",
                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        // Create new cancellation token for this request - Addresses Issue 4
        _currentRequestCts = new CancellationTokenSource();
        var cancellationToken = _currentRequestCts.Token;

        try
        {
            // Disable send button and enable cancel button
            SetControlsState(sending: true);

            // Create streaming assistant message
            _streamingAssistant = new ChatMessageControl
            {
                Role = "Assistant",
                Width = _messagesPanel.ClientSize.Width - 40
            };
            
            _messagesPanel.Controls.Add(_streamingAssistant);
            _streamingBuffer.Clear();
            _lastStreamedContent = string.Empty;

            // Create chat request
            var chatRequest = new ChatRequest
            {
                Model = "gpt-4",
                Messages = new[]
                {
                    new ChatMessage(ChatMessageRole.User, message)
                }
            };

            // Stream response - Addresses Issue 3 (proper async/await)
            await foreach (var delta in _runtime.Chat.StreamChatEnumerableAsync(chatRequest, cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                if (delta.Choices != null && delta.Choices.Count > 0)
                {
                    var content = delta.Choices[0].Delta?.Content;
                    if (!string.IsNullOrEmpty(content))
                    {
                        // Append to buffer
                        _streamingBuffer.Append(content);

                        // Update UI - Addresses Issue 2 (incremental append instead of full re-render)
                        await AppendAssistantStreamAsync(content);
                    }
                }
            }

            // Finalize the message
            if (_streamingAssistant != null && !cancellationToken.IsCancellationRequested)
            {
                _streamingAssistant.MarkdownText = _streamingBuffer.ToString();
            }
        }
        catch (OperationCanceledException)
        {
            // User cancelled the request
            if (_streamingAssistant != null)
            {
                _streamingAssistant.AppendText("\n\n[Request cancelled by user]");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error sending message: {ex.Message}",
                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            // Re-enable controls
            SetControlsState(sending: false);
            _streamingAssistant = null;
            _currentRequestCts?.Dispose();
            _currentRequestCts = null;
        }
    }

    /// <summary>
    /// Appends streamed content incrementally to the assistant message.
    /// Addresses Issue 2: Appends text instead of re-rendering entire content.
    /// </summary>
    private Task AppendAssistantStreamAsync(string delta)
    {
        if (_streamingAssistant == null || string.IsNullOrEmpty(delta))
            return Task.CompletedTask;

        // Use Invoke to marshal to UI thread - Addresses Issue 3
        if (_streamingAssistant.InvokeRequired)
        {
            return Task.Run(() =>
            {
                _streamingAssistant.Invoke(() =>
                {
                    _streamingAssistant.AppendText(delta);
                    ScrollToBottom();
                });
            });
        }
        else
        {
            _streamingAssistant.AppendText(delta);
            ScrollToBottom();
            return Task.CompletedTask;
        }
    }

    private void SetControlsState(bool sending)
    {
        if (InvokeRequired)
        {
            Invoke(() => SetControlsState(sending));
            return;
        }

        _sendButton.Enabled = !sending;
        _inputTextBox.Enabled = !sending;
        _cancelButton.Enabled = sending;
    }

    public void AddUserMessage(string message)
    {
        var messageControl = new ChatMessageControl
        {
            Role = "User",
            MarkdownText = message,
            Width = _messagesPanel.ClientSize.Width - 40,
            BackColor = Color.LightBlue
        };

        if (_messagesPanel.InvokeRequired)
        {
            _messagesPanel.Invoke(() => _messagesPanel.Controls.Add(messageControl));
        }
        else
        {
            _messagesPanel.Controls.Add(messageControl);
        }

        ScrollToBottom();
    }

    public void AddAssistantMessage(string message)
    {
        var messageControl = new ChatMessageControl
        {
            Role = "Assistant",
            MarkdownText = message,
            Width = _messagesPanel.ClientSize.Width - 40
        };

        if (_messagesPanel.InvokeRequired)
        {
            _messagesPanel.Invoke(() => _messagesPanel.Controls.Add(messageControl));
        }
        else
        {
            _messagesPanel.Controls.Add(messageControl);
        }

        ScrollToBottom();
    }

    public void ClearMessages()
    {
        if (_messagesPanel.InvokeRequired)
        {
            _messagesPanel.Invoke(() => _messagesPanel.Controls.Clear());
        }
        else
        {
            _messagesPanel.Controls.Clear();
        }
    }

    private void ScrollToBottom()
    {
        if (_messagesPanel.InvokeRequired)
        {
            _messagesPanel.Invoke(ScrollToBottom);
            return;
        }

        _messagesPanel.AutoScrollPosition = new Point(0, _messagesPanel.VerticalScroll.Maximum);
    }
}

public class SendMessageEventArgs : EventArgs
{
    public string Message { get; }
    
    public SendMessageEventArgs(string message)
    {
        Message = message;
    }
}
