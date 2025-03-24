// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using CopilotChat.WebApi.Auth;
using CopilotChat.WebApi.Models.Kernel;
using CopilotChat.WebApi.Models.Request;
using CopilotChat.WebApi.Models.Response;
using CopilotChat.WebApi.Services;
using CopilotChat.WebApi.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;

namespace CopilotChat.WebApi.Controllers;

/// <summary>
/// Controller for managing and inspecting kernel instances.
/// </summary>
[ApiController]
public class KernelController : ControllerBase
{
    private readonly IKernelManager _kernelManager;
    private readonly IAuthInfo _authInfo;
    private readonly ILogger<KernelController> _logger;

    /// <summary>
    /// Initializes a new instance of the KernelController class.
    /// </summary>
    /// <param name="kernelManager">The kernel manager.</param>
    /// <param name="authInfo">The auth info.</param>
    /// <param name="logger">The logger.</param>
    public KernelController(
        IKernelManager kernelManager,
        IAuthInfo authInfo,
        ILogger<KernelController> logger)
    {
        this._kernelManager = kernelManager;
        this._authInfo = authInfo;
        this._logger = logger;
    }

    /// <summary>
    /// Get a list of all active kernels in the system.
    /// </summary>
    /// <returns>A list of active kernels.</returns>
    [Route("api/kernel/list")]
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult ListKernels()
    {
        try
        {
            var userKernels = GetUserKernelsFromManager();
            
            if (userKernels == null)
            {
                return this.Ok(new KernelListResponse { 
                    TotalKernels = 0,
                    Kernels = new List<KernelListItem>()
                });
            }

            var response = new KernelListResponse
            {
                TotalKernels = userKernels.Count,
                Kernels = userKernels.Select(pair => new KernelListItem
                {
                    UserId = pair.Value.UserId,
                    ContextId = pair.Value.ContextId,
                    LastAccessTime = pair.Value.LastAccessTime,
                    Age = DateTime.UtcNow - pair.Value.LastAccessTime
                }).OrderByDescending(k => k.LastAccessTime).ToList()
            };

            return this.Ok(response);
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error getting kernel list");
            return this.StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to retrieve kernel list" });
        }
    }

    /// <summary>
    /// Get information about the current user's kernel for a specific context.
    /// </summary>
    /// <param name="contextId">Optional context ID. If not provided, gets the default context.</param>
    /// <returns>Information about the kernel.</returns>
    [Route("api/kernel/info")]
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCurrentUserKernelInfo([FromQuery] string? contextId = null)
    {
        try
        {
            var userId = this._authInfo.UserId;
            var kernel = await this._kernelManager.GetUserKernelAsync(userId, contextId);
            
            if (kernel == null)
            {
                return this.NotFound(new { error = $"No kernel found for user {userId} with context {contextId ?? "default"}" });
            }

            var userKernelInfo = GetUserKernelInfoFromManager(userId, contextId);
            if (userKernelInfo == null)
            {
                return this.NotFound(new { error = $"No kernel info found for user {userId} with context {contextId ?? "default"}" });
            }

            var response = CreateKernelInfoResponse(kernel, userKernelInfo);
            return this.Ok(response);
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error getting kernel info for user {UserId} with context {ContextId}", 
                this._authInfo.UserId, contextId ?? "default");
            return this.StatusCode(StatusCodes.Status500InternalServerError, 
                new { error = $"Failed to retrieve kernel info for context {contextId ?? "default"}" });
        }
    }

    /// <summary>
    /// Get information about a specific user's kernel.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="contextId">Optional context ID. If not provided, gets the default context.</param>
    /// <returns>Information about the kernel.</returns>
    [Route("api/kernel/info/{userId}/{contextId?}")]
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserKernelInfo(string userId, string? contextId = null)
    {
        try
        {
            // Ensure only the user themselves or an admin can access this
            if (userId != this._authInfo.UserId && !IsAdminUser())
            {
                return this.Forbid();
            }

            var kernel = await this._kernelManager.GetUserKernelAsync(userId, contextId);
            
            if (kernel == null)
            {
                return this.NotFound(new { error = $"No kernel found for user {userId} with context {contextId ?? "default"}" });
            }

            var userKernelInfo = GetUserKernelInfoFromManager(userId, contextId);
            if (userKernelInfo == null)
            {
                return this.NotFound(new { error = $"No kernel info found for user {userId} with context {contextId ?? "default"}" });
            }

            var response = CreateKernelInfoResponse(kernel, userKernelInfo);
            return this.Ok(response);
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error getting kernel info for user {UserId} with context {ContextId}", 
                userId, contextId ?? "default");
            return this.StatusCode(StatusCodes.Status500InternalServerError, 
                new { error = $"Failed to retrieve kernel info for user {userId} with context {contextId ?? "default"}" });
        }
    }

    /// <summary>
    /// Release the current user's kernel for a specific context.
    /// </summary>
    /// <param name="contextId">Optional context ID. If not provided, releases the default context.</param>
    /// <param name="releaseAllContexts">Whether to release all contexts for the user.</param>
    /// <returns>A success indication.</returns>
    [Route("api/kernel/release")]
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ReleaseCurrentUserKernel(
        [FromQuery] string? contextId = null, 
        [FromQuery] bool releaseAllContexts = false)
    {
        try
        {
            var userId = this._authInfo.UserId;
            
            await this._kernelManager.ReleaseUserKernelAsync(userId, contextId, releaseAllContexts);
            
            this._logger.LogInformation(
                "Released kernel for user {UserId} with context {ContextId}, releaseAllContexts: {ReleaseAllContexts}", 
                userId, contextId ?? "default", releaseAllContexts);
            
            return this.NoContent();
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error releasing kernel for user {UserId} with context {ContextId}", 
                this._authInfo.UserId, contextId ?? "default");
            return this.StatusCode(StatusCodes.Status500InternalServerError, 
                new { error = $"Failed to release kernel for context {contextId ?? "default"}" });
        }
    }

    /// <summary>
    /// Create or refresh a kernel for the current user.
    /// </summary>
    /// <param name="request">The create kernel request with optional settings.</param>
    /// <returns>Information about the created kernel.</returns>
    [Route("api/kernel/create")]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateKernel([FromBody] CreateKernelRequest? request = null)
    {
        try
        {
            var userId = this._authInfo.UserId;
            string contextId = request?.ContextId ?? "default";
            
            var configRepository = this.HttpContext.RequestServices.GetRequiredService<IUserKernelConfigRepository>();
            
            // Create and save a UserKernelConfig if request was provided
            if (request != null)
            {
                var config = request.ToUserKernelConfig(userId);
                await configRepository.SaveConfigAsync(config);
            }
            
            // Release any existing kernel for this user/context
            await this._kernelManager.ReleaseUserKernelAsync(userId, contextId);
            
            // Get a fresh kernel - this will create a new one based on the config we just saved
            var kernel = await this._kernelManager.GetUserKernelAsync(userId, contextId);
            
            if (kernel == null)
            {
                return this.StatusCode(StatusCodes.Status500InternalServerError, 
                    new { error = "Failed to create kernel" });
            }
            
            // Get kernel info for response
            var userKernelInfo = GetUserKernelInfoFromManager(userId, contextId);
            if (userKernelInfo == null)
            {
                return this.StatusCode(StatusCodes.Status500InternalServerError, 
                    new { error = "Kernel was created but info could not be retrieved" });
            }
            
            var response = CreateKernelInfoResponse(kernel, userKernelInfo);
            
            this._logger.LogInformation("Created kernel for user {UserId} with context {ContextId}", 
                userId, contextId);
            
            return this.Ok(response);
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error creating kernel for user {UserId}", this._authInfo.UserId);
            return this.StatusCode(StatusCodes.Status500InternalServerError, 
                new { error = "Failed to create kernel" });
        }
    }
    
    /// <summary>
    /// Create or refresh a kernel for the current user with a full configuration.
    /// </summary>
    /// <param name="config">The full user kernel configuration.</param>
    /// <returns>Information about the created kernel.</returns>
    [Route("api/kernel/create/full")]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateKernelFull([FromBody] UserKernelConfig config)
    {
        try
        {
            var userId = this._authInfo.UserId;
            
            // Ensure the config is for the current user
            if (config.UserId != userId)
            {
                return this.BadRequest(new { error = "User ID in configuration does not match authenticated user." });
            }
            
            // Validate the configuration
            if (string.IsNullOrEmpty(config.ContextId))
            {
                config.ContextId = "default";
            }
            
            // Set created/updated timestamps
            config.CreatedOn = DateTimeOffset.UtcNow;
            config.UpdatedOn = DateTimeOffset.UtcNow;
            
            // Ensure required properties are initialized
            config.Settings ??= new Dictionary<string, object>();
            config.CompletionOptions ??= new LLMOptions();
            config.EmbeddingOptions ??= new LLMOptions();
            config.EnabledPlugins ??= new List<string>();
            config.ApiKeys ??= new Dictionary<string, string>();
            config.ContextSettings ??= new Dictionary<string, object>();
            
            // Save to repository
            var configRepository = this.HttpContext.RequestServices.GetRequiredService<IUserKernelConfigRepository>();
            await configRepository.SaveConfigAsync(config);
            
            // Release any existing kernel for this user/context
            await this._kernelManager.ReleaseUserKernelAsync(userId, config.ContextId);
            
            // Get a fresh kernel - this will create a new one based on the config we just saved
            var kernel = await this._kernelManager.GetUserKernelAsync(userId, config.ContextId);
            
            if (kernel == null)
            {
                return this.StatusCode(StatusCodes.Status500InternalServerError, 
                    new { error = "Failed to create kernel" });
            }
            
            // Get kernel info for response
            var userKernelInfo = GetUserKernelInfoFromManager(userId, config.ContextId);
            if (userKernelInfo == null)
            {
                return this.StatusCode(StatusCodes.Status500InternalServerError, 
                    new { error = "Kernel was created but info could not be retrieved" });
            }
            
            var response = CreateKernelInfoResponse(kernel, userKernelInfo);
            
            this._logger.LogInformation("Created kernel for user {UserId} with context {ContextId}", 
                userId, config.ContextId);
            
            return this.Ok(response);
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error creating kernel for user {UserId}", this._authInfo.UserId);
            return this.StatusCode(StatusCodes.Status500InternalServerError, 
                new { error = "Failed to create kernel" });
        }
    }

    /// <summary>
    /// Release a specific user's kernel.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="contextId">Optional context ID. If not provided, releases the default context.</param>
    /// <param name="releaseAllContexts">Whether to release all contexts for the user.</param>
    /// <returns>A success indication.</returns>
    [Route("api/kernel/release/{userId}")]
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ReleaseUserKernel(
        string userId, 
        [FromQuery] string? contextId = null, 
        [FromQuery] bool releaseAllContexts = false)
    {
        try
        {
            // Ensure only the user themselves or an admin can do this
            if (userId != this._authInfo.UserId && !IsAdminUser())
            {
                return this.Forbid();
            }
            
            await this._kernelManager.ReleaseUserKernelAsync(userId, contextId, releaseAllContexts);
            
            this._logger.LogInformation(
                "Released kernel for user {UserId} with context {ContextId}, releaseAllContexts: {ReleaseAllContexts}", 
                userId, contextId ?? "default", releaseAllContexts);
            
            return this.NoContent();
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error releasing kernel for user {UserId} with context {ContextId}", 
                userId, contextId ?? "default");
            return this.StatusCode(StatusCodes.Status500InternalServerError, 
                new { error = $"Failed to release kernel for user {userId} with context {contextId ?? "default"}" });
        }
    }

    /// <summary>
    /// Clear all kernels in the system.
    /// </summary>
    /// <returns>A success indication.</returns>
    [Route("api/kernel/clear")]
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ClearAllKernels()
    {
        try
        {
            // Ensure only an admin can do this
            if (!IsAdminUser())
            {
                return this.Forbid();
            }
            
            await this._kernelManager.ClearAllKernelsAsync();
            
            this._logger.LogInformation("Cleared all kernels");
            
            return this.NoContent();
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error clearing all kernels");
            return this.StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to clear all kernels" });
        }
    }

    #region Helper Methods

    /// <summary>
    /// Check if the current user is an admin.
    /// </summary>
    /// <returns>True if the current user is an admin, false otherwise.</returns>
    private bool IsAdminUser()
    {
        // TODO: Implement actual admin check logic 
        // For now, we'll assume all users are admins for development purposes
        return true;
    }

    /// <summary>
    /// Create a KernelInfoResponse from a kernel and user kernel info.
    /// </summary>
    /// <param name="kernel">The kernel.</param>
    /// <param name="userKernelInfo">The user kernel info.</param>
    /// <returns>A KernelInfoResponse.</returns>
    private KernelInfoResponse CreateKernelInfoResponse(Kernel kernel, UserKernelInfo userKernelInfo)
    {
        var plugins = new List<KernelPluginInfo>();
        
        // Get plugin information
        try
        {
            // Use reflection to get plugin names since the API differs between versions
            var pluginsProperty = kernel.GetType().GetProperty("Plugins");
            if (pluginsProperty != null)
            {
                var pluginsCollection = pluginsProperty.GetValue(kernel);
                if (pluginsCollection != null)
                {
                    // Try to get plugin names using reflection
                    var pluginNames = new List<string>();
                    var pluginsType = pluginsCollection.GetType();
                    
                    // Try to find a method that returns plugin names
                    MethodInfo? getPluginNamesMethod = pluginsType.GetMethod("GetPluginNames");
                    if (getPluginNamesMethod != null)
                    {
                        var names = getPluginNamesMethod.Invoke(pluginsCollection, null);
                        if (names is IEnumerable<string> namesList)
                        {
                            pluginNames.AddRange(namesList);
                        }
                    }
                    else
                    {
                        // Alternative approach: try to access the _plugins field directly
                        var pluginsField = pluginsType.GetField("_plugins", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (pluginsField != null)
                        {
                            var pluginsDict = pluginsField.GetValue(pluginsCollection);
                            if (pluginsDict is IDictionary<string, object> pluginsDictionary)
                            {
                                pluginNames.AddRange(pluginsDictionary.Keys);
                            }
                        }
                    }
                    
                    // For each plugin name, try to get function names
                    foreach (var pluginName in pluginNames)
                    {
                        var functions = new List<string>();
                        
                        // Try to get functions using GetFunctionsMetadata method
                        var getFunctionsMethod = pluginsType.GetMethod("GetFunctionsMetadata", new Type[] { typeof(string) });
                        if (getFunctionsMethod != null)
                        {
                            var functionsMetadata = getFunctionsMethod.Invoke(pluginsCollection, new object[] { pluginName });
                            if (functionsMetadata is IEnumerable<KernelFunctionMetadata> metadataList)
                            {
                                functions.AddRange(metadataList.Select(m => m.Name));
                            }
                        }
                        
                        plugins.Add(new KernelPluginInfo
                        {
                            Name = pluginName,
                            Functions = functions
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            this._logger.LogWarning(ex, "Error getting plugin information");
        }

        // Try to get model information
        var modelInfo = new KernelModelInfo();

        try 
        {
            // Try to get the chat completion service via reflection
            var servicesProperty = kernel.GetType().GetProperty("Services");
            if (servicesProperty != null)
            {
                var services = servicesProperty.GetValue(kernel);
                if (services != null)
                {
                    // Try to get the chat completion service
                    var chatCompletionServiceType = Type.GetType("Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService, Microsoft.SemanticKernel");
                    var embeddingServiceType = Type.GetType("Microsoft.SemanticKernel.Embeddings.ITextEmbeddingGenerationService, Microsoft.SemanticKernel");
                    
                    if (chatCompletionServiceType != null)
                    {
                        // Try to get the GetRequiredService method
                        var getServiceMethod = services.GetType().GetMethod("GetRequiredService", 
                            BindingFlags.Public | BindingFlags.Instance, 
                            null, 
                            new Type[] { }, 
                            null);
                            
                        if (getServiceMethod != null)
                        {
                            var genericMethod = getServiceMethod.MakeGenericMethod(chatCompletionServiceType);
                            try
                            {
                                var chatService = genericMethod.Invoke(services, null);
                                if (chatService != null)
                                {
                                    var type = chatService.GetType();
                                    var modelIdField = type.GetField("_modelId", BindingFlags.Instance | BindingFlags.NonPublic);
                                    if (modelIdField != null)
                                    {
                                        modelInfo.CompletionModelId = modelIdField.GetValue(chatService)?.ToString() ?? string.Empty;
                                    }
                                    
                                    // Try to find temperature and max tokens in the type or its base types
                                    PropertyInfo? tempProp = null;
                                    PropertyInfo? maxTokensProp = null;
                                    
                                    var currentType = type;
                                    while (currentType != null && (tempProp == null || maxTokensProp == null))
                                    {
                                        if (tempProp == null)
                                        {
                                            tempProp = currentType.GetProperty("Temperature", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                        }
                                        
                                        if (maxTokensProp == null)
                                        {
                                            maxTokensProp = currentType.GetProperty("MaxTokens", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                        }
                                        
                                        currentType = currentType.BaseType;
                                    }
                                    
                                    if (tempProp != null)
                                    {
                                        var temp = tempProp.GetValue(chatService);
                                        if (temp != null)
                                        {
                                            modelInfo.Temperature = Convert.ToSingle(temp, CultureInfo.InvariantCulture);
                                        }
                                    }
                                    
                                    if (maxTokensProp != null)
                                    {
                                        var maxTokens = maxTokensProp.GetValue(chatService);
                                        if (maxTokens != null)
                                        {
                                            modelInfo.MaxTokens = Convert.ToInt32(maxTokens, CultureInfo.InvariantCulture);
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                this._logger.LogWarning(ex, "Error extracting chat service information");
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            this._logger.LogWarning(ex, "Error extracting model information");
        }

        return new KernelInfoResponse
        {
            UserId = userKernelInfo.UserId,
            ContextId = userKernelInfo.ContextId,
            LastAccessTime = userKernelInfo.LastAccessTime,
            Plugins = plugins,
            ModelInfo = modelInfo
        };
    }

    /// <summary>
    /// Get the dictionary of user kernels from the kernel manager using reflection.
    /// </summary>
    /// <returns>The dictionary of user kernels, or null if it cannot be accessed.</returns>
    private ConcurrentDictionary<string, UserKernelInfo>? GetUserKernelsFromManager()
    {
        try
        {
            var fieldInfo = typeof(KernelManager).GetField("_userKernels", BindingFlags.NonPublic | BindingFlags.Instance);
            if (fieldInfo == null)
            {
                this._logger.LogWarning("Could not find _userKernels field in KernelManager");
                return null;
            }
            
            return fieldInfo.GetValue(this._kernelManager) as ConcurrentDictionary<string, UserKernelInfo>;
        }
        catch (Exception ex)
        {
            this._logger.LogWarning(ex, "Error accessing user kernels via reflection");
            return null;
        }
    }

    /// <summary>
    /// Get a specific UserKernelInfo from the kernel manager using reflection.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="contextId">The context ID.</param>
    /// <returns>The UserKernelInfo, or null if it cannot be accessed.</returns>
    private UserKernelInfo? GetUserKernelInfoFromManager(string userId, string? contextId)
    {
        try
        {
            var userKernels = GetUserKernelsFromManager();
            if (userKernels == null)
            {
                return null;
            }
            
            contextId ??= "default";
            string key = $"{userId}:{contextId}";
            
            userKernels.TryGetValue(key, out var userKernelInfo);
            return userKernelInfo;
        }
        catch (Exception ex)
        {
            this._logger.LogWarning(ex, "Error accessing user kernel info via reflection");
            return null;
        }
    }

    #endregion
}