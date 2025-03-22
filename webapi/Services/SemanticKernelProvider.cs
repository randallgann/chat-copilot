// Copyright (c) Microsoft. All rights reserved.

using CopilotChat.WebApi.Models.Kernel;
using Microsoft.Extensions.Options;
using Microsoft.KernelMemory;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace CopilotChat.WebApi.Services;

/// <summary>
/// Provider for creating and configuring Semantic Kernel instances.
/// </summary>
public sealed class SemanticKernelProvider
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly Kernel _defaultKernel;

    /// <summary>
    /// Initializes a new instance of the SemanticKernelProvider class.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    public SemanticKernelProvider(IServiceProvider serviceProvider, IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        this._serviceProvider = serviceProvider;
        this._configuration = configuration;
        this._httpClientFactory = httpClientFactory;
        this._defaultKernel = InitializeCompletionKernel(null);
    }

    /// <summary>
    /// Get a kernel with default configuration.
    /// </summary>
    /// <returns>A kernel with default configuration.</returns>
    public Kernel GetCompletionKernel() => this._defaultKernel.Clone();

    /// <summary>
    /// Get a kernel with user-specific configuration.
    /// </summary>
    /// <param name="userConfig">The user-specific kernel configuration.</param>
    /// <returns>A kernel with user-specific configuration.</returns>
    public Kernel GetCompletionKernel(UserKernelConfig userConfig) => InitializeCompletionKernel(userConfig);

    /// <summary>
    /// Initialize a kernel with user-specific or default configuration.
    /// </summary>
    /// <param name="userConfig">Optional user-specific kernel configuration.</param>
    /// <returns>An initialized kernel.</returns>
    private Kernel InitializeCompletionKernel(UserKernelConfig? userConfig)
    {
        var builder = Kernel.CreateBuilder();

        builder.Services.AddLogging();

        var memoryOptions = this._serviceProvider.GetRequiredService<IOptions<KernelMemoryConfig>>().Value;

        // Apply user config or use default
        var kernelConfig = ApplyDefaultOrUserConfig(userConfig);

        switch (memoryOptions.TextGeneratorType)
        {
            case string x when x.Equals("AzureOpenAI", StringComparison.OrdinalIgnoreCase):
            case string y when y.Equals("AzureOpenAIText", StringComparison.OrdinalIgnoreCase):
                var azureAIOptions = memoryOptions.GetServiceConfig<AzureOpenAIConfig>(this._configuration, "AzureOpenAIText");
                
                // Apply user-specific overrides if available
                var modelId = kernelConfig.CompletionOptions?.ModelId ?? azureAIOptions.Deployment;
                var endpoint = kernelConfig.CompletionOptions?.Endpoint ?? azureAIOptions.Endpoint;
                var apiKey = kernelConfig.ApiKeys.TryGetValue("AzureOpenAI", out var key) ? key : azureAIOptions.APIKey;
                
#pragma warning disable CA2000 // No need to dispose of HttpClient instances from IHttpClientFactory
                var httpClient = this._httpClientFactory.CreateClient();
                
                // Create execution settings using the correct namespace
                // But we don't need to pass these explicitly as the AddAzureOpenAIChatCompletion method 
                // doesn't take them directly anymore in the newer version of SK
                
                // Ensure we have a valid model ID
                if (string.IsNullOrWhiteSpace(modelId))
                {
                    modelId = "gpt-4"; // Default to gpt-4 if not specified
                }
                
                builder.AddAzureOpenAIChatCompletion(
                    modelId,
                    endpoint,
                    apiKey,
                    httpClient: httpClient);
                break;

            case string x when x.Equals("OpenAI", StringComparison.OrdinalIgnoreCase):
                var openAIOptions = memoryOptions.GetServiceConfig<OpenAIConfig>(this._configuration, "OpenAI");
                
                // Apply user-specific overrides if available
                var openAiModelId = kernelConfig.CompletionOptions?.ModelId ?? openAIOptions.TextModel;
                var openAiApiKey = kernelConfig.ApiKeys.TryGetValue("OpenAI", out var openAiKey) ? openAiKey : openAIOptions.APIKey;
                
                // Create execution settings using the correct namespace
                // But we don't need to pass these explicitly as the AddOpenAIChatCompletion method 
                // doesn't take them directly anymore in the newer version of SK
                
                // Ensure we have a valid model ID
                if (string.IsNullOrWhiteSpace(openAiModelId))
                {
                    openAiModelId = "gpt-4o"; // Default to gpt-4o if not specified
                }
                
                builder.AddOpenAIChatCompletion(
                    openAiModelId,
                    openAiApiKey,
                    httpClient: this._httpClientFactory.CreateClient());
#pragma warning restore CA2000
                break;

            default:
                throw new ArgumentException($"Invalid {nameof(memoryOptions.TextGeneratorType)} value in 'KernelMemory' settings.");
        }

        return builder.Build();
    }

    /// <summary>
    /// Apply user configuration or use default.
    /// </summary>
    /// <param name="userConfig">Optional user-specific kernel configuration.</param>
    /// <returns>The kernel configuration to use.</returns>
    private UserKernelConfig ApplyDefaultOrUserConfig(UserKernelConfig? userConfig)
    {
        if (userConfig != null)
        {
            // Use the user config but make sure all required properties are initialized
            userConfig.Settings ??= new Dictionary<string, object>();
            userConfig.CompletionOptions ??= new LLMOptions();
            userConfig.EmbeddingOptions ??= new LLMOptions();
            userConfig.EnabledPlugins ??= new List<string>();
            userConfig.ApiKeys ??= new Dictionary<string, string>();
            
            return userConfig;
        }
        
        // Otherwise create a default config
        return new UserKernelConfig
        {
            Settings = new Dictionary<string, object>(),
            CompletionOptions = new LLMOptions(),
            EmbeddingOptions = new LLMOptions(),
            EnabledPlugins = new List<string>(),
            ApiKeys = new Dictionary<string, string>()
        };
    }
}