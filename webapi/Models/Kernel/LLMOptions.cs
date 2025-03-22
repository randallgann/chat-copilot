// Copyright (c) Microsoft. All rights reserved.

namespace CopilotChat.WebApi.Models.Kernel;

/// <summary>
/// Options for configuring a large language model.
/// </summary>
public class LLMOptions
{
    /// <summary>
    /// The model ID to use.
    /// </summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// The endpoint to use for the model.
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// The temperature to use for generation (0.0 to 1.0).
    /// </summary>
    public float Temperature { get; set; } = 0.7f;

    /// <summary>
    /// The maximum number of tokens to generate.
    /// </summary>
    public int MaxTokens { get; set; } = 2000;
}