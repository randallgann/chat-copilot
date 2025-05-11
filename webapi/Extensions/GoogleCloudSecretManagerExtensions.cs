// Copyright (c) Microsoft. All rights reserved.

using Google.Apis.Auth.OAuth2;
using Google.Cloud.SecretManager.V1;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CopilotChat.WebApi.Extensions;

/// <summary>
/// Extension methods for Google Cloud Secret Manager integration
/// </summary>
public static class GoogleCloudSecretManagerExtensions
{
    /// <summary>
    /// Add Google Cloud Secret Manager as a configuration source
    /// </summary>
    /// <param name="configBuilder">The configuration builder</param>
    /// <param name="projectId">Google Cloud project ID</param>
    /// <param name="logger">Optional logger</param>
    /// <returns>True if a valid API key was successfully retrieved and added to the configuration, false otherwise</returns>
    public static bool AddGoogleCloudSecretManager(
        this IConfigurationBuilder configBuilder,
        string projectId,
        ILogger? logger = null)
    {
        if (string.IsNullOrEmpty(projectId))
        {
            logger?.LogWarning("GCP ProjectId is empty. Skipping Google Cloud Secret Manager integration.");
            return false; // No valid API key retrieved
        }

        try
        {
            // Check if we have a key file path configured
            string? keyFilePath = configBuilder.Build().GetValue<string>("GCP:ServiceAccountKeyPath");

            logger?.LogInformation("Fetching secrets from Google Cloud Secret Manager for project {ProjectId}", projectId);

            // Create the Secret Manager client with the appropriate credentials
            SecretManagerServiceClient client;

            if (!string.IsNullOrEmpty(keyFilePath))
            {
                logger?.LogInformation("Using service account key file from: {KeyFilePath}", keyFilePath);

                try
                {
                    // Check if file exists
                    if (!File.Exists(keyFilePath))
                    {
                        logger?.LogWarning("Service account key file not found at: {KeyFilePath}", keyFilePath);
                        Console.WriteLine($"Service account key file not found at: {keyFilePath}");
                        return false; // No valid API key retrieved
                    }

                    // Use the service account key file for authentication
                    var credential = GoogleCredential.FromFile(keyFilePath)
                        .CreateScoped(SecretManagerServiceClient.DefaultScopes);

                    client = new SecretManagerServiceClientBuilder
                    {
                        Credential = credential
                    }.Build();
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Error loading service account key file from {KeyFilePath}", keyFilePath);
                    Console.WriteLine($"Error loading service account key file: {ex.Message}");
                    return false; // No valid API key retrieved
                }
            }
            else
            {
                // Fall back to default credentials - for local development
                logger?.LogInformation("No service account key file specified, using default credentials");
                client = new SecretManagerServiceClientBuilder().Build();
            }

            // Add OpenAI API key from Secret Manager
            // Check if we have an explicit secret path from environment
            string? explicitSecretPath = Environment.GetEnvironmentVariable("GCP__SecretPath");

            // Use the explicit path or build the standard path
            var secretName = !string.IsNullOrEmpty(explicitSecretPath)
                ? explicitSecretPath
                : $"projects/{projectId}/secrets/OPENAI_API_KEY/versions/latest";

            Console.WriteLine($"Attempting to access secret: {secretName}");

            try {
                var response = client.AccessSecretVersion(secretName);
                var openAiApiKey = response.Payload.Data.ToStringUtf8();

                // Debug output - safely show part of the key to confirm it's the right format (sk-...)
                // Never log the full API key
                string keyForLogging = string.IsNullOrEmpty(openAiApiKey) ?
                    "<empty>" :
                    (openAiApiKey.Length > 10 ?
                        $"{openAiApiKey.Substring(0, 5)}...{openAiApiKey.Substring(openAiApiKey.Length - 3)}" :
                        "key too short");

                Console.WriteLine($"Retrieved API key from GCP Secret Manager: {keyForLogging}");

                // Check for placeholder or invalid key patterns
                if (openAiApiKey.Contains("your-") || openAiApiKey.Contains("placeholder") || openAiApiKey.Contains("dummy")) {
                    Console.WriteLine($"WARNING: The retrieved API key appears to be a placeholder: {keyForLogging}");
                    Console.WriteLine("Please set a real OpenAI API key in GCP Secret Manager.");
                    return false; // No valid API key retrieved
                }

                // Add the secret to the configuration if it has the correct format
                if (!string.IsNullOrEmpty(openAiApiKey) && (openAiApiKey.StartsWith("sk-") || openAiApiKey.StartsWith("org-")))
                {
                    logger?.LogInformation("Successfully retrieved OpenAI API key from GCP Secret Manager");
                    Console.WriteLine("Successfully retrieved valid OpenAI API key from GCP Secret Manager");

                    var secrets = new Dictionary<string, string>
                    {
                        // Set the key in the same format expected by the application
                        { "KernelMemory:Services:OpenAI:APIKey", openAiApiKey }
                    };

                    configBuilder.AddInMemoryCollection(secrets);
                    return true; // Valid API key successfully retrieved and added
                }
                else
                {
                    Console.WriteLine($"WARNING: Retrieved key doesn't match expected OpenAI API key format (should start with 'sk-'): {keyForLogging}");
                    return false; // No valid API key retrieved
                }
            }
            catch (Exception ex) {
                Console.WriteLine($"Error accessing specific secret version: {ex.Message}");

                // Try listing available secrets for debugging
                try {
                    Console.WriteLine($"Listing available secrets in project {projectId}...");
                    var secretManagerServiceClient = new SecretManagerServiceClientBuilder().Build();
                    var projectName = $"projects/{projectId}";
                    var availableSecrets = secretManagerServiceClient.ListSecrets(projectName);

                    Console.WriteLine("Available secrets:");
                    foreach (var secret in availableSecrets)
                    {
                        Console.WriteLine($"- {secret.Name}");
                    }
                } catch (Exception listEx) {
                    Console.WriteLine($"Error listing secrets: {listEx.Message}");
                }
                return false; // Error accessing secret
            }

            return false; // No valid API key retrieved
        }
        catch (Exception ex)
        {
            // Log the error but don't throw - allow the app to start
            // and potentially use other configuration sources
            var message = $"Error accessing Google Cloud Secret Manager: {ex.Message}";
            logger?.LogError(ex, message);
            Console.WriteLine(message);
            return false; // Error accessing GCP Secret Manager
        }
    }

    /// <summary>
    /// Fallback to use a direct key for development/testing when Secret Manager is unavailable
    /// </summary>
    public static IConfigurationBuilder AddFallbackOpenAIKey(
        this IConfigurationBuilder configBuilder,
        ILogger? logger = null)
    {
        try
        {
            Console.WriteLine("Checking for fallback OpenAI API key sources...");

            // First check environment variables
            string? envKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            Console.WriteLine($"Environment variable OPENAI_API_KEY exists: {!string.IsNullOrEmpty(envKey)}");

            if (!string.IsNullOrEmpty(envKey))
            {
                // Verify key format - just for logging
                string keyForLogging = envKey.Length > 10 ?
                    $"{envKey.Substring(0, 5)}...{envKey.Substring(envKey.Length - 3)}" :
                    "key too short";

                logger?.LogWarning("Using OpenAI API key from environment variable");
                Console.WriteLine($"Using OpenAI API key from environment variable: {keyForLogging}");

                var secrets = new Dictionary<string, string>
                {
                    { "KernelMemory:Services:OpenAI:APIKey", envKey }
                };

                configBuilder.AddInMemoryCollection(secrets);
                return configBuilder;
            }

            // Then look for fallback direct key in configs
            var config = configBuilder.Build();
            string? directKey = config.GetValue<string>("OpenAI:DirectKey");

            if (string.IsNullOrEmpty(directKey))
            {
                directKey = config.GetValue<string>("KernelMemory:Services:OpenAI:APIKey");
            }

            if (!string.IsNullOrEmpty(directKey))
            {
                logger?.LogWarning("Using direct OpenAI key from configuration (for development only)");
                Console.WriteLine("Using direct OpenAI key from configuration");

                var secrets = new Dictionary<string, string>
                {
                    { "KernelMemory:Services:OpenAI:APIKey", directKey }
                };

                configBuilder.AddInMemoryCollection(secrets);
                return configBuilder;
            }

            return configBuilder;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error applying fallback OpenAI key");
            Console.WriteLine($"Error applying fallback OpenAI key: {ex.Message}");
            return configBuilder;
        }
    }
}