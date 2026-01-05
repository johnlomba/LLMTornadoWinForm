using System;
using System.Collections.Generic;
using LlmTornado.Chat.Models;
using LlmTornado.Code;
using LlmTornado.Code.Models;

namespace LlmTornado.Embedding.Models.OpenAi;

/// <summary>
/// Known embedding models from OpenAI.
/// </summary>
public class EmbeddingModelOpenAi : BaseVendorModelProvider
{
    /// <inheritdoc cref="BaseVendorModelProvider.Provider"/>
    public override LLmProviders Provider => LLmProviders.OpenAi;
    
    /// <summary>
    /// Generation 2 models (Ada).
    /// </summary>
    public readonly EmbeddingModelOpenAiGen2 Gen2 = new EmbeddingModelOpenAiGen2();

    /// <summary>
    /// Generation 3 models.
    /// </summary>
    public readonly EmbeddingModelOpenAiGen3 Gen3 = new EmbeddingModelOpenAiGen3();

    /// <summary>
    /// All known embedding models from OpenAI.
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
        ..EmbeddingModelOpenAiGen2.ModelsAll,
        ..EmbeddingModelOpenAiGen3.ModelsAll
    ]);

    internal EmbeddingModelOpenAi()
    {
        
    }
}