using System;
using System.Collections.Generic;
using LlmTornado.Chat.Models;
using LlmTornado.Code;
using LlmTornado.Code.Models;
using LlmTornado.Embedding.Models.OpenAi;

namespace LlmTornado.Embedding.Models.Cohere;

/// <summary>
/// Generation 4 embedding models from Cohere.
/// </summary>
public class EmbeddingModelCohereGen4 : BaseVendorModelProvider
{
    /// <inheritdoc cref="BaseVendorModelProvider.Provider"/>
    public override LLmProviders Provider => LLmProviders.Cohere;
    
    /// <summary>
    /// A model that allows for text and images to be classified or turned into embeddings.
    /// </summary>
    public static readonly EmbeddingModel ModelV4 = new EmbeddingModel("embed-v4.0", LLmProviders.Cohere, 128_000, 1_536, [ 256, 512, 1024, 1536 ]);

    /// <summary>
    /// <inheritdoc cref="ModelV4"/>
    /// </summary>
    public readonly EmbeddingModel V4 = ModelV4;

    /// <summary>
    /// All known embedding models from Cohere Gen 4.
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
        ModelV4
    ]);
    
    internal EmbeddingModelCohereGen4()
    {
        
    }
}