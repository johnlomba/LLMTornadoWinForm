# TornadoViews Architecture Diagram

## Component Hierarchy

```
Form1 (Main Window)
├── Setup Panel
│   ├── API Key TextBox
│   ├── Connect Button
│   └── Instruction Label
│
└── ChatWindowControl (Main Chat Interface)
    ├── Messages Panel (FlowLayoutPanel with AutoScroll)
    │   ├── ChatMessageControl (User Message)
    │   │   ├── Role Label ("User")
    │   │   └── Content RichTextBox (AutoSize, No Scrollbars)
    │   │
    │   ├── ChatMessageControl (Assistant Message)
    │   │   ├── Role Label ("Assistant")
    │   │   └── Content RichTextBox (AutoSize, No Scrollbars)
    │   │
    │   └── ChatMessageControl (Streaming Message)
    │       ├── Role Label ("Assistant")
    │       └── Content RichTextBox (Dynamic Height Updates)
    │
    └── Input Panel
        ├── Input TextBox (Multiline)
        ├── Cancel Button (Red, enabled during requests)
        └── Send Button (Disabled during requests)
```

## Data Flow - Sending a Message

```
User Input
    ↓
Send Button Click
    ↓
ChatWindowControl.SendMessageAsync()
    ↓
Create ChatRequest with CancellationToken
    ↓
TornadoApi.Chat.StreamChatEnumerable()
    ↓
┌─────────── Streaming Loop ──────────┐
│                                     │
│  Receive Delta                      │
│      ↓                              │
│  Append to Buffer                   │
│      ↓                              │
│  AppendAssistantStreamAsync()       │
│      ↓                              │
│  Check InvokeRequired               │
│      ↓                              │
│  Invoke() → UI Thread               │
│      ↓                              │
│  ChatMessageControl.AppendText()    │
│      ↓                              │
│  Trigger ContentsResized            │
│      ↓                              │
│  Adjust Height Dynamically          │
│      ↓                              │
│  Back to Streaming Loop             │
│                                     │
└─────────────────────────────────────┘
    ↓
Streaming Complete
    ↓
Cleanup (Dispose CancellationTokenSource)
```

## Issue 1 Fix: Auto-sizing Flow

```
ChatMessageControl Created
    ↓
Content Added to RichTextBox
    ↓
ContentsResized Event Fires
    ↓
Calculate Height: e.NewRectangle.Height
    ↓
Update RichTextBox.Height
    ↓
Update ContentPanel.Height
    ↓
Update ChatMessageControl.Height
    ↓
FlowLayoutPanel Reflows
    ↓
AutoScroll Adjusts (if needed)

Result: Message grows to fit content,
        no internal scrollbars needed
```

## Issue 2 Fix: Streaming Optimization

```
Traditional (BAD):
├── Receive Delta → Append to Buffer
├── Set MarkdownText = Buffer
├── Clear RichTextBox
├── Re-render ALL content with formatting
└── Repeat for EVERY delta
    → Performance degrades exponentially

Our Solution (GOOD):
├── Receive Delta
├── Append ONLY Delta to RichTextBox
└── Repeat for EVERY delta
    → Constant-time performance
```

## Issue 3 Fix: Thread Safety

```
Streaming on Background Thread
    ↓
Need to Update UI
    ↓
Check: InvokeRequired?
    ├── YES → Task.Run(() => Invoke(() => Update UI))
    │          │
    │          └→ Marshals to UI Thread
    │             └→ Safe Update
    │
    └── NO  → Direct Update (already on UI thread)

Result: No blocking, no cross-thread exceptions
```

## Issue 4 Fix: Cancellation Flow

```
User Clicks Send
    ↓
Create CancellationTokenSource
    ↓
Pass CancellationToken to ChatRequest
    ↓
Enable Cancel Button (Red)
    ↓
Disable Send Button
    ↓
┌─── Streaming Active ────┐
│                          │
│  User Clicks Cancel      │
│      ↓                   │
│  CancellationTokenSource │
│      .Cancel()           │
│      ↓                   │
│  OperationCanceled       │
│      Exception           │
│      ↓                   │
│  Caught & Handled        │
│      ↓                   │
│  Add "[Cancelled]" Text  │
│                          │
└──────────────────────────┘
    ↓
Finally Block
    ↓
Dispose CancellationTokenSource
    ↓
Enable Send Button
    ↓
Disable Cancel Button
```

## Class Responsibilities

### ChatMarkdownRenderer (Static Utility)
**Purpose:** Render markdown to RichTextBox
**Key Methods:**
- `RenderToRichTextBox()` - Full render (initial display)
- `AppendMarkdownToRichTextBox()` - Incremental render (streaming)

**Supports:**
- Headers (# ## ###)
- Code blocks (```)
- Inline code (`code`)
- Bold (**text**)

### ChatMessageControl (User Control)
**Purpose:** Display a single message with auto-sizing
**Key Features:**
- Auto-sizing based on content
- No internal scrollbars
- Dynamic height adjustment
- Role-based styling

**Key Methods:**
- `AppendText()` - Add plain text incrementally
- `AppendMarkdown()` - Add formatted text incrementally
- `AdjustHeight()` - Recalculate control height

### ChatWindowControl (User Control)
**Purpose:** Main chat interface with streaming support
**Key Features:**
- Message history management
- Streaming with cancellation
- Thread-safe UI updates
- Async/await pattern

**Key Methods:**
- `SendMessageAsync()` - Handle message sending
- `AppendAssistantStreamAsync()` - Thread-safe streaming
- `SetControlsState()` - Enable/disable UI controls

### Form1 (Form)
**Purpose:** Application window and API configuration
**Key Features:**
- API key input
- Connection management
- Host ChatWindowControl

## Memory Management

```
Per Request:
├── CancellationTokenSource (created)
├── StringBuilder for streaming buffer
├── ChatMessageControl for streaming display
└── Cleanup in finally block:
    ├── Dispose CancellationTokenSource
    ├── Clear streaming assistant reference
    └── Clear streaming buffer

Per Message:
├── ChatMessageControl added to FlowLayoutPanel
└── Managed by .NET WinForms lifecycle
```

## Performance Characteristics

| Aspect | Traditional | Our Implementation |
|--------|-------------|-------------------|
| Streaming delta processing | O(n²) - re-render all | O(1) - append only |
| UI thread blocking | Frequent | Never |
| Memory during streaming | High (multiple copies) | Low (single buffer) |
| User responsiveness | Poor (blocked) | Excellent (async) |
| Cancellation | Not available | Immediate |

## Key Design Principles

1. **Separation of Concerns**
   - Markdown rendering isolated in utility class
   - Message display in dedicated control
   - Chat logic in main control

2. **Performance First**
   - Incremental updates, not full re-renders
   - Suspend/Resume layout for batch updates
   - StringBuilder for efficient string building

3. **Thread Safety**
   - Always check InvokeRequired
   - Marshal UI updates to UI thread
   - No blocking synchronous calls

4. **Resource Management**
   - Dispose pattern for CancellationTokenSource
   - Finally blocks for cleanup
   - Clear references when done

5. **User Experience**
   - Visual feedback (button states, colors)
   - Immediate cancellation
   - Smooth, responsive UI
   - Auto-scrolling to latest message
