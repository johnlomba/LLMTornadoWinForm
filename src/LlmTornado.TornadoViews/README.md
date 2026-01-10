# TornadoViews - WinForm Chat Application

A Windows Forms chat application for LLM Tornado that demonstrates best practices for building responsive, user-friendly AI chat interfaces.

## Features

This application addresses four common UX issues in streaming chat applications:

### 1. ✅ Auto-Sizing Messages (Fixed: Internal Scrolling Issue)
- **Problem**: Fixed-size message containers with internal scrollbars make content hard to read
- **Solution**: Messages use `AutoSize` and `AutoSizeMode.GrowAndShrink` to expand naturally with content
- Messages adapt to their content without internal scrolling
- The parent `FlowLayoutPanel` handles overall scrolling for the entire chat history

**Implementation**: See `ChatMessageControl.cs`
- No fixed `Width` on RichTextBox
- `ScrollBars = RichTextBoxScrollBars.None`
- Dynamic height calculation based on content
- `ContentsResized` event handler for automatic adjustment

### 2. ✅ Efficient Streaming (Fixed: Re-rendering Performance Issue)
- **Problem**: Re-rendering entire markdown content on each streaming delta causes severe performance degradation
- **Solution**: Incremental text appending instead of full re-renders
- Each delta is appended directly to the RichTextBox
- Markdown formatting is applied only to new content

**Implementation**: See `ChatWindowControl.AppendAssistantStreamAsync()`
- Uses `AppendText()` for plain text streaming
- Uses `AppendMarkdown()` for formatted streaming
- Avoids calling `RenderToRichTextBox()` during streaming
- Full render only happens once at message completion (optional)

### 3. ✅ Non-Blocking UI (Fixed: UI Deadlock Issue)
- **Problem**: Synchronous or improperly handled async operations block the UI thread
- **Solution**: Proper async/await pattern with thread-safe UI updates
- All streaming operations use `async`/`await`
- UI updates are marshaled to the UI thread using `Invoke()`
- No synchronous blocking calls

**Implementation**: See `ChatWindowControl.SendMessageAsync()`
- `await foreach` for streaming responses
- `Task.Run()` with `Invoke()` for thread-safe UI updates
- Non-blocking message sending with fire-and-forget pattern
- Proper exception handling

### 4. ✅ Request Cancellation (Fixed: Missing Cancel Button)
- **Problem**: No way to stop long-running or stuck requests
- **Solution**: Cancel button with proper CancellationToken support
- Cancel button appears and is enabled during active requests
- Send button is disabled during requests to prevent duplicate sends
- Graceful cancellation with user feedback

**Implementation**: See `ChatWindowControl`
- `CancellationTokenSource` created for each request
- Cancel button wired to `CancellationTokenSource.Cancel()`
- Proper cleanup in finally blocks
- Cancelled message indication in chat

## Architecture

### Components

1. **ChatMarkdownRenderer** (`ChatMarkdownRenderer.cs`)
   - Renders markdown to RichTextBox with basic formatting
   - Supports headers (#, ##, ###)
   - Supports code blocks (```)
   - Supports inline code (`code`)
   - Supports bold text (**bold**)
   - Optimized for both full renders and incremental appends

2. **ChatMessageControl** (`ChatMessageControl.cs`)
   - Displays a single chat message
   - Auto-sizing based on content
   - No internal scrolling
   - Supports user and assistant roles
   - Different background colors for different roles

3. **ChatWindowControl** (`ChatWindowControl.cs`)
   - Main chat interface
   - Manages message history in FlowLayoutPanel
   - Handles streaming with proper async/await
   - Provides cancel functionality
   - Thread-safe UI updates

4. **Form1** (`Form1.cs`)
   - Main application window
   - API key configuration
   - Hosts ChatWindowControl

## Usage

### Running the Application

1. Build the project:
   ```bash
   dotnet build
   ```

2. Run the application:
   ```bash
   dotnet run
   ```

3. Enter your OpenAI API key in the text box at the top

4. Click "Connect" to initialize the API connection

5. Start chatting!

### During Chat

- **Send Message**: Type in the input box and click "Send" or press Ctrl+Enter
- **Cancel Request**: Click the "Cancel" button (appears red when a request is active)
- **Scroll History**: Scroll through the message panel to review previous messages

## Requirements

- .NET 10.0 or later
- Windows (for WinForms support)
- LLM Tornado library
- OpenAI API key (or other supported provider)

## Code Highlights

### Issue 1 Fix: Auto-Sizing Messages
```csharp
// ChatMessageControl.cs
_contentTextBox = new RichTextBox
{
    ScrollBars = RichTextBoxScrollBars.None, // No internal scrolling
    WordWrap = true
};

this.AutoSize = true;
this.AutoSizeMode = AutoSizeMode.GrowAndShrink;
```

### Issue 2 Fix: Efficient Streaming
```csharp
// ChatWindowControl.cs - Incremental append instead of full re-render
private Task AppendAssistantStreamAsync(string delta)
{
    _streamingAssistant.AppendText(delta); // Append only new content
    ScrollToBottom();
    return Task.CompletedTask;
}
```

### Issue 3 Fix: Non-Blocking UI
```csharp
// ChatWindowControl.cs - Proper async streaming
await foreach (var delta in _runtime.Chat.StreamChatEnumerableAsync(chatRequest, cancellationToken))
{
    if (delta.Choices != null && delta.Choices.Count > 0)
    {
        var content = delta.Choices[0].Delta?.Content;
        if (!string.IsNullOrEmpty(content))
        {
            await AppendAssistantStreamAsync(content);
        }
    }
}
```

### Issue 4 Fix: Cancellation Support
```csharp
// ChatWindowControl.cs - Cancel button
_cancelButton.Click += OnCancelButtonClick;

private void OnCancelButtonClick(object? sender, EventArgs e)
{
    _currentRequestCts?.Cancel();
}
```

## Performance Characteristics

- **Memory**: Efficient due to incremental text appending
- **CPU**: Minimal during streaming (no re-rendering of entire content)
- **UI Responsiveness**: Always responsive due to proper async handling
- **User Control**: Complete control with cancel button

## Future Enhancements

Possible improvements:
- Enhanced markdown rendering (lists, links, images)
- Syntax highlighting for code blocks
- Message editing and deletion
- Conversation history persistence
- Multiple conversation threads
- Export chat to file
- Customizable themes
- Keyboard shortcuts
- Voice input support

## License

This project is part of LLM Tornado and follows the same license.

## Contributing

Contributions are welcome! Please ensure any changes maintain the four core fixes:
1. Auto-sizing messages
2. Efficient streaming
3. Non-blocking UI
4. Request cancellation
