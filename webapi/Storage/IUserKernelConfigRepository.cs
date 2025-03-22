// Copyright (c) Microsoft. All rights reserved.

using CopilotChat.WebApi.Models.Kernel;

namespace CopilotChat.WebApi.Storage;

/// <summary>
/// Interface for a repository of user kernel configurations.
/// </summary>
public interface IUserKernelConfigRepository
{
    /// <summary>
    /// Get the kernel configuration for a user and context.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="contextId">The context ID (e.g., channelId). If null, uses default context.</param>
    /// <returns>The user's kernel configuration, or null if not found.</returns>
    Task<UserKernelConfig?> GetConfigAsync(string userId, string? contextId = null);

    /// <summary>
    /// Get all configurations for a specific user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns>List of all configurations for the user across all contexts.</returns>
    Task<IEnumerable<UserKernelConfig>> GetUserConfigsAsync(string userId);

    /// <summary>
    /// Save a kernel configuration for a user.
    /// </summary>
    /// <param name="config">The configuration to save.</param>
    /// <returns>A task that completes when the configuration is saved.</returns>
    Task SaveConfigAsync(UserKernelConfig config);

    /// <summary>
    /// Delete a user's kernel configuration for a specific context.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="contextId">The context ID. If null, deletes only the default context.</param>
    /// <returns>A task that completes when the configuration is deleted.</returns>
    Task DeleteConfigAsync(string userId, string? contextId = null);

    /// <summary>
    /// Delete all configurations for a specific user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns>A task that completes when all configurations are deleted.</returns>
    Task DeleteAllUserConfigsAsync(string userId);
}