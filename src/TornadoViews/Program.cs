using LlmTornado;
using LlmTornado.Agents;
using LlmTornado.Chat.Models;
using LlmTornado.Code;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.ComponentModel;

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

            TornadoAgent agent = new TornadoAgent(
             api,
             ChatModel.OpenAi.Gpt41.V41Mini,
             instructions: "You are a useful assistant.",
             tools: [GetCurrentWeather],
             streaming: true,
             toolPermissionRequired: new Dictionary<string, bool>()
                 {
                    { "GetCurrentWeather", true }
                 }
             );

            var controller = new AgentChatController(chat);
            controller.AttachAgent(agent);

            Application.Run(form);
        }

        [JsonConverter(typeof(StringEnumConverter))]
        public enum Unit
        {
            Celsius,
            Fahrenheit
        }

        [Description("Get the current weather in a given location")]
        public static string GetCurrentWeather(
       [Description("The city and state, e.g. Boston, MA")] string location,
       [Description("unit of temperature measurement in C or F")] Unit unit = Unit.Celsius)
        {
            // Call the weather API here.
            return $"31 C";
        }

    }
}