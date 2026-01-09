using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using LlmTornado.Agents;
using LlmTornado.Agents.ChatRuntime;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Agents.Utility;
using LlmTornado.Agents.DataModels;

namespace TornadoViews
{
    /// <summary>
    /// Controller to bridge a ChatWindowControl with a ChatRuntime lifecycle.
    /// </summary>
    /// <remarks>
    /// TODO: Add CancellationToken support - pass token to runtime.InvokeAsync() and add cancel button to UI
    /// TODO: Wire up RetryRequested event from ChatMessageControl to re-send failed messages
    /// TODO: Consider using ConversationPersistence class instead of extension methods for auto-save support
    /// TODO: Add error state display in chat UI instead of MessageBox
    /// TODO: Expose retry logic via OnRetryRequested event
    /// </remarks>
    public class AgentChatController
    {
        private readonly ChatWindowControl _view;
        private ChatRuntime? _runtime;

        public AgentChatController(ChatWindowControl view)
        {
            _view = view;
            _view.SendRequested += OnSendRequested;
            _view.ToolUseDecisionChanged += OnToolUseDecisionChanged;
        }

        public void AttachRuntime(ChatRuntime runtime)
        {
            _runtime = runtime;

            // Wire up runtime events for streaming
            _runtime.RuntimeConfiguration.OnRuntimeEvent = RuntimeEventHandler;

            // Wire up tool permission request handler
            _runtime.RuntimeConfiguration.OnRuntimeRequestEvent = UiToolApproval;
        }

        private ValueTask RuntimeEventHandler(ChatRuntimeEvents runtimeEvent)
        {
            // Handle agent runner events (streaming, tool invoked, etc.)
            if (runtimeEvent is ChatRuntimeAgentRunnerEvents agentEvent)
            {
                var runEvent = agentEvent.AgentRunnerEvent;

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
            }

            return ValueTask.CompletedTask;
        }

        private ValueTask<bool> UiToolApproval(string request)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            void Handler(object? _, ToolUseDecision d)
            {
                if (d == ToolUseDecision.None) return;
                _view.ToolUseDecisionChanged -= Handler;

                if (_view.InvokeRequired)
                {
                    _view.BeginInvoke(new Action(() =>
                    {
                        _view.SetStreamingToolDecisionVisible(false);
                        _view.SetStreamingToolRequest(string.Empty);
                    }));
                }
                else
                {
                    _view.SetStreamingToolDecisionVisible(false);
                    _view.SetStreamingToolRequest(string.Empty);
                }

                tcs.TrySetResult(d == ToolUseDecision.Accept);
            }

            _view.ToolUseDecisionChanged += Handler;

            if (_view.InvokeRequired)
            {
                _view.BeginInvoke(new Action(() =>
                {
                    _view.SetStreamingToolRequest(request);
                    _view.SetStreamingToolDecisionVisible(true);
                }));
            }
            else
            {
                _view.SetStreamingToolRequest(request);
                _view.SetStreamingToolDecisionVisible(true);
            }

            return new ValueTask<bool>(tcs.Task);
        }

        public async Task LoadConversationFromFileAsync(string path)
        {
            if (_runtime == null) throw new InvalidOperationException("Runtime not attached");

            var msgs = new System.Collections.Generic.List<ChatMessage>();
            await msgs.LoadMessagesAsync(path);

            // Clear current messages and load from file
            _runtime.RuntimeConfiguration.ClearMessages();
            foreach (var msg in msgs)
            {
                // This will add messages to the runtime's conversation
                // Note: You may need to add a LoadMessages method to IRuntimeConfiguration
                // For now, we'll just display them in the UI
            }
        }

        private async void OnSendRequested(object? sender, string prompt)
        {
            if (_runtime == null)
            {
                MessageBox.Show("No runtime attached.");
                return;
            }

            try
            {
                // Begin streaming message in UI
                if (_view.InvokeRequired) _view.BeginInvoke(new Action(_view.BeginAssistantStream));
                else _view.BeginAssistantStream();

                // Create a user message and invoke the runtime
                var userMessage = new ChatMessage(LlmTornado.Code.ChatMessageRoles.User, prompt);
                var response = await _runtime.InvokeAsync(userMessage);

                string final = response.Content ?? string.Empty;

                // finalize stream with final content
                if (_view.InvokeRequired) _view.BeginInvoke(new Action<string?>(_view.EndAssistantStream), final);
                else _view.EndAssistantStream(final);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Runtime Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            if (_runtime == null) return;
            var messages = _runtime.RuntimeConfiguration.GetMessages();
            messages.SaveConversation(path);
            await Task.CompletedTask;
        }
    }
}
