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

            chat.SendRequested += (s, prompt) =>
            {
                // Simulate assistant response
                chat.AppendAssistantMessage("**Received:**\n\n" + prompt + "\n\n```json\n{ \"status\": \"ok\" }\n```");
            };
            Application.Run(form);
        }
    }
}