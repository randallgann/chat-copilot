// Copyright (c) Microsoft. All rights reserved.

using CopilotChat.WebApi.Auth;
using CopilotChat.WebApi.Models.Kernel;
using CopilotChat.WebApi.Services;
using CopilotChat.WebApi.Storage;
using Microsoft.AspNetCore.Mvc;

namespace CopilotChat.WebApi.Controllers;

/// <summary>
/// Controller for managing user-specific kernel configurations.
/// </summary>
[ApiController]
public class UserConfigController : ControllerBase
{
    private readonly IUserKernelConfigRepository _configRepository;
    private readonly IAuthInfo _authInfo;
    private readonly IKernelManager _kernelManager;
    private readonly ILogger<UserConfigController> _logger;

    /// <summary>
    /// Initializes a new instance of the UserConfigController class.
    /// </summary>
    /// <param name="configRepository">The user kernel config repository.</param>
    /// <param name="authInfo">The auth info.</param>
    /// <param name="kernelManager">The kernel manager.</param>
    /// <param name="logger">The logger.</param>
    public UserConfigController(
        IUserKernelConfigRepository configRepository,
        IAuthInfo authInfo,
        IKernelManager kernelManager,
        ILogger<UserConfigController> logger)
    {
        this._configRepository = configRepository;
        this._authInfo = authInfo;
        this._kernelManager = kernelManager;
        this._logger = logger;
    }

    /// <summary>
    /// Get the current user's kernel configuration for a specific context.
    /// </summary>
    /// <param name="contextId">Optional context ID (e.g., channelId). If not provided, returns the default context.</param>
    /// <returns>The user's kernel configuration, or a default configuration if none exists.</returns>
    [Route("userconfig")]
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserConfigAsync([FromQuery] string? contextId = null)
    {
        var userId = this._authInfo.UserId;
        var config = await this._configRepository.GetConfigAsync(userId, contextId);
        
        if (config == null)
        {
            // Return a default config if none exists
            config = new UserKernelConfig
            {
                UserId = userId,
                ContextId = contextId ?? "default",
                Settings = new Dictionary<string, object>(),
                CompletionOptions = new LLMOptions(),
                EmbeddingOptions = new LLMOptions(),
                EnabledPlugins = new List<string>(),
                ApiKeys = new Dictionary<string, string>(),
                ContextSettings = new Dictionary<string, object>()
            };
        }
        
        return this.Ok(config);
    }

    /// <summary>
    /// Get all configurations for the current user.
    /// </summary>
    /// <returns>List of all user configurations.</returns>
    [Route("userconfig/all")]
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllUserConfigsAsync()
    {
        var userId = this._authInfo.UserId;
        var configs = await this._configRepository.GetUserConfigsAsync(userId);
        
        return this.Ok(configs);
    }

    /// <summary>
    /// Update the current user's kernel configuration.
    /// </summary>
    /// <param name="config">The new configuration.</param>
    /// <returns>The updated configuration.</returns>
    [Route("userconfig")]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateUserConfigAsync([FromBody] UserKernelConfig config)
    {
        var userId = this._authInfo.UserId;
        
        // Ensure the config is for the current user
        if (config.UserId != userId)
        {
            return this.BadRequest("User ID in configuration does not match authenticated user.");
        }
        
        // Save the config
        await this._configRepository.SaveConfigAsync(config);
        
        // Release the current kernel so a new one will be created with the updated config
        await this._kernelManager.ReleaseUserKernelAsync(userId, config.ContextId);
        
        this._logger.LogInformation("Updated kernel configuration for user {UserId} with context {ContextId}", 
            userId, config.ContextId);
        
        return this.Ok(config);
    }

    /// <summary>
    /// Reset a specific context configuration for the current user.
    /// </summary>
    /// <param name="contextId">Optional context ID to reset. If not provided, resets only the default context.</param>
    /// <returns>A success indication.</returns>
    [Route("userconfig/reset")]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ResetUserConfigAsync([FromQuery] string? contextId = null)
    {
        var userId = this._authInfo.UserId;
        
        // Delete the config for the specified context
        await this._configRepository.DeleteConfigAsync(userId, contextId);
        
        // Release the current kernel for this context so a new one will be created with the default config
        await this._kernelManager.ReleaseUserKernelAsync(userId, contextId);
        
        this._logger.LogInformation("Reset kernel configuration for user {UserId} with context {ContextId}", 
            userId, contextId ?? "default");
        
        return this.Ok(new { success = true });
    }

    /// <summary>
    /// Reset all configurations for the current user.
    /// </summary>
    /// <returns>A success indication.</returns>
    [Route("userconfig/resetall")]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ResetAllUserConfigsAsync()
    {
        var userId = this._authInfo.UserId;
        
        // Delete all configs for this user
        await this._configRepository.DeleteAllUserConfigsAsync(userId);
        
        // Release all kernels for this user
        await this._kernelManager.ReleaseUserKernelAsync(userId, null, true);
        
        this._logger.LogInformation("Reset all kernel configurations for user {UserId}", userId);
        
        return this.Ok(new { success = true });
    }
}