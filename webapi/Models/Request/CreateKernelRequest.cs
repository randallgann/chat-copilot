// Copyright (c) Microsoft. All rights reserved.

using CopilotChat.WebApi.Models.Kernel;

namespace CopilotChat.WebApi.Models.Request;

/// <summary>
/// Request model for creating a new kernel.
/// </summary>
public class CreateKernelRequest
{
    /// <summary>
    /// The context ID to associate with the kernel (e.g., channelId or other identifier).
    /// If not provided, a default context is used.
    /// </summary>
    public string? ContextId { get; set; }

    /// <summary>
    /// Options for the completion model.
    /// </summary>
    public LLMOptions? CompletionOptions { get; set; }

    /// <summary>
    /// Options for the embedding model.
    /// </summary>
    public LLMOptions? EmbeddingOptions { get; set; }

    /// <summary>
    /// List of enabled plugins for this kernel.
    /// </summary>
    public List<string>? EnabledPlugins { get; set; }

    /// <summary>
    /// Convert this request to a UserKernelConfig.
    /// </summary>
    /// <param name="userId">The user ID to associate with the config.</param>
    /// <returns>A UserKernelConfig object.</returns>
    public UserKernelConfig ToUserKernelConfig(string userId)
    {
        return new UserKernelConfig
        {
            UserId = userId,
            ContextId = this.ContextId ?? "default",
            CreatedOn = DateTimeOffset.UtcNow,
            UpdatedOn = DateTimeOffset.UtcNow,
            Settings = new Dictionary<string, object>(),
            CompletionOptions = this.CompletionOptions ?? new LLMOptions(),
            EmbeddingOptions = this.EmbeddingOptions ?? new LLMOptions(),
            EnabledPlugins = this.EnabledPlugins ?? new List<string>(),
            ApiKeys = new Dictionary<string, string>(),
            ContextSettings = new Dictionary<string, object>()
        };
    }
}