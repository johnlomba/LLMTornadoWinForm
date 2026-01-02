using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LlmTornado.Batch.Vendors.Anthropic;
using LlmTornado.Batch.Vendors.OpenAi;
using LlmTornado.Code;
using LlmTornado.Common;
using LlmTornado.Files;
using Newtonsoft.Json;

namespace LlmTornado.Batch;

/// <summary>
/// Endpoint for batch processing of requests.
/// The Batch API allows you to create asynchronous jobs to process multiple requests at once.
/// </summary>
public class BatchEndpoint : EndpointBase
{
    /// <summary>
    /// Constructor of the api endpoint. Rather than instantiating this yourself, access it through an instance of
    /// <see cref="TornadoApi" /> as <see cref="TornadoApi.Batch" />.
    /// </summary>
    internal BatchEndpoint(TornadoApi api) : base(api)
    {
    }

    /// <summary>
    /// The name of the endpoint.
    /// </summary>
    protected override CapabilityEndpoints Endpoint => CapabilityEndpoints.Batch;

    /// <summary>
    /// Creates a new batch of requests.
    /// </summary>
    /// <param name="request">The batch request containing individual chat requests.</param>
    /// <param name="provider">Which provider to use. Defaults to the first authenticated provider.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created batch.</returns>
    public async Task<HttpCallResult<BatchItem>> Create(BatchRequest request, LLmProviders? provider = null, CancellationToken cancellationToken = default)
    {
        IEndpointProvider resolvedProvider = Api.ResolveProvider(provider);
        
        return resolvedProvider.Provider switch
        {
            LLmProviders.OpenAi or LLmProviders.Custom => await CreateOpenAi(request, resolvedProvider, cancellationToken).ConfigureAwait(false),
            LLmProviders.Anthropic => await CreateAnthropic(request, resolvedProvider, cancellationToken).ConfigureAwait(false),
            _ => throw new NotSupportedException($"Batch API is not supported for provider {resolvedProvider.Provider}")
        };
    }
    
    private async Task<HttpCallResult<BatchItem>> CreateOpenAi(BatchRequest request, IEndpointProvider provider, CancellationToken cancellationToken)
    {
        // Step 1: Serialize to JSONL and upload as a file
        byte[] jsonlBytes = VendorOpenAiBatchRequest.SerializeToJsonlBytes(request, provider);
        
        HttpCallResult<TornadoFile> uploadResult = await Api.Files.Upload(
            jsonlBytes, 
            $"batch_{Guid.NewGuid():N}.jsonl",
            FilePurpose.Batch,
            "application/jsonl",
            provider: provider.Provider
        ).ConfigureAwait(false);
        
        if (!uploadResult.Ok || uploadResult.Data?.Id is null)
        {
            return new HttpCallResult<BatchItem>(
                uploadResult.Code, 
                uploadResult.Response, 
                null, 
                false, 
                uploadResult.Request
            )
            {
                Exception = uploadResult.Exception ?? new Exception("Failed to upload batch file")
            };
        }
        
        // Step 2: Create the batch with the uploaded file ID
        VendorOpenAiBatchCreateRequest createRequest = new VendorOpenAiBatchCreateRequest
        {
            InputFileId = uploadResult.Data.Id,
            Endpoint = "/v1/chat/completions",
            CompletionWindow = request.CompletionWindow.Value,
            Metadata = request.VendorExtensions?.OpenAi?.Metadata
        };
        
        return await HttpPost<BatchItem>(provider, Endpoint, postData: createRequest, ct: cancellationToken).ConfigureAwait(false);
    }
    
    private async Task<HttpCallResult<BatchItem>> CreateAnthropic(BatchRequest request, IEndpointProvider provider, CancellationToken cancellationToken)
    {
        VendorAnthropicBatchRequest anthropicRequest = new VendorAnthropicBatchRequest(request, provider);
        string body = anthropicRequest.Serialize();
        
        return await HttpPost<BatchItem>(provider, Endpoint, postData: body, ct: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Retrieves a batch by ID.
    /// </summary>
    /// <param name="batchId">ID of the batch to retrieve.</param>
    /// <param name="provider">Which provider to use.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The batch.</returns>
    public Task<HttpCallResult<BatchItem>> Get(string batchId, LLmProviders? provider = null, CancellationToken cancellationToken = default)
    {
        IEndpointProvider resolvedProvider = Api.ResolveProvider(provider);
        return HttpGet<BatchItem>(resolvedProvider, Endpoint, GetUrl(resolvedProvider, $"/{batchId}"), ct: cancellationToken);
    }

    /// <summary>
    /// Lists all batches.
    /// </summary>
    /// <param name="query">Optional query parameters for pagination.</param>
    /// <param name="provider">Which provider to use.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of batches.</returns>
    public Task<HttpCallResult<ListResponse<BatchItem>>> List(ListQuery? query = null, LLmProviders? provider = null, CancellationToken cancellationToken = default)
    {
        IEndpointProvider resolvedProvider = Api.ResolveProvider(provider);
        return HttpGet<ListResponse<BatchItem>>(resolvedProvider, Endpoint, GetUrl(resolvedProvider), query?.ToQueryParams(resolvedProvider.Provider), ct: cancellationToken);
    }

    /// <summary>
    /// Cancels a batch that is in progress.
    /// </summary>
    /// <param name="batchId">ID of the batch to cancel.</param>
    /// <param name="provider">Which provider to use.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated batch.</returns>
    public Task<HttpCallResult<BatchItem>> Cancel(string batchId, LLmProviders? provider = null, CancellationToken cancellationToken = default)
    {
        IEndpointProvider resolvedProvider = Api.ResolveProvider(provider);
        return HttpPost<BatchItem>(resolvedProvider, Endpoint, GetUrl(resolvedProvider, $"/{batchId}/cancel"), ct: cancellationToken);
    }

    /// <summary>
    /// Deletes a batch. Only supported by Anthropic.
    /// The batch must have finished processing (status = ended) before it can be deleted.
    /// </summary>
    /// <param name="batchId">ID of the batch to delete.</param>
    /// <param name="provider">Which provider to use.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The deleted batch info.</returns>
    public async Task<HttpCallResult<bool>> Delete(string batchId, LLmProviders? provider = null, CancellationToken cancellationToken = default)
    {
        IEndpointProvider resolvedProvider = Api.ResolveProvider(provider);
        
        if (resolvedProvider.Provider is not LLmProviders.Anthropic)
        {
            throw new NotSupportedException("Batch deletion is only supported by Anthropic");
        }
        
        HttpCallResult<DeletedBatch> result = await HttpDelete<DeletedBatch>(resolvedProvider, Endpoint, GetUrl(resolvedProvider, $"/{batchId}"), ct: cancellationToken).ConfigureAwait(false);
        return new HttpCallResult<bool>(result.Code, result.Response, result.Data?.Id is not null, result.Ok, result.Request);
    }

    /// <summary>
    /// Gets results for a completed batch.
    /// For OpenAI, downloads results from the output file.
    /// For Anthropic, streams results from the results endpoint.
    /// </summary>
    /// <param name="batch">The batch to get results for. Must be completed/ended.</param>
    /// <param name="provider">Which provider to use.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of batch results.</returns>
    public async Task<List<BatchResult>> GetResults(BatchItem batch, LLmProviders? provider = null, CancellationToken cancellationToken = default)
    {
        List<BatchResult> results = [];
        
        await foreach (BatchResult result in GetResultsStreaming(batch, provider, cancellationToken).ConfigureAwait(false))
        {
            results.Add(result);
        }
        
        return results;
    }

    /// <summary>
    /// Gets results for a completed batch by ID.
    /// </summary>
    /// <param name="batchId">ID of the batch to get results for.</param>
    /// <param name="provider">Which provider to use.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of batch results.</returns>
    public async Task<List<BatchResult>> GetResults(string batchId, LLmProviders? provider = null, CancellationToken cancellationToken = default)
    {
        HttpCallResult<BatchItem> batchResult = await Get(batchId, provider, cancellationToken).ConfigureAwait(false);
        
        if (batchResult.Data is null)
        {
            throw new Exception($"Failed to retrieve batch {batchId}");
        }
        
        return await GetResults(batchResult.Data, provider, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Streams results for a completed batch.
    /// </summary>
    /// <param name="batch">The batch to get results for. Must be completed/ended.</param>
    /// <param name="provider">Which provider to use.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Async enumerable of batch results.</returns>
    public async IAsyncEnumerable<BatchResult> GetResultsStreaming(BatchItem batch, LLmProviders? provider = null, CancellationToken cancellationToken = default)
    {
        IEndpointProvider resolvedProvider = Api.ResolveProvider(provider);
        
        switch (resolvedProvider.Provider)
        {
            case LLmProviders.OpenAi or LLmProviders.Custom:
            {
                if (batch.OutputFileId is null)
                {
                    yield break;
                }
                
                string content = await Api.Files.GetContent(batch.OutputFileId, resolvedProvider.Provider).ConfigureAwait(false);
                
                using StringReader reader = new StringReader(content);
                while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        yield break;
                    }
                    
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }
                    
                    BatchResult? result = BatchResult.Deserialize(resolvedProvider.Provider, line);
                    if (result is not null)
                    {
                        yield return result;
                    }
                }
                
                break;
            }
            case LLmProviders.Anthropic:
            {
                // Anthropic returns a results_url in the batch response that should be used to download results
                if (string.IsNullOrEmpty(batch.ResultsUrl))
                {
                    yield break;
                }
                
                // Use the results URL directly - it's a full URL to download JSONL results
                StreamResponse? response = await HttpGetStream(resolvedProvider, Endpoint, batch.ResultsUrl, ct: cancellationToken).ConfigureAwait(false);
                
                if (response?.Stream is null)
                {
                    yield break;
                }

                try
                {
                    using StreamReader reader = new StreamReader(response.Stream);
                    while (!reader.EndOfStream)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            yield break;
                        }
                        
                        string? line = await reader.ReadLineAsync().ConfigureAwait(false);
                        
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            continue;
                        }
                        
                        BatchResult? result = BatchResult.Deserialize(resolvedProvider.Provider, line);
                        if (result is not null)
                        {
                            yield return result;
                        }
                    }
                }
                finally
                {
                    response.Response?.Dispose();
                }
                
                break;
            }
        }
    }

    /// <summary>
    /// Polls a batch until it completes or reaches a terminal state.
    /// </summary>
    /// <param name="batchId">ID of the batch to poll.</param>
    /// <param name="pollingIntervalMs">Interval between polls in milliseconds. Defaults to 5000 (5 seconds).</param>
    /// <param name="maxWaitMs">Maximum time to wait in milliseconds. Defaults to 86400000 (24 hours).</param>
    /// <param name="provider">Which provider to use.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The final batch state.</returns>
    public async Task<HttpCallResult<BatchItem>> WaitForCompletion(
        string batchId, 
        int pollingIntervalMs = 5000, 
        int maxWaitMs = 86400000,
        LLmProviders? provider = null, 
        CancellationToken cancellationToken = default)
    {
        DateTime startTime = DateTime.UtcNow;
        
        while (!cancellationToken.IsCancellationRequested)
        {
            HttpCallResult<BatchItem> result = await Get(batchId, provider, cancellationToken).ConfigureAwait(false);
            
            if (!result.Ok || result.Data is null)
            {
                return result;
            }
            
            BatchStatus status = result.Data.Status;
            
            // Check for terminal states
            if (status is BatchStatus.Completed or BatchStatus.Ended or BatchStatus.Failed or BatchStatus.Expired or BatchStatus.Cancelled)
            {
                return result;
            }
            
            // Check timeout
            if ((DateTime.UtcNow - startTime).TotalMilliseconds > maxWaitMs)
            {
                return result;
            }
            
            await Task.Delay(pollingIntervalMs, cancellationToken).ConfigureAwait(false);
        }
        
        return await Get(batchId, provider, cancellationToken).ConfigureAwait(false);
    }
}
