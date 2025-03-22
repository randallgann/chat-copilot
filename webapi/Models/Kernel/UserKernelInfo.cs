// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel;

namespace CopilotChat.WebApi.Models.Kernel;

/// <summary>
/// Container for a user-specific kernel instance.
/// </summary>
public class UserKernelInfo
{
    /// <summary>
    /// The user ID associated with this kernel.
    /// </summary>
    public string UserId { get; private set; }

    /// <summary>
    /// The context ID associated with this kernel (e.g., channelId or other identifier).
    /// </summary>
    public string ContextId { get; private set; }

    /// <summary>
    /// The semantic kernel instance.
    /// </summary>
    public Microsoft.SemanticKernel.Kernel Kernel { get; private set; }

    /// <summary>
    /// The last time this kernel was accessed.
    /// </summary>
    public DateTime LastAccessTime { get; private set; }

    /// <summary>
    /// Constructor for a new user kernel info.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="contextId">The context ID (e.g., channelId or other identifier).</param>
    /// <param name="kernel">The semantic kernel instance.</param>
    public UserKernelInfo(string userId, string contextId, Microsoft.SemanticKernel.Kernel kernel)
    {
        this.UserId = userId;
        this.ContextId = contextId;
        this.Kernel = kernel;
        this.LastAccessTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Update the last access time.
    /// </summary>
    public void UpdateLastAccessTime()
    {
        this.LastAccessTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Get the kernel instance.
    /// </summary>
    /// <returns>The semantic kernel instance.</returns>
    public Microsoft.SemanticKernel.Kernel GetKernel()
    {
        this.UpdateLastAccessTime();
        return this.Kernel;
    }
}