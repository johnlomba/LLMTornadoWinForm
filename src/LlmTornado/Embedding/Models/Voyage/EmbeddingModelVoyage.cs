using System;
using System.Collections.Generic;
using LlmTornado.Chat.Models;
using LlmTornado.Code;
using LlmTornado.Code.Models;

namespace LlmTornado.Embedding.Models.Voyage;

/// <summary>
/// Known embedding models from Voyage.
/// </summary>
public class EmbeddingModelVoyage : BaseVendorModelProvider
{
    /// <inheritdoc cref="BaseVendorModelProvider.Provider"/>
    public override LLmProviders Provider => LLmProviders.Voyage;
    
    /// <summary>
    /// Voyage 2 models.
    /// </summary>
    public readonly EmbeddingModelVoyageGen2 Gen2 = new EmbeddingModelVoyageGen2();
    
    /// <summary>
    /// Voyage 3 models.
    /// </summary>
    public readonly EmbeddingModelVoyageGen3 Gen3 = new EmbeddingModelVoyageGen3();
    
    /// <summary>
    /// Voyage 3.5 models.
    /// </summary>
    public readonly EmbeddingModelVoyageGen35 Gen35 = new EmbeddingModelVoyageGen35();

    /// <summary>
    /// Voyage contextual models.
    /// </summary>
    public readonly EmbeddingModelVoyageContextual Contextual = new EmbeddingModelVoyageContextual();

    /// <summary>
    /// Voyage multimodal models.
    /// </summary>
    public readonly EmbeddingModelVoyageMultimodal Multimodal = new EmbeddingModelVoyageMultimodal();
    
    /// <summary>
    /// All known embedding models from Voyage.
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
        ..EmbeddingModelVoyageGen2.ModelsAll,
        ..EmbeddingModelVoyageGen3.ModelsAll,
        ..EmbeddingModelVoyageGen35.ModelsAll,
        ..EmbeddingModelVoyageContextual.ModelsAll,
        ..EmbeddingModelVoyageMultimodal.ModelsAll
    ]);
    
    internal EmbeddingModelVoyage()
    {
        
    }
}