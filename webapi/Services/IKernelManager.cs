// Copyright (c) Microsoft. All rights reserved.

namespace CopilotChat.WebApi.Services;

using Microsoft.SemanticKernel;

/// <summary>
/// Interface for managing user-specific kernel instances.
/// </summary>
public interface IKernelManager
{
    /// <summary>
    /// Get a kernel for a specific user and context.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="contextId">The context ID (e.g., channelId or other identifier). If null, uses a default context.</param>
    /// <returns>A kernel for the user.</returns>
    Task<Kernel> GetUserKernelAsync(string userId, string? contextId = null);

    /// <summary>
    /// Release a kernel for a specific user and context.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="contextId">The context ID. If null, releases only the default context.</param>
    /// <param name="releaseAllUserContexts">If true, releases all kernels for the user regardless of contextId.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ReleaseUserKernelAsync(string userId, string? contextId = null, bool releaseAllUserContexts = false);

    /// <summary>
    /// Clear all kernels from memory.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ClearAllKernelsAsync();
}