using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using LlmTornado.Agents;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Agents.Utility;
using LlmTornado.Agents.DataModels;

namespace TornadoViews
{
    /// <summary>
    /// Controller to bridge a ChatWindowControl with a TornadoAgent lifecycle.
    /// </summary>
    /// <remarks>
    /// TODO: Add CancellationToken support - pass token to agent.Run() and add cancel button to UI
    /// TODO: Wire up RetryRequested event from ChatMessageControl to re-send failed messages
    /// TODO: Consider using ConversationPersistence class instead of extension methods for auto-save support
    /// TODO: Add error state display in chat UI instead of MessageBox
    /// TODO: Expose retry logic via OnRetryRequested event
    /// </remarks>
    public class AgentChatController
    {
        private readonly ChatWindowControl _view;
        private TornadoAgent? _agent;
        private Conversation? _conversation;

        // Optional external tool approval handler (e.g., custom dialog)
        public Func<string, ValueTask<bool>>? ToolApprovalHandler { get; set; }

        public AgentChatController(ChatWindowControl view)
        {
            _view = view;
            _view.SendRequested += OnSendRequested;
            _view.ToolUseDecisionChanged += OnToolUseDecisionChanged;
        }

        public void AttachAgent(TornadoAgent agent)
        {
            _agent = agent;
            _conversation = agent.Client.Chat.CreateConversation(agent.Options);
        }

        public async Task LoadConversationFromFileAsync(string path)
        {
            if (_agent == null) throw new InvalidOperationException("Agent not attached");
            if (_conversation == null) _conversation = _agent.Client.Chat.CreateConversation(_agent.Options);

            var msgs = new System.Collections.Generic.List<ChatMessage>();
            await msgs.LoadMessagesAsync(path);
            _conversation.LoadConversation(msgs);
        }

        private async void OnSendRequested(object? sender, string prompt)
        {
            if (_agent == null)
            {
                MessageBox.Show("No agent attached.");
                return;
            }

            // streaming handler: push deltas into the UI
            ValueTask RunEventHandler(AgentRunnerEvents runEvent)
            {
                if (runEvent.EventType == AgentRunnerEventTypes.Streaming && runEvent is AgentRunnerStreamingEvent s)
                {
                    if (s.ModelStreamingEvent is ModelStreamingOutputTextDeltaEvent delta && !string.IsNullOrEmpty(delta.DeltaText))
                    {
                        // marshal to UI thread
                        if (_view.InvokeRequired)
                        {
                            _view.BeginInvoke(new Action<string>(_view.AppendAssistantStream), delta.DeltaText);
                        }
                        else
                        {
                            _view.AppendAssistantStream(delta.DeltaText);
                        }
                    }
                }
                return ValueTask.CompletedTask;
            }

            ValueTask<bool> UiToolApproval(string request)
            {
                var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                void Handler(object? _, ToolUseDecision d)
                {
                    if (d == ToolUseDecision.None) return;
                    _view.ToolUseDecisionChanged -= Handler;

                    if (_view.InvokeRequired)
                    {
                        _view.BeginInvoke(new Action(() => _view.SetStreamingToolDecisionVisible(false)));
                    }
                    else
                    {
                        _view.SetStreamingToolDecisionVisible(false);
                    }

                    tcs.TrySetResult(d == ToolUseDecision.Accept);
                }

                _view.ToolUseDecisionChanged += Handler;

                if (_view.InvokeRequired)
                {
                    _view.BeginInvoke(new Action(() => _view.SetStreamingToolDecisionVisible(true)));
                }
                else
                {
                    _view.SetStreamingToolDecisionVisible(true);
                }

                return new ValueTask<bool>(tcs.Task);
            }

            try
            {
                // Begin streaming message in UI
                if (_view.InvokeRequired) _view.BeginInvoke(new Action(_view.BeginAssistantStream));
                else _view.BeginAssistantStream();

                var toolApproval = ToolApprovalHandler ?? UiToolApproval;

                var conv = await _agent.Run(prompt,
                    appendMessages: _conversation?.Messages.ToList(),
                    streaming: true,
                    onAgentRunnerEvent: RunEventHandler,
                    toolPermissionHandle: toolApproval);

                _conversation = conv;

                string final = conv.Messages.Last().Content ?? string.Empty;

                // finalize stream with final content
                if (_view.InvokeRequired) _view.BeginInvoke(new Action<string?>(_view.EndAssistantStream), final);
                else _view.EndAssistantStream(final);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Agent Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                if (_view.InvokeRequired) _view.BeginInvoke(new Action<string?>(_view.EndAssistantStream), null);
                else _view.EndAssistantStream();
            }
        }

        private void OnToolUseDecisionChanged(object? sender, ToolUseDecision decision)
        {
            // Optional: integrate decision with your gating if you have your own policy
        }

        public async Task SaveConversationAsync(string path)
        {
            if (_conversation == null) return;
            _conversation.Messages.ToList().SaveConversation(path);
            await Task.CompletedTask;
        }
    }
}
