// Copyright (c) Microsoft. All rights reserved.

using CopilotChat.WebApi.Storage;

namespace CopilotChat.WebApi.Models.Kernel;

/// <summary>
/// Configuration for a user-specific kernel.
/// </summary>
public class UserKernelConfig : IStorageEntity
{
    /// <summary>
    /// The unique ID for this configuration.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The user ID associated with this kernel configuration.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// The context ID associated with this kernel configuration (e.g., channelId or any other identifier).
    /// </summary>
    public string ContextId { get; set; } = "default";

    /// <summary>
    /// The timestamp when this configuration was created.
    /// </summary>
    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// The timestamp when this configuration was last updated.
    /// </summary>
    public DateTimeOffset UpdatedOn { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Generic settings for the kernel.
    /// </summary>
    public Dictionary<string, object> Settings { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// Options for the completion model.
    /// </summary>
    public LLMOptions? CompletionOptions { get; set; }

    /// <summary>
    /// Options for the embedding model.
    /// </summary>
    public LLMOptions? EmbeddingOptions { get; set; }

    /// <summary>
    /// List of enabled plugins for this user.
    /// </summary>
    public List<string> EnabledPlugins { get; set; } = new List<string>();

    /// <summary>
    /// API keys for various services.
    /// </summary>
    public Dictionary<string, string> ApiKeys { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Context-specific settings (e.g., for a specific YouTube channel).
    /// </summary>
    public Dictionary<string, object> ContextSettings { get; set; } = new Dictionary<string, object>();

    public string Partition => throw new NotImplementedException();
}