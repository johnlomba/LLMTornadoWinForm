using LlmTornado;
using LlmTornado.Agents;
using LlmTornado.Code;

namespace TornadoViews
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            var form = new TornadoChatUI();
            var chat = new ChatWindowControl();
            form.Controls.Add(chat);

            // Create agent outside the control and attach
            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                ?? throw new InvalidOperationException("OPENAI_API_KEY environment variable is not set.");
            var api = new TornadoApi(new ProviderAuthentication(LLmProviders.OpenAi, apiKey));
            var agent = new TornadoAgent(api, LlmTornado.Chat.Models.ChatModel.OpenAi.Gpt41.V41Mini, instructions: "You are a helpful assistant.", streaming: true);
            var controller = new AgentChatController(chat);
            controller.AttachAgent(agent);

            Application.Run(form);
        }
    }
}