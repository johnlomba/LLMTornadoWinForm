# TornadoViews Quick Start Guide

## Overview

TornadoViews is a Windows Forms chat application for LLM Tornado that demonstrates proper implementation of streaming chat interfaces with optimal UX.

## Prerequisites

- Windows OS
- .NET 10.0 SDK or later
- Visual Studio 2022 (or any IDE with WinForms support)
- OpenAI API key (or key for another supported provider)

## Installation & Setup

### 1. Build the Project

```bash
cd src/LlmTornado.TornadoViews
dotnet restore
dotnet build
```

### 2. Run the Application

```bash
dotnet run
```

Or open in Visual Studio and press F5.

## First-Time Configuration

When you launch the application:

1. **Enter your API key** in the text box at the top
2. **Click "Connect"** to initialize the API
3. You'll see a welcome message confirming the connection
4. **Start chatting!**

## Using the Application

### Sending Messages

- Type your message in the input box at the bottom
- Click **"Send"** or press **Ctrl+Enter**
- The message will appear in the chat window
- The assistant's response will stream in real-time

### During Streaming

- **Cancel button**: Click the red "Cancel" button to stop the current request
- **UI remains responsive**: You can scroll through previous messages
- **Smooth streaming**: No lag or performance issues even with long responses

### Key Features Demonstrated

1. **Auto-sizing messages**: Scroll through the chat - each message adjusts to its content
2. **Efficient streaming**: Watch how text appears smoothly without flickering
3. **Responsive UI**: Try scrolling or interacting while streaming - everything works
4. **Cancel control**: Click cancel mid-stream to stop the request immediately

## Configuration Options

### Change the Model

Edit `ChatWindowControl.cs` line 171:

```csharp
Model = "gpt-4",  // Change to "gpt-3.5-turbo", "gpt-4-turbo", etc.
```

### Use a Different Provider

Edit `Form1.cs` line 100:

```csharp
var api = new TornadoApi(LLmProviders.OpenAi, apiKey);
// Change to LLmProviders.Anthropic, LLmProviders.Cohere, etc.
```

And update the model name accordingly.

### Customize Appearance

**Message colors** - Edit `ChatWindowControl.cs`:

```csharp
// User messages
var messageControl = new ChatMessageControl
{
    BackColor = Color.LightBlue  // Change this
};

// Assistant messages (default is white)
var messageControl = new ChatMessageControl
{
    BackColor = Color.LightGreen  // Add custom color
};
```

**Chat background** - Edit `ChatWindowControl.cs` line 45:

```csharp
BackColor = Color.WhiteSmoke  // Change this
```

## Troubleshooting

### "Runtime not initialized" error

Make sure you:
1. Entered a valid API key
2. Clicked the "Connect" button
3. Saw the success message

### Build errors on Linux

This is a Windows Forms application and requires Windows to build and run. The `EnableWindowsTargeting` property allows compilation on Linux, but the app cannot execute.

### Streaming is slow or choppy

Check your network connection. The streaming performance on the client side is optimized - any delays are typically due to:
- Network latency
- API provider response time
- Model processing time

### Cancel button not working

Make sure:
1. You're clicking it during an active request (when it's red and enabled)
2. The request uses a model that supports streaming
3. Your internet connection is stable

## Advanced Usage

### Conversation History

Currently, each message is sent independently. To implement conversation history:

1. Store all `ChatMessage` objects in a list
2. Pass the entire list to `ChatRequest.Messages`
3. Append new user/assistant messages to the list

Example modification in `ChatWindowControl.cs`:

```csharp
private List<ChatMessage> _conversationHistory = new List<ChatMessage>();

// In SendMessageAsync:
_conversationHistory.Add(new ChatMessage(ChatMessageRoles.User, message));

var chatRequest = new ChatRequest
{
    Model = "gpt-4",
    Messages = _conversationHistory,  // Use full history
    CancellationToken = cancellationToken
};

// After streaming:
_conversationHistory.Add(new ChatMessage(ChatMessageRoles.Assistant, _streamingBuffer.ToString()));
```

### System Prompts

Add a system message at the start:

```csharp
_conversationHistory.Insert(0, new ChatMessage(
    ChatMessageRoles.System, 
    "You are a helpful AI assistant."
));
```

### Custom Markdown Formatting

Extend `ChatMarkdownRenderer.cs` to support:
- Lists (ordered and unordered)
- Links
- Images
- Tables
- Custom formatting rules

## Performance Tips

1. **Long conversations**: Clear old messages periodically to avoid memory buildup
2. **Large responses**: The incremental streaming handles this efficiently
3. **Multiple concurrent chats**: Create separate `ChatWindowControl` instances

## Security Considerations

- **API keys**: Never hardcode API keys in source code
- **User input**: The app sends user input directly to the API - add validation if needed
- **Error messages**: Be careful about exposing sensitive error details to users

## Support

For issues or questions:
- Check the README.md for detailed documentation
- Review IMPLEMENTATION_SUMMARY.md for technical details
- Refer to LLM Tornado documentation: https://llmtornado.ai

## License

This project follows the LLM Tornado license (MIT).
