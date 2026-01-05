using System;
using System.Collections.Generic;
using LlmTornado.Code;
using LlmTornado.Code.Models;

namespace LlmTornado.Embedding.Models.Upstage;

/// <summary>
/// Known embedding models from Upstage.
/// </summary>
public class EmbeddingModelUpstage : BaseVendorModelProvider
{
    /// <inheritdoc cref="BaseVendorModelProvider.Provider"/>
    public override LLmProviders Provider => LLmProviders.Upstage;
    
    /// <summary>
    /// All known embedding models from Upstage.
    /// </summary>
    public override List<IModel> AllModels => ModelsAll;
    
    /// <summary>
    /// Checks whether the model is owned by the provider.
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    public override bool OwnsModel(string model)
    {
        return AllModelsMap.Contains(model);
    }

    /// <summary>
    /// Map of models owned by the provider.
    /// </summary>
    public static HashSet<string> AllModelsMap => LazyAllModelsMap.Value;

    private static readonly Lazy<HashSet<string>> LazyAllModelsMap = new Lazy<HashSet<string>>(() =>
    {
        HashSet<string> map = [];
        ModelsAll.ForEach(x => { map.Add(x.Name); });
        return map;
    });
    
    /// <summary>
    /// <inheritdoc cref="AllModels"/>
    /// </summary>
    public static List<IModel> ModelsAll => LazyModelsAll.Value;

    private static readonly Lazy<List<IModel>> LazyModelsAll = new Lazy<List<IModel>>(() => [
        ModelEmbeddingQuery,
        ModelEmbeddingPassage
    ]);

    /// <summary>
    /// embedding-query - Currently points to solar-embedding-1-large-query. Optimized for queries and search terms.
    /// </summary>
    public static readonly EmbeddingModel ModelEmbeddingQuery = new EmbeddingModel("embedding-query", LLmProviders.Upstage, 8_192, 1024);

    /// <summary>
    /// <inheritdoc cref="ModelEmbeddingQuery"/>
    /// </summary>
    public readonly EmbeddingModel EmbeddingQuery = ModelEmbeddingQuery;

    /// <summary>
    /// embedding-passage - Currently points to solar-embedding-1-large-passage. Optimized for passages and longer text.
    /// </summary>
    public static readonly EmbeddingModel ModelEmbeddingPassage = new EmbeddingModel("embedding-passage", LLmProviders.Upstage, 8_192, 1024);

    /// <summary>
    /// <inheritdoc cref="ModelEmbeddingPassage"/>
    /// </summary>
    public readonly EmbeddingModel EmbeddingPassage = ModelEmbeddingPassage;
    
    internal EmbeddingModelUpstage()
    {
        
    }
}
