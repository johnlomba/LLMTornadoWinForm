using System;
using System.Collections.Generic;
using LlmTornado.Code;
using LlmTornado.Code.Models;

namespace LlmTornado.Chat.Models.Upstage;

/// <summary>
/// Known chat models from Upstage.
/// </summary>
public class ChatModelUpstage : BaseVendorModelProvider
{
    /// <inheritdoc cref="BaseVendorModelProvider.Provider"/>
    public override LLmProviders Provider => LLmProviders.Upstage;
    
    /// <summary>
    /// All known chat models from Upstage.
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
        ModelSolarPro2,
        ModelSolarMini,
        ModelSynPro,
        ModelSolarPro2Nightly,
        ModelSolarMiniNightly
    ]);

    /// <summary>
    /// solar-pro2 - Currently points to solar-pro2-251215. Supports reasoning.
    /// </summary>
    public static readonly ChatModel ModelSolarPro2 = new ChatModel("solar-pro2-251215", LLmProviders.Upstage, 128_000, [ "solar-pro2" ]);

    /// <summary>
    /// <inheritdoc cref="ModelSolarPro2"/>
    /// </summary>
    public readonly ChatModel SolarPro2 = ModelSolarPro2;

    /// <summary>
    /// solar-mini - Currently points to solar-mini-250422.
    /// </summary>
    public static readonly ChatModel ModelSolarMini = new ChatModel("solar-mini-250422", LLmProviders.Upstage, 128_000, [ "solar-mini" ]);

    /// <summary>
    /// <inheritdoc cref="ModelSolarMini"/>
    /// </summary>
    public readonly ChatModel SolarMini = ModelSolarMini;

    /// <summary>
    /// syn-pro - Currently points to syn-pro-251021.
    /// </summary>
    public static readonly ChatModel ModelSynPro = new ChatModel("syn-pro-251021", LLmProviders.Upstage, 128_000);

    /// <summary>
    /// <inheritdoc cref="ModelSynPro"/>
    /// </summary>
    public readonly ChatModel SynPro = ModelSynPro;

    /// <summary>
    /// solar-pro2-nightly - Nightly build of solar-pro2. Supports reasoning.
    /// </summary>
    public static readonly ChatModel ModelSolarPro2Nightly = new ChatModel("solar-pro2-nightly", LLmProviders.Upstage, 128_000);

    /// <summary>
    /// <inheritdoc cref="ModelSolarPro2Nightly"/>
    /// </summary>
    public readonly ChatModel SolarPro2Nightly = ModelSolarPro2Nightly;

    /// <summary>
    /// solar-mini-nightly - Nightly build of solar-mini.
    /// </summary>
    public static readonly ChatModel ModelSolarMiniNightly = new ChatModel("solar-mini-nightly", LLmProviders.Upstage, 128_000);

    /// <summary>
    /// <inheritdoc cref="ModelSolarMiniNightly"/>
    /// </summary>
    public readonly ChatModel SolarMiniNightly = ModelSolarMiniNightly;

    /// <summary>
    /// Models that support reasoning (reasoning_effort parameter).
    /// </summary>
    public static readonly HashSet<IModel> ReasoningModels = [
        ModelSolarPro2,
        ModelSolarPro2Nightly
    ];
    
    /// <summary>
    /// Models capable of reasoning.
    /// </summary>
    public static List<IModel> ReasoningModelsList => LazyReasoningModels.Value;

    private static readonly Lazy<List<IModel>> LazyReasoningModels = new Lazy<List<IModel>>(() => [
        ModelSolarPro2,
        ModelSolarPro2Nightly
    ]);
    
    internal ChatModelUpstage()
    {
        
    }
}
