# Feature Comparison: WpfViews vs SmarterViews.Desktop Chat Features

## Summary
The **LlmTornado.WpfViews** project contains several advanced chat features that are **missing or not fully implemented** in the **SmarterViews.Desktop** application. Below is a detailed breakdown.

---

## Missing Features in SmarterViews.Desktop

### 1. **MCP Server Management** ?? CRITICAL
**Status:** Completely Missing

**WpfViews Implementation:**
- `McpServerManager.cs` - Full MCP server lifecycle management
- `McpServerConfig.cs` - Configuration model for MCP servers
- `McpServersViewModel.cs` - ViewModel for managing MCP servers
- `McpServersDialog.xaml.cs` - UI dialog for MCP server configuration
- Integration with `ChatService` to load and register MCP tools dynamically

**What it does:**
- Load and manage MCP (Model Context Protocol) servers
- Persist server configurations to JSON
- Initialize MCP servers and register their tools with the chat agent
- Provide UI for adding/removing/configuring MCP servers

**Impact:** SmarterViews cannot use MCP tools (external tool integrations like filesystem access, web browsing, etc.)

---

### 2. **Prompt Template System** ?? HIGH PRIORITY
**Status:** Completely Missing

**WpfViews Implementation:**
- `PromptTemplate.cs` - Model for system prompt templates
- `PromptTemplateService.cs` - Service for CRUD operations on templates
- `PromptTemplateViewModel.cs` - ViewModel for template management
- `PromptTemplateDialog.xaml.cs` - UI dialog for creating/editing templates
- Built-in + custom template support
- Template persistence to JSON

**What it does:**
- Create, edit, and delete custom system prompts
- Save and load prompt templates
- Switch between different system prompts at runtime
- Built-in default prompts included

**Impact:** SmarterViews users cannot save custom system prompts or quickly switch between different conversation styles

---

### 3. **Model Discovery Service** ?? MEDIUM PRIORITY
**Status:** Missing

**WpfViews Implementation:**
- `ModelDiscoveryService.cs` - Dynamically discovers available models from API providers
- Handles provider-specific model listing
- Estimates context token limits for models
- Formats model display names

**What it does:**
- Query providers (OpenAI, Claude, etc.) for available models
- Display available models to user dynamically
- Handle models from all configured providers
- Fallback handling if provider is unavailable

**Impact:** SmarterViews may have hardcoded model lists instead of discovering what's actually available from configured providers

---

### 4. **Conversation History Management** ?? MEDIUM PRIORITY
**Status:** Partially Present

**WpfViews has more complete implementation:**
- `ConversationStore.cs` in WpfViews has better serialization/deserialization
- `ChatViewModel` has dedicated conversation management with save/load
- Conversation list UI component (`ConversationList.xaml.cs`)

**SmarterViews Implementation:**
- Basic `ConversationStore.cs` exists but may lack features
- No dedicated conversation UI panel

**Impact:** SmarterViews may not properly persist/restore full conversation context

---

### 5. **Tool Call Approval Dialog UI** ?? MEDIUM PRIORITY
**Status:** Missing

**WpfViews Implementation:**
- `ToolApprovalDialog.xaml.cs` - Dedicated UI dialog for tool approval
- `ToolApprovalViewModel.cs` - ViewModel for tool approval display
- Shows tool name, arguments, and provides Approve/Deny buttons

**What it does:**
- Display tool calls to user with clear information
- Allow user to approve or deny tool execution
- Visual confirmation before tools run

**Impact:** SmarterViews may have inline or minimal tool approval UX

---

### 6. **Enhanced Chat ViewModel** ?? MEDIUM PRIORITY
**Status:** Partially Present

**WpfViews ChatViewModel features missing from SmarterViews:**
- `PendingAttachments` collection for staging files before sending
- `ClearAttachmentsCommand` for managing pending files
- `ToolCallHistory` collection - tracking all tool calls in session
- `SystemPromptContent` property - displays current system prompt
- Better separation of concerns for chat state management
- `ConversationSaved` event for synchronizing conversation list

**Impact:** SmarterViews has less polished attachment/file management and no tool call history

---

### 7. **Provider API Key ViewModel** ?? LOW PRIORITY
**Status:** Missing

**WpfViews Implementation:**
- `ProviderApiKeyViewModel.cs` - Dedicated ViewModel for provider configuration

**What it does:**
- Manage API keys for multiple providers in UI
- Validation of provider settings

---

## Code Quality Improvements in WpfViews

### 1. **Better Comments and Documentation**
```csharp
// WpfViews has detailed comments on event purposes
/// <summary>
/// Raised when a tool call requires approval BEFORE execution.
/// The ToolCallRequest.ApprovalTask should be completed with true (approve) or false (deny).
/// </summary>
public event Action<ToolCallRequest>? OnToolApprovalRequired;
```

### 2. **More Comprehensive ChatService**
WpfViews ChatService has:
- `SetMcpServerManager()` method for MCP integration
- More detailed comments in `HandleToolPermissionRequestAsync()`
- Better structured async handling

### 3. **Conversation Model Enhancements**
WpfViews has more complete conversation persistence capabilities

---

## Files to Copy/Integrate from WpfViews

To bring SmarterViews up to feature parity with WpfViews, you should:

### **Critical (Must Have):**
1. Copy and integrate `McpServerManager.cs`
2. Copy and integrate `McpServerConfig.cs` model
3. Copy `McpServersViewModel.cs` and `McpServersDialog.xaml`
4. Update `ChatService.cs` to support MCP tools via `SetMcpServerManager()`

### **High Priority:**
5. Copy `PromptTemplate.cs` model
6. Copy `PromptTemplateService.cs`
7. Copy `PromptTemplateViewModel.cs` and `PromptTemplateDialog.xaml`
8. Update settings to persist default template choice

### **Medium Priority:**
9. Copy `ModelDiscoveryService.cs`
10. Copy `ToolApprovalDialog.xaml.cs` and `ToolApprovalViewModel.cs`
11. Enhance `ChatViewModel` with:
    - `PendingAttachments` collection
    - `ToolCallHistory` collection
    - Attachment management commands
12. Copy `ConversationList.xaml.cs` for better conversation management

### **Low Priority:**
13. Copy `ProviderApiKeyViewModel.cs`
14. Add `RoleToColorConverter.cs` for UI consistency

---

## Integration Strategy

### Phase 1: MCP Support (Critical)
- Copy MCP-related files
- Update `ChatService` to accept `McpServerManager`
- Add MCP configuration UI

### Phase 2: Prompt Templates (High Priority)
- Copy template model and service
- Add template management UI
- Update chat initialization to use selected template

### Phase 3: Model Discovery (Medium Priority)
- Copy `ModelDiscoveryService`
- Update settings dialog to use dynamic model discovery
- Remove hardcoded model lists

### Phase 4: UI/UX Polish (Medium Priority)
- Add tool approval dialog
- Add conversation history UI
- Enhance attachment management

---

## Migration Checklist

- [ ] Analyze namespace differences (`LlmTornado.WpfViews` ? `SmarterViews.Desktop`)
- [ ] Copy MCP-related services and models
- [ ] Copy PromptTemplate service and models
- [ ] Update ChatService with MCP and prompt template support
- [ ] Copy UI components (dialogs, viewmodels)
- [ ] Update Settings model to include new configurations
- [ ] Test MCP server loading and tool registration
- [ ] Test prompt template persistence and switching
- [ ] Test model discovery from all providers
- [ ] Update main ViewModel to wire up new services
- [ ] Update settings persistence to handle new options

---

## Notes

- Both projects use **MVVM Community Toolkit** and **Newtonsoft.Json**
- Both target **.NET 10** for Windows
- WpfViews is the more "complete" reference implementation
- SmarterViews has domain-specific features (database connection, SQL generation) that WpfViews doesn't have
- The integration should be mostly copy-paste with namespace adjustments
