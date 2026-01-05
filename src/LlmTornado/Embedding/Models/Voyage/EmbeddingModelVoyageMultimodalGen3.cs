using System;
using System.Collections.Generic;
using LlmTornado.Code;
using LlmTornado.Code.Models;

namespace LlmTornado.Embedding.Models.Voyage;

/// <summary>
/// Voyage Multimodal Gen 3 embedding models from Voyage.
/// </summary>
public class EmbeddingModelVoyageMultimodalGen3 : BaseVendorModelProvider
{
    /// <inheritdoc cref="BaseVendorModelProvider.Provider"/>
    public override LLmProviders Provider => LLmProviders.Voyage;
    
    /// <summary>
    /// A multimodal model that can embed text, images, or an interleaving of both modalities.
    /// </summary>
    public static readonly MultimodalEmbeddingModel ModelMultimodal3 = new MultimodalEmbeddingModel("voyage-multimodal-3", LLmProviders.Voyage, 32_000, 1024);

    /// <summary>
    /// <inheritdoc cref="ModelMultimodal3"/>
    /// </summary>
    public readonly MultimodalEmbeddingModel Multimodal3 = ModelMultimodal3;
    
    /// <summary>
    /// All owned models.
    /// </summary>
    public override List<IModel> AllModels => ModelsAll;

    /// <summary>
    /// Checks whether a model is owned.
    /// </summary>
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
    /// All known Voyage Multimodal Gen 3 models.
    /// </summary>
    public static List<IModel> ModelsAll => LazyModelsAll.Value;

    private static readonly Lazy<List<IModel>> LazyModelsAll = new Lazy<List<IModel>>(() => [
        ModelMultimodal3
    ]);
    
    internal EmbeddingModelVoyageMultimodalGen3()
    {
        
    }
}