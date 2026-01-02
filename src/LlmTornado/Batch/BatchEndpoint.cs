using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LlmTornado.Batch.Vendors.Anthropic;
using LlmTornado.Batch.Vendors.Google;
using LlmTornado.Batch.Vendors.OpenAi;
using LlmTornado.Code;
using LlmTornado.Common;

namespace LlmTornado.Batch;

/// <summary>
/// Endpoint for batch processing of requests.
/// </summary>
public class BatchEndpoint : EndpointBase
{
    internal BatchEndpoint(TornadoApi api) : base(api)
    {
    }

    /// <inheritdoc />
    protected override CapabilityEndpoints Endpoint => CapabilityEndpoints.Batch;

    /// <summary>
    /// Creates a new batch of requests.
    /// </summary>
    public async Task<HttpCallResult<BatchItem>> Create(BatchRequest request, LLmProviders? provider = null, CancellationToken cancellationToken = default)
    {
        IEndpointProvider resolvedProvider = Api.ResolveProvider(provider);
        
        return resolvedProvider.Provider switch
        {
            LLmProviders.OpenAi or LLmProviders.Custom => await VendorOpenAiBatchHandler.Create(request, resolvedProvider, this, cancellationToken).ConfigureAwait(false),
            LLmProviders.Anthropic => await VendorAnthropicBatchHandler.Create(request, resolvedProvider, this, cancellationToken).ConfigureAwait(false),
            LLmProviders.Google => await VendorGoogleBatchHandler.Create(request, resolvedProvider, this, cancellationToken).ConfigureAwait(false),
            _ => throw new NotSupportedException($"Batch API is not supported for provider {resolvedProvider.Provider}")
        };
    }

    /// <summary>
    /// Retrieves a batch by ID.
    /// </summary>
    public async Task<HttpCallResult<BatchItem>> Get(string batchId, LLmProviders? provider = null, CancellationToken cancellationToken = default)
    {
        IEndpointProvider resolvedProvider = Api.ResolveProvider(provider);
        
        return resolvedProvider.Provider switch
        {
            LLmProviders.Google => await VendorGoogleBatchHandler.Get(batchId, resolvedProvider, this, cancellationToken).ConfigureAwait(false),
            _ => await HttpGet<BatchItem>(resolvedProvider, Endpoint, GetUrl(resolvedProvider, $"/{batchId}"), ct: cancellationToken).ConfigureAwait(false)
        };
    }

    /// <summary>
    /// Lists all batches.
    /// </summary>
    public async Task<HttpCallResult<ListResponse<BatchItem>>> List(ListQuery? query = null, LLmProviders? provider = null, CancellationToken cancellationToken = default)
    {
        IEndpointProvider resolvedProvider = Api.ResolveProvider(provider);
        
        return resolvedProvider.Provider switch
        {
            LLmProviders.Google => await VendorGoogleBatchHandler.List(resolvedProvider, this, query, cancellationToken).ConfigureAwait(false),
            _ => await HttpGet<ListResponse<BatchItem>>(resolvedProvider, Endpoint, GetUrl(resolvedProvider), query?.ToQueryParams(resolvedProvider.Provider), ct: cancellationToken).ConfigureAwait(false)
        };
    }

    /// <summary>
    /// Cancels a batch that is in progress.
    /// </summary>
    public async Task<HttpCallResult<BatchItem>> Cancel(string batchId, LLmProviders? provider = null, CancellationToken cancellationToken = default)
    {
        IEndpointProvider resolvedProvider = Api.ResolveProvider(provider);
        
        return resolvedProvider.Provider switch
        {
            LLmProviders.Google => await VendorGoogleBatchHandler.Cancel(batchId, resolvedProvider, this, cancellationToken).ConfigureAwait(false),
            _ => await HttpPost<BatchItem>(resolvedProvider, Endpoint, GetUrl(resolvedProvider, $"/{batchId}/cancel"), ct: cancellationToken).ConfigureAwait(false)
        };
    }

    /// <summary>
    /// Deletes a batch.
    /// </summary>
    public async Task<HttpCallResult<bool>> Delete(string batchId, LLmProviders? provider = null, CancellationToken cancellationToken = default)
    {
        IEndpointProvider resolvedProvider = Api.ResolveProvider(provider);
        
        return resolvedProvider.Provider switch
        {
            LLmProviders.Anthropic => await VendorAnthropicBatchHandler.Delete(batchId, resolvedProvider, this, cancellationToken).ConfigureAwait(false),
            LLmProviders.Google => await VendorGoogleBatchHandler.Delete(batchId, resolvedProvider, this, cancellationToken).ConfigureAwait(false),
            _ => throw new NotSupportedException("Batch deletion is only supported by Anthropic and Google")
        };
    }

    /// <summary>
    /// Gets results for a completed batch.
    /// </summary>
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
    public async IAsyncEnumerable<BatchResult> GetResultsStreaming(BatchItem batch, LLmProviders? provider = null, CancellationToken cancellationToken = default)
    {
        IEndpointProvider resolvedProvider = Api.ResolveProvider(provider);
        
        IAsyncEnumerable<BatchResult> results = resolvedProvider.Provider switch
        {
            LLmProviders.OpenAi or LLmProviders.Custom => VendorOpenAiBatchHandler.StreamResults(batch, resolvedProvider, cancellationToken),
            LLmProviders.Anthropic => VendorAnthropicBatchHandler.StreamResults(batch, resolvedProvider, this, cancellationToken),
            LLmProviders.Google => VendorGoogleBatchHandler.StreamResults(batch, resolvedProvider, this, Get, cancellationToken),
            _ => AsyncEnumerableEmpty()
        };
        
        await foreach (BatchResult result in results.ConfigureAwait(false).WithCancellation(cancellationToken))
        {
            yield return result;
        }
    }
    
    private static async IAsyncEnumerable<BatchResult> AsyncEnumerableEmpty()
    {
        await Task.CompletedTask;
        yield break;
    }

    /// <summary>
    /// Polls a batch until it completes or reaches a terminal state.
    /// </summary>
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
            
            if (status is BatchStatus.Completed or BatchStatus.Ended or BatchStatus.Failed or BatchStatus.Expired or BatchStatus.Cancelled || (DateTime.UtcNow - startTime).TotalMilliseconds > maxWaitMs)
            {
                return result;
            }

            await Task.Delay(pollingIntervalMs, cancellationToken).ConfigureAwait(false);
        }
        
        return await Get(batchId, provider, cancellationToken).ConfigureAwait(false);
    }
}
