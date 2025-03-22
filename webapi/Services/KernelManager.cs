// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Concurrent;
using CopilotChat.WebApi.Extensions;
using CopilotChat.WebApi.Models.Kernel;
using CopilotChat.WebApi.Storage;
using Microsoft.SemanticKernel;

namespace CopilotChat.WebApi.Services;

/// <summary>
/// Service that manages user-specific kernel instances.
/// </summary>
public class KernelManager : IKernelManager
{
    // Dictionary to store user kernel info, keyed by "userId:contextId"
    private readonly ConcurrentDictionary<string, UserKernelInfo> _userKernels = new();
    private readonly ILogger<KernelManager> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IUserKernelConfigRepository _configRepository;
    private readonly SemanticKernelProvider _semanticKernelProvider;

    /// <summary>
    /// Initializes a new instance of the KernelManager class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="configRepository">The user kernel config repository.</param>
    /// <param name="semanticKernelProvider">The semantic kernel provider.</param>
    public KernelManager(
        ILogger<KernelManager> logger,
        IServiceProvider serviceProvider,
        IUserKernelConfigRepository configRepository,
        SemanticKernelProvider semanticKernelProvider)
    {
        this._logger = logger;
        this._serviceProvider = serviceProvider;
        this._configRepository = configRepository;
        this._semanticKernelProvider = semanticKernelProvider;
    }

    /// <inheritdoc/>
    public async Task<Kernel> GetUserKernelAsync(string userId, string? contextId = null)
    {
        contextId ??= "default";
        string key = $"{userId}:{contextId}";

        if (!this._userKernels.TryGetValue(key, out var userKernelInfo))
        {
            userKernelInfo = await this.CreateUserKernelInfoAsync(userId, contextId);
            this._userKernels[key] = userKernelInfo;
        }

        return userKernelInfo.GetKernel();
    }

    /// <inheritdoc/>
    public Task ReleaseUserKernelAsync(string userId, string? contextId = null, bool releaseAllUserContexts = false)
    {
        if (releaseAllUserContexts)
        {
            // Remove all kernels for this user by finding all keys that start with userId:
            var keysToRemove = this._userKernels.Keys.Where(k => k.StartsWith($"{userId}:", StringComparison.Ordinal)).ToList();
            foreach (var key in keysToRemove)
            {
                this._userKernels.TryRemove(key, out _);
            }
        }
        else
        {
            // Remove only the specific kernel
            contextId ??= "default";
            string key = $"{userId}:{contextId}";
            this._userKernels.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ClearAllKernelsAsync()
    {
        this._userKernels.Clear();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Create a new user kernel info.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="contextId">The context ID.</param>
    /// <returns>A new user kernel info.</returns>
    private async Task<UserKernelInfo> CreateUserKernelInfoAsync(string userId, string contextId)
    {
        // Get user configuration or create default
        var userConfig = await this._configRepository.GetConfigAsync(userId, contextId);
        
        // Create a kernel for the user
        Kernel kernel;
        if (userConfig != null)
        {
            this._logger.LogInformation("Creating kernel for user {UserId} with context {ContextId} using custom configuration", userId, contextId);
            kernel = this._semanticKernelProvider.GetCompletionKernel(userConfig);
        }
        else
        {
            this._logger.LogInformation("Creating kernel for user {UserId} with context {ContextId} using default configuration", userId, contextId);
            kernel = this._semanticKernelProvider.GetCompletionKernel();
        }
        
        try 
        {
            // Register standard plugins - using the chat plugin
            // Using direct registration method instead of requiring the delegate
            SemanticKernelExtensions.RegisterChatPlugin(kernel, this._serviceProvider);
            
            // Also register the time plugin
            kernel.ImportPluginFromObject(new Microsoft.SemanticKernel.Plugins.Core.TimePlugin(), nameof(Microsoft.SemanticKernel.Plugins.Core.TimePlugin));
            
            // Register any custom kernel setup
            this._serviceProvider.GetService<SemanticKernelExtensions.KernelSetupHook>()?.Invoke(this._serviceProvider, kernel);
        }
        catch (Exception ex) 
        {
            this._logger.LogError(ex, "Error registering plugins for user {UserId} with context {ContextId}", userId, contextId);
        }
        
        // Apply any user-specific configuration
        if (userConfig != null)
        {
            this.ApplyUserConfigToKernel(kernel, userConfig);
        }
        
        return new UserKernelInfo(userId, contextId, kernel);
    }

    /// <summary>
    /// Apply user configuration to a kernel.
    /// </summary>
    /// <param name="kernel">The kernel to configure.</param>
    /// <param name="config">The user configuration.</param>
    private void ApplyUserConfigToKernel(Kernel kernel, UserKernelConfig config)
    {
        // Apply any additional user configuration that cannot be applied during kernel creation
        
        // Example: Selectively enable plugins based on user preferences
        if (config.EnabledPlugins.Count > 0)
        {
            this._logger.LogInformation("Applying user-specific plugin configuration for user {UserId} with context {ContextId}", 
                config.UserId, config.ContextId);
            
            // Register user-specific plugins here based on config.EnabledPlugins
        }
        
        // Apply any context-specific settings
        if (config.ContextSettings.Count > 0)
        {
            this._logger.LogInformation("Applying context-specific settings for user {UserId} with context {ContextId}", 
                config.UserId, config.ContextId);
            
            // Apply context-specific settings (e.g., for a YouTube channel)
        }
    }
}