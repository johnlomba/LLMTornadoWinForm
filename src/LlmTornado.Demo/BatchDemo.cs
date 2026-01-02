using LlmTornado.Batch;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Code;
using LlmTornado.Common;

namespace LlmTornado.Demo;

public class BatchDemo : DemoBase
{
    /// <summary>
    /// Creates a simple batch with two requests using OpenAI.
    /// </summary>
    [TornadoTest]
    [Flaky("Batch processing takes time")]
    public static async Task CreateBatchOpenAi()
    {
        TornadoApi api = Program.Connect();
        
        BatchRequest request = new BatchRequest
        {
            Requests =
            [
                new BatchRequestItem("request-1", new ChatRequest
                {
                    Model = ChatModel.OpenAi.Gpt41.V41Mini,
                    Messages = [new ChatMessage(ChatMessageRoles.User, "What is 2+2?")]
                }),
                new BatchRequestItem("request-2", new ChatRequest
                {
                    Model = ChatModel.OpenAi.Gpt41.V41Mini,
                    Messages = [new ChatMessage(ChatMessageRoles.User, "What is the capital of France?")]
                })
            ],
            CompletionWindow = BatchCompletionWindow.Hours24
        };
        
        HttpCallResult<BatchItem> result = await api.Batch.Create(request, LLmProviders.OpenAi);
        
        if (result.Ok && result.Data is not null)
        {
            Console.WriteLine($"Batch created: {result.Data.Id}");
            Console.WriteLine($"Status: {result.Data.Status}");
            Console.WriteLine($"Total requests: {result.Data.RequestCounts?.Total}");
        }
        else
        {
            Console.WriteLine($"Failed to create batch: {result.Exception?.Message}");
        }
    }
    
    /// <summary>
    /// Creates a simple batch with two requests using Anthropic.
    /// </summary>
    [TornadoTest]
    [Flaky("Batch processing takes time")]
    public static async Task CreateBatchAnthropic()
    {
        TornadoApi api = Program.Connect();
        
        BatchRequest request = new BatchRequest
        {
            Requests =
            [
                new BatchRequestItem("request-1", new ChatRequest
                {
                    Model = ChatModel.Anthropic.Claude4.Sonnet250514,
                    MaxTokens = 100,
                    Messages = [new ChatMessage(ChatMessageRoles.User, "What is 2+2?")]
                }),
                new BatchRequestItem("request-2", new ChatRequest
                {
                    Model = ChatModel.Anthropic.Claude4.Sonnet250514,
                    MaxTokens = 100,
                    Messages = [new ChatMessage(ChatMessageRoles.User, "What is the capital of France?")]
                })
            ]
        };
        
        HttpCallResult<BatchItem> result = await api.Batch.Create(request, LLmProviders.Anthropic);
        
        if (result.Ok && result.Data is not null)
        {
            Console.WriteLine($"Batch created: {result.Data.Id}");
            Console.WriteLine($"Status: {result.Data.Status}");
            Console.WriteLine($"Total requests: {result.Data.RequestCounts?.Total}");
        }
        else
        {
            Console.WriteLine($"Failed to create batch: {result.Exception?.Message}");
        }
    }
    
    /// <summary>
    /// Creates a simple batch with two requests using Google/Gemini.
    /// </summary>
    [TornadoTest]
    [Flaky("Batch processing takes time")]
    public static async Task CreateBatchGoogle()
    {
        TornadoApi api = Program.Connect();
        
        BatchRequest request = new BatchRequest
        {
            Requests =
            [
                new BatchRequestItem("request-1", new ChatRequest
                {
                    Model = ChatModel.Google.Gemini.Gemini25Flash,
                    Messages = [new ChatMessage(ChatMessageRoles.User, "What is 2+2?")]
                }),
                new BatchRequestItem("request-2", new ChatRequest
                {
                    Model = ChatModel.Google.Gemini.Gemini25Flash,
                    Messages = [new ChatMessage(ChatMessageRoles.User, "What is the capital of France?")]
                })
            ],
            VendorExtensions = new BatchRequestVendorExtensions
            {
                Google = new BatchRequestVendorGoogleExtensions
                {
                    DisplayName = "demo-batch"
                }
            }
        };
        
        HttpCallResult<BatchItem> result = await api.Batch.Create(request, LLmProviders.Google);
        
        if (result.Ok && result.Data is not null)
        {
            Console.WriteLine($"Batch created: {result.Data.Id}");
            Console.WriteLine($"Display name: {result.Data.DisplayName}");
            Console.WriteLine($"Status: {result.Data.Status}");
            Console.WriteLine($"Total requests: {result.Data.RequestCounts?.Total}");
        }
        else
        {
            Console.WriteLine($"Failed to create batch: {result.Exception?.Message}");
        }
    }
    
    /// <summary>
    /// Lists all batches.
    /// </summary>
    [TornadoTest]
    public static async Task ListBatchesOpenAi()
    {
        TornadoApi api = Program.Connect();
        
        HttpCallResult<ListResponse<BatchItem>> result = await api.Batch.List(new ListQuery { Limit = 10 }, LLmProviders.OpenAi);
        
        if (result.Ok && result.Data is not null)
        {
            Console.WriteLine($"Found {result.Data.Items.Count} batches:");
            
            foreach (BatchItem batch in result.Data.Items)
            {
                Console.WriteLine($"  - {batch.Id}: {batch.Status} (Completed: {batch.RequestCounts?.Completed}/{batch.RequestCounts?.Total})");
            }
        }
        else
        {
            Console.WriteLine($"Failed to list batches: {result.Exception?.Message}");
        }
    }
    
    /// <summary>
    /// Lists all batches from Anthropic.
    /// </summary>
    [TornadoTest]
    public static async Task ListBatchesAnthropic()
    {
        TornadoApi api = Program.Connect();
        
        HttpCallResult<ListResponse<BatchItem>> result = await api.Batch.List(new ListQuery { Limit = 10 }, LLmProviders.Anthropic);
        
        if (result.Ok && result.Data is not null)
        {
            Console.WriteLine($"Found {result.Data.Items.Count} batches:");
            
            foreach (BatchItem batch in result.Data.Items)
            {
                Console.WriteLine($"  - {batch.Id}: {batch.Status} (Completed: {batch.RequestCounts?.Completed}/{batch.RequestCounts?.Total})");
            }
        }
        else
        {
            Console.WriteLine($"Failed to list batches: {result.Exception?.Message}");
        }
    }
    
    /// <summary>
    /// Lists all batches from Google/Gemini.
    /// </summary>
    [TornadoTest]
    public static async Task ListBatchesGoogle()
    {
        TornadoApi api = Program.Connect();
        
        HttpCallResult<ListResponse<BatchItem>> result = await api.Batch.List(new ListQuery { Limit = 10 }, LLmProviders.Google);
        
        if (result.Ok && result.Data is not null)
        {
            Console.WriteLine($"Found {result.Data.Items.Count} batches:");
            
            foreach (BatchItem batch in result.Data.Items)
            {
                Console.WriteLine($"  - {batch.Id}: {batch.Status} (Display: {batch.DisplayName}, Completed: {batch.RequestCounts?.Completed}/{batch.RequestCounts?.Total})");
            }
        }
        else
        {
            Console.WriteLine($"Failed to list batches: {result.Exception?.Message}");
        }
    }
    
    /// <summary>
    /// Gets a specific batch by ID.
    /// </summary>
    [TornadoTest]
    [Flaky("Requires existing batch ID")]
    public static async Task GetBatchOpenAi()
    {
        TornadoApi api = Program.Connect();
        
        // First, list batches to get an ID
        HttpCallResult<ListResponse<BatchItem>> listResult = await api.Batch.List(new ListQuery { Limit = 1 }, LLmProviders.OpenAi);
        
        if (listResult.Data?.Items.Count > 0)
        {
            string batchId = listResult.Data.Items[0].Id;
            
            HttpCallResult<BatchItem> result = await api.Batch.Get(batchId, LLmProviders.OpenAi);
            
            if (result.Ok && result.Data is not null)
            {
                Console.WriteLine($"Batch: {result.Data.Id}");
                Console.WriteLine($"Status: {result.Data.Status}");
                Console.WriteLine($"Created: {result.Data.CreatedAt}");
                Console.WriteLine($"Expires: {result.Data.ExpiresAt}");
                Console.WriteLine($"Request counts:");
                Console.WriteLine($"  Total: {result.Data.RequestCounts?.Total}");
                Console.WriteLine($"  Completed: {result.Data.RequestCounts?.Completed}");
                Console.WriteLine($"  Failed: {result.Data.RequestCounts?.Failed}");
            }
            else
            {
                Console.WriteLine($"Failed to get batch: {result.Exception?.Message}");
            }
        }
        else
        {
            Console.WriteLine("No batches found to retrieve.");
        }
    }
    
    /// <summary>
    /// Gets a specific batch by ID from Google/Gemini.
    /// </summary>
    [TornadoTest]
    [Flaky("Requires existing batch ID")]
    public static async Task GetBatchGoogle()
    {
        TornadoApi api = Program.Connect();
        
        // First, list batches to get an ID
        HttpCallResult<ListResponse<BatchItem>> listResult = await api.Batch.List(new ListQuery { Limit = 1 }, LLmProviders.Google);
        
        if (listResult.Data?.Items.Count > 0)
        {
            string batchId = listResult.Data.Items[0].Id;
            
            HttpCallResult<BatchItem> result = await api.Batch.Get(batchId, LLmProviders.Google);
            
            if (result.Ok && result.Data is not null)
            {
                Console.WriteLine($"Batch: {result.Data.Id}");
                Console.WriteLine($"Display name: {result.Data.DisplayName}");
                Console.WriteLine($"Status: {result.Data.Status}");
                Console.WriteLine($"Model: {result.Data.Model}");
                Console.WriteLine($"Created: {result.Data.CreatedAt}");
                Console.WriteLine($"Request counts:");
                Console.WriteLine($"  Total: {result.Data.RequestCounts?.Total}");
                Console.WriteLine($"  Completed: {result.Data.RequestCounts?.Completed}");
                Console.WriteLine($"  Failed: {result.Data.RequestCounts?.Failed}");
            }
            else
            {
                Console.WriteLine($"Failed to get batch: {result.Exception?.Message}");
            }
        }
        else
        {
            Console.WriteLine("No batches found to retrieve.");
        }
    }
    
    /// <summary>
    /// Gets results from a completed batch.
    /// </summary>
    [TornadoTest]
    [Flaky("Requires completed batch")]
    public static async Task GetBatchResultsOpenAi()
    {
        TornadoApi api = Program.Connect();
        
        // Find a completed batch
        HttpCallResult<ListResponse<BatchItem>> listResult = await api.Batch.List(new ListQuery { Limit = 20 }, LLmProviders.OpenAi);
        
        BatchItem? completedBatch = listResult.Data?.Items.FirstOrDefault(b => b.Status == BatchStatus.Completed);
        
        if (completedBatch is not null)
        {
            Console.WriteLine($"Getting results for batch: {completedBatch.Id}");
            
            List<BatchResult> results = await api.Batch.GetResults(completedBatch, LLmProviders.OpenAi);
            
            foreach (BatchResult result in results)
            {
                Console.WriteLine($"Result for {result.CustomId}:");
                Console.WriteLine($"  Type: {result.ResultType}");
                
                if (result.ChatResult is not null)
                {
                    Console.WriteLine($"  Response: {result.ChatResult.Choices?.FirstOrDefault()?.Message?.Content}");
                }
                else if (result.Error is not null)
                {
                    Console.WriteLine($"  Error: {result.Error.Message}");
                }
            }
        }
        else
        {
            Console.WriteLine("No completed batches found.");
        }
    }
    
    /// <summary>
    /// Gets results from a completed batch using streaming.
    /// </summary>
    [TornadoTest]
    [Flaky("Requires completed batch")]
    public static async Task GetBatchResultsStreamingAnthropic()
    {
        TornadoApi api = Program.Connect();
        
        // Find a completed batch
        HttpCallResult<ListResponse<BatchItem>> listResult = await api.Batch.List(new ListQuery { Limit = 20 }, LLmProviders.Anthropic);
        
        BatchItem? completedBatch = listResult.Data?.Items.FirstOrDefault(b => b.Status == BatchStatus.Ended);
        
        if (completedBatch is not null)
        {
            Console.WriteLine($"Getting results for batch: {completedBatch.Id}");
            
            await foreach (BatchResult result in api.Batch.GetResultsStreaming(completedBatch, LLmProviders.Anthropic))
            {
                Console.WriteLine($"Result for {result.CustomId}:");
                Console.WriteLine($"  Type: {result.ResultType}");
                
                if (result.ChatResult is not null)
                {
                    Console.WriteLine($"  Response: {result.ChatResult}");
                }
            }
        }
        else
        {
            Console.WriteLine("No completed batches found. Completed batches have status 'ended' in Anthropic.");
        }
    }
    
    /// <summary>
    /// Gets results from a completed Google/Gemini batch.
    /// </summary>
    [TornadoTest]
    [Flaky("Requires completed batch")]
    public static async Task GetBatchResultsGoogle()
    {
        TornadoApi api = Program.Connect();
        
        // Find a completed batch
        HttpCallResult<ListResponse<BatchItem>> listResult = await api.Batch.List(new ListQuery { Limit = 20 }, LLmProviders.Google);
        
        BatchItem? completedBatch = listResult.Data?.Items.FirstOrDefault(b => b.Status == BatchStatus.Completed);
        
        if (completedBatch is not null)
        {
            Console.WriteLine($"Getting results for batch: {completedBatch.Id}");
            Console.WriteLine($"Display name: {completedBatch.DisplayName}");
            
            List<BatchResult> results = await api.Batch.GetResults(completedBatch, LLmProviders.Google);
            
            foreach (BatchResult result in results)
            {
                Console.WriteLine($"Result for {result.CustomId}:");
                Console.WriteLine($"  Type: {result.ResultType}");
                
                if (result.ChatResult is not null)
                {
                    Console.WriteLine($"  Response: {result.ChatResult.Choices?.FirstOrDefault()?.Message?.Content}");
                }
                else if (result.Error is not null)
                {
                    Console.WriteLine($"  Error: {result.Error.Message}");
                }
            }
        }
        else
        {
            Console.WriteLine("No completed batches found.");
        }
    }
    
    /// <summary>
    /// Creates a batch and waits for it to complete (full workflow).
    /// </summary>
    [TornadoTest]
    [Flaky("Long running - batch processing can take hours")]
    public static async Task FullWorkflowOpenAi()
    {
        TornadoApi api = Program.Connect();
        
        Console.WriteLine("Creating batch...");
        
        BatchRequest request = new BatchRequest
        {
            Requests =
            [
                new BatchRequestItem("math-problem", new ChatRequest
                {
                    Model = ChatModel.OpenAi.Gpt41.V41Mini,
                    Messages = [new ChatMessage(ChatMessageRoles.User, "What is 15 * 7?")]
                }),
                new BatchRequestItem("geography", new ChatRequest
                {
                    Model = ChatModel.OpenAi.Gpt41.V41Mini,
                    Messages = [new ChatMessage(ChatMessageRoles.User, "What are the 5 largest countries by area?")]
                }),
                new BatchRequestItem("science", new ChatRequest
                {
                    Model = ChatModel.OpenAi.Gpt41.V41Mini,
                    Messages = [new ChatMessage(ChatMessageRoles.User, "Explain photosynthesis briefly.")]
                })
            ],
            VendorExtensions = new BatchRequestVendorExtensions
            {
                OpenAi = new BatchRequestVendorOpenAiExtensions
                {
                    Metadata = new Dictionary<string, string>
                    {
                        ["purpose"] = "demo",
                        ["created_by"] = "llmtornado"
                    }
                }
            }
        };
        
        HttpCallResult<BatchItem> createResult = await api.Batch.Create(request, LLmProviders.OpenAi);
        
        if (!createResult.Ok || createResult.Data is null)
        {
            Console.WriteLine($"Failed to create batch: {createResult.Exception?.Message}");
            return;
        }
        
        Console.WriteLine($"Batch created: {createResult.Data.Id}");
        Console.WriteLine($"Initial status: {createResult.Data.Status}");
        Console.WriteLine($"Waiting for completion (this may take a while)...");
        
        // Wait for completion with a short timeout for demo purposes
        HttpCallResult<BatchItem> completedResult = await api.Batch.WaitForCompletion(
            createResult.Data.Id,
            pollingIntervalMs: 10000, // Check every 10 seconds
            maxWaitMs: 300000, // Wait up to 5 minutes for demo
            provider: LLmProviders.OpenAi
        );
        
        if (completedResult.Data is null)
        {
            Console.WriteLine("Failed to get batch status.");
            return;
        }
        
        Console.WriteLine($"Final status: {completedResult.Data.Status}");
        Console.WriteLine($"Completed: {completedResult.Data.RequestCounts?.Completed}/{completedResult.Data.RequestCounts?.Total}");
        
        if (completedResult.Data.Status == BatchStatus.Completed)
        {
            Console.WriteLine("\nResults:");
            
            List<BatchResult> results = await api.Batch.GetResults(completedResult.Data, LLmProviders.OpenAi);
            
            foreach (BatchResult result in results)
            {
                Console.WriteLine($"\n--- {result.CustomId} ---");
                Console.WriteLine(result.ChatResult?.Choices?.FirstOrDefault()?.Message?.Content);
            }
        }
        else
        {
            Console.WriteLine("Batch did not complete within the timeout. Check back later.");
        }
    }
    
    /// <summary>
    /// Cancels a batch that is in progress.
    /// </summary>
    [TornadoTest]
    [Flaky("Requires in-progress batch")]
    public static async Task CancelBatchOpenAi()
    {
        TornadoApi api = Program.Connect();
        
        // Find an in-progress batch
        HttpCallResult<ListResponse<BatchItem>> listResult = await api.Batch.List(new ListQuery { Limit = 20 }, LLmProviders.OpenAi);
        
        BatchItem? inProgressBatch = listResult.Data?.Items.FirstOrDefault(b => 
            b.Status == BatchStatus.InProgress || b.Status == BatchStatus.Validating);
        
        if (inProgressBatch is not null)
        {
            Console.WriteLine($"Cancelling batch: {inProgressBatch.Id}");
            
            HttpCallResult<BatchItem> result = await api.Batch.Cancel(inProgressBatch.Id, LLmProviders.OpenAi);
            
            if (result.Ok && result.Data is not null)
            {
                Console.WriteLine($"Batch status after cancel: {result.Data.Status}");
            }
            else
            {
                Console.WriteLine($"Failed to cancel batch: {result.Exception?.Message}");
            }
        }
        else
        {
            Console.WriteLine("No in-progress batches found to cancel.");
        }
    }
    
    /// <summary>
    /// Creates a batch with Google/Gemini and waits for it to complete (full workflow).
    /// </summary>
    [TornadoTest]
    [Flaky("Long running - batch processing can take time")]
    public static async Task FullWorkflowGoogle()
    {
        TornadoApi api = Program.Connect();
        
        Console.WriteLine("Creating Google/Gemini batch...");
        
        BatchRequest request = new BatchRequest
        {
            Requests =
            [
                new BatchRequestItem("math-problem", new ChatRequest
                {
                    Model = ChatModel.Google.Gemini.Gemini25Flash,
                    Messages = [new ChatMessage(ChatMessageRoles.User, "What is 15 * 7?")]
                }),
                new BatchRequestItem("geography", new ChatRequest
                {
                    Model = ChatModel.Google.Gemini.Gemini25Flash,
                    Messages = [new ChatMessage(ChatMessageRoles.User, "What are the 5 largest countries by area?")]
                }),
                new BatchRequestItem("science", new ChatRequest
                {
                    Model = ChatModel.Google.Gemini.Gemini25Flash,
                    Messages = [new ChatMessage(ChatMessageRoles.User, "Explain photosynthesis briefly.")]
                })
            ],
            VendorExtensions = new BatchRequestVendorExtensions
            {
                Google = new BatchRequestVendorGoogleExtensions
                {
                    DisplayName = "demo-full-workflow",
                    Priority = 10
                }
            }
        };
        
        HttpCallResult<BatchItem> createResult = await api.Batch.Create(request, LLmProviders.Google);
        
        if (!createResult.Ok || createResult.Data is null)
        {
            Console.WriteLine($"Failed to create batch: {createResult.Exception?.Message}");
            return;
        }
        
        Console.WriteLine($"Batch created: {createResult.Data.Id}");
        Console.WriteLine($"Display name: {createResult.Data.DisplayName}");
        Console.WriteLine($"Initial status: {createResult.Data.Status}");
        Console.WriteLine($"Waiting for completion (this may take a while)...");
        
        // Wait for completion with a short timeout for demo purposes
        HttpCallResult<BatchItem> completedResult = await api.Batch.WaitForCompletion(
            createResult.Data.Id,
            pollingIntervalMs: 10000, // Check every 10 seconds
            maxWaitMs: 300000, // Wait up to 5 minutes for demo
            provider: LLmProviders.Google
        );
        
        if (completedResult.Data is null)
        {
            Console.WriteLine("Failed to get batch status.");
            return;
        }
        
        Console.WriteLine($"Final status: {completedResult.Data.Status}");
        Console.WriteLine($"Completed: {completedResult.Data.RequestCounts?.Completed}/{completedResult.Data.RequestCounts?.Total}");
        
        if (completedResult.Data.Status == BatchStatus.Completed)
        {
            Console.WriteLine("\nResults:");
            
            List<BatchResult> results = await api.Batch.GetResults(completedResult.Data, LLmProviders.Google);
            
            foreach (BatchResult result in results)
            {
                Console.WriteLine($"\n--- {result.CustomId} ---");
                Console.WriteLine(result.ChatResult?.Choices?.FirstOrDefault()?.Message?.Content);
            }
        }
        else
        {
            Console.WriteLine("Batch did not complete within the timeout. Check back later.");
        }
    }
    
    /// <summary>
    /// Cancels a Google/Gemini batch that is in progress.
    /// </summary>
    [TornadoTest]
    [Flaky("Requires in-progress batch")]
    public static async Task CancelBatchGoogle()
    {
        TornadoApi api = Program.Connect();
        
        // Find an in-progress batch
        HttpCallResult<ListResponse<BatchItem>> listResult = await api.Batch.List(new ListQuery { Limit = 20 }, LLmProviders.Google);
        
        BatchItem? inProgressBatch = listResult.Data?.Items.FirstOrDefault(b => 
            b.Status == BatchStatus.InProgress || b.Status == BatchStatus.Validating);
        
        if (inProgressBatch is not null)
        {
            Console.WriteLine($"Cancelling batch: {inProgressBatch.Id}");
            Console.WriteLine($"Display name: {inProgressBatch.DisplayName}");
            
            HttpCallResult<BatchItem> result = await api.Batch.Cancel(inProgressBatch.Id, LLmProviders.Google);
            
            if (result.Ok && result.Data is not null)
            {
                Console.WriteLine($"Batch status after cancel: {result.Data.Status}");
            }
            else
            {
                Console.WriteLine($"Failed to cancel batch: {result.Exception?.Message}");
            }
        }
        else
        {
            Console.WriteLine("No in-progress batches found to cancel.");
        }
    }
    
    /// <summary>
    /// Deletes a completed Google/Gemini batch.
    /// </summary>
    [TornadoTest]
    [Flaky("Requires completed batch")]
    public static async Task DeleteBatchGoogle()
    {
        TornadoApi api = Program.Connect();
        
        // Find a completed batch
        HttpCallResult<ListResponse<BatchItem>> listResult = await api.Batch.List(new ListQuery { Limit = 20 }, LLmProviders.Google);
        
        BatchItem? completedBatch = listResult.Data?.Items.FirstOrDefault(b => 
            b.Status == BatchStatus.Completed || b.Status == BatchStatus.Failed || b.Status == BatchStatus.Cancelled);
        
        if (completedBatch is not null)
        {
            Console.WriteLine($"Deleting batch: {completedBatch.Id}");
            Console.WriteLine($"Display name: {completedBatch.DisplayName}");
            
            HttpCallResult<bool> result = await api.Batch.Delete(completedBatch.Id, LLmProviders.Google);
            
            if (result.Ok && result.Data)
            {
                Console.WriteLine("Batch deleted successfully.");
            }
            else
            {
                Console.WriteLine($"Failed to delete batch: {result.Exception?.Message}");
            }
        }
        else
        {
            Console.WriteLine("No completed batches found to delete.");
        }
    }
}
