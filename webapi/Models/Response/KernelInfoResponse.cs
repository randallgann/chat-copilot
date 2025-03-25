// Copyright (c) Microsoft. All rights reserved.

namespace CopilotChat.WebApi.Models.Response;

/// <summary>
/// Response model containing detailed information about a kernel instance.
/// </summary>
public class KernelInfoResponse
{
    /// <summary>
    /// The user ID associated with this kernel.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// The context ID associated with this kernel.
    /// </summary>
    public string ContextId { get; set; } = string.Empty;

    /// <summary>
    /// The time when this kernel was last accessed.
    /// </summary>
    public DateTime LastAccessTime { get; set; }

    /// <summary>
    /// Information about the plugins loaded in this kernel.
    /// </summary>
    public List<KernelPluginInfo> Plugins { get; set; } = new();

    /// <summary>
    /// Information about the AI models used by this kernel.
    /// </summary>
    public KernelModelInfo ModelInfo { get; set; } = new();
}

/// <summary>
/// Information about a plugin loaded in a kernel.
/// </summary>
public class KernelPluginInfo
{
    /// <summary>
    /// The name of the plugin.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// The functions provided by this plugin.
    /// </summary>
    public List<string> Functions { get; set; } = new();
}

/// <summary>
/// Information about the AI models used by a kernel.
/// </summary>
public class KernelModelInfo
{
    /// <summary>
    /// The ID of the completion model.
    /// </summary>
    public string CompletionModelId { get; set; } = string.Empty;
    
    /// <summary>
    /// The ID of the embedding model.
    /// </summary>
    public string EmbeddingModelId { get; set; } = string.Empty;
    
    /// <summary>
    /// The temperature setting for the completion model.
    /// </summary>
    public float Temperature { get; set; }
    
    /// <summary>
    /// The maximum tokens setting for the completion model.
    /// </summary>
    public int MaxTokens { get; set; }
}