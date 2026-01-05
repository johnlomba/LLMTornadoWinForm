using System;
using System.Collections.Generic;
using LlmTornado.Chat.Models;
using LlmTornado.Code;
using LlmTornado.Code.Models;

namespace LlmTornado.Audio.Models.OpenAi;

/// <summary>
/// Whisper class models from OpenAI.
/// </summary>
public class AudioModelOpenAiWhisper : IVendorModelClassProvider
{
    /// <summary>
    /// Whisper V2 (whisper-1)
    /// </summary>
    public static readonly AudioModel ModelV2 = new AudioModel("whisper-1", LLmProviders.OpenAi, 16_385);

    /// <summary>
    /// <inheritdoc cref="ModelV2"/>
    /// </summary>
    public readonly AudioModel V2 = ModelV2;

    /// <summary>
    /// All known Whisper models from OpenAI.
    /// </summary>
    public static List<IModel> ModelsAll => LazyModelsAll.Value;

    private static readonly Lazy<List<IModel>> LazyModelsAll = new Lazy<List<IModel>>(() => [
        ModelV2
    ]);

    /// <summary>
    /// <inheritdoc cref="ModelsAll"/>
    /// </summary>
    public List<IModel> AllModels => ModelsAll;
    
    internal AudioModelOpenAiWhisper()
    {
        
    }
}