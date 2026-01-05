using System;
using System.Collections.Generic;
using LlmTornado.Chat.Models;
using LlmTornado.Code;
using LlmTornado.Code.Models;

namespace LlmTornado.Embedding.Models.Cohere;

/// <summary>
/// Known embedding models from Cohere.
/// </summary>
public class EmbeddingModelCohere : BaseVendorModelProvider
{
    /// <inheritdoc cref="BaseVendorModelProvider.Provider"/>
    public override LLmProviders Provider => LLmProviders.Cohere;
    
    /// <summary>
    /// Generation 2 models.
    /// </summary>
    public readonly EmbeddingModelCohereGen2 Gen2 = new EmbeddingModelCohereGen2();
    
    /// <summary>
    /// Generation 3 models.
    /// </summary>
    public readonly EmbeddingModelCohereGen3 Gen3 = new EmbeddingModelCohereGen3();
    
    /// <summary>
    /// Generation 4 models.
    /// </summary>
    public readonly EmbeddingModelCohereGen4 Gen4 = new EmbeddingModelCohereGen4();
    
    /// <summary>
    /// All known embedding models from Cohere.
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
        ..EmbeddingModelCohereGen2.ModelsAll,
        ..EmbeddingModelCohereGen3.ModelsAll,
        ..EmbeddingModelCohereGen4.ModelsAll
    ]);
    
    internal EmbeddingModelCohere()
    {
        
    }
}