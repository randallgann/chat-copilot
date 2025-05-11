// Copyright (c) Microsoft. All rights reserved.

using System.Reflection;
using Azure.Identity;

namespace CopilotChat.WebApi.Extensions;

internal static class ConfigExtensions
{
    /// <summary>
    /// Build the configuration for the service.
    /// </summary>
    public static IHostBuilder AddConfiguration(this IHostBuilder host)
    {
        string? environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        host.ConfigureAppConfiguration((builderContext, configBuilder) =>
        {
            configBuilder.AddJsonFile(
                path: "appsettings.json",
                optional: false,
                reloadOnChange: true);

            configBuilder.AddJsonFile(
                path: $"appsettings.{environment}.json",
                optional: true,
                reloadOnChange: true);

            configBuilder.AddEnvironmentVariables();

            configBuilder.AddUserSecrets(
                assembly: Assembly.GetExecutingAssembly(),
                optional: true,
                reloadOnChange: true);

            bool gcpKeyRetrieved = false;

            // Add Google Cloud Secret Manager integration
            string? gcpProjectId = builderContext.Configuration["GCP:ProjectId"];
            if (!string.IsNullOrWhiteSpace(gcpProjectId))
            {
                // Since we don't have access to the logger service yet,
                // we'll log at the console level for now
                Console.WriteLine($"Configuring Google Cloud Secret Manager with project ID: {gcpProjectId}");
                gcpKeyRetrieved = configBuilder.AddGoogleCloudSecretManager(gcpProjectId);
            }

            // Add fallback OpenAI key ONLY if Secret Manager failed or is not configured
            if (!gcpKeyRetrieved)
            {
                Console.WriteLine("GCP Secret Manager key retrieval unsuccessful, trying fallback methods...");
                configBuilder.AddFallbackOpenAIKey();
            }
            else
            {
                Console.WriteLine("Successfully retrieved key from GCP Secret Manager, skipping fallbacks");
            }

            // For settings from Key Vault, see https://learn.microsoft.com/en-us/aspnet/core/security/key-vault-configuration?view=aspnetcore-8.0
            string? keyVaultUri = builderContext.Configuration["Service:KeyVault"];
            if (!string.IsNullOrWhiteSpace(keyVaultUri))
            {
                configBuilder.AddAzureKeyVault(
                    new Uri(keyVaultUri),
                    new DefaultAzureCredential());

                // for more information on how to use DefaultAzureCredential, see https://learn.microsoft.com/en-us/dotnet/api/azure.identity.defaultazurecredential?view=azure-dotnet
            }
        });

        return host;
    }
}
