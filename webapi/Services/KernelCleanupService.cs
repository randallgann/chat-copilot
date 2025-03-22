// Copyright (c) Microsoft. All rights reserved.

using CopilotChat.WebApi.Models.Kernel;
using System.Reflection;

namespace CopilotChat.WebApi.Services;

/// <summary>
/// Background service that cleans up inactive kernels.
/// </summary>
public class KernelCleanupService : BackgroundService
{
    private readonly IKernelManager _kernelManager;
    private readonly ILogger<KernelCleanupService> _logger;
    private readonly TimeSpan _cleanupInterval;
    private readonly TimeSpan _kernelMaxInactiveTime;

    /// <summary>
    /// Initializes a new instance of the KernelCleanupService class.
    /// </summary>
    /// <param name="kernelManager">The kernel manager.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="configuration">The configuration.</param>
    public KernelCleanupService(
        IKernelManager kernelManager,
        ILogger<KernelCleanupService> logger,
        IConfiguration configuration)
    {
        this._kernelManager = kernelManager;
        this._logger = logger;
        
        // Get configuration values or use defaults
        this._cleanupInterval = TimeSpan.FromMinutes(
            configuration.GetValue("KernelManagement:CleanupIntervalMinutes", 30));
        
        this._kernelMaxInactiveTime = TimeSpan.FromMinutes(
            configuration.GetValue("KernelManagement:MaxInactiveTimeMinutes", 60));
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        this._logger.LogInformation("Kernel cleanup service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(this._cleanupInterval, stoppingToken);
                await this.CleanupKernelsAsync();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown
                break;
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Error during kernel cleanup");
            }
        }

        this._logger.LogInformation("Kernel cleanup service stopped");
    }

    /// <summary>
    /// Clean up inactive kernels.
    /// </summary>
    private async Task CleanupKernelsAsync()
    {
        this._logger.LogInformation("Starting kernel cleanup");
        
        try
        {
            // Use reflection to access the private _userKernels field
            // This is not ideal but avoids modifying the IKernelManager interface for this cleanup purpose
            var kernelManagerType = this._kernelManager.GetType();
            var userKernelsField = kernelManagerType.GetField("_userKernels", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (userKernelsField == null)
            {
                this._logger.LogWarning("Could not find _userKernels field in KernelManager");
                return;
            }

            var userKernels = userKernelsField.GetValue(this._kernelManager) as 
                System.Collections.Concurrent.ConcurrentDictionary<string, UserKernelInfo>;
            
            if (userKernels == null)
            {
                this._logger.LogWarning("Could not access userKernels dictionary");
                return;
            }
            
            var now = DateTime.UtcNow;
            var inactiveKeys = new List<string>();
            
            // Find inactive kernels
            foreach (var kv in userKernels)
            {
                var key = kv.Key;
                var info = kv.Value;
                
                var inactiveTime = now - info.LastAccessTime;
                if (inactiveTime > this._kernelMaxInactiveTime)
                {
                    this._logger.LogInformation("Kernel {Key} has been inactive for {InactiveTime}, cleaning up", 
                        key, inactiveTime);
                    inactiveKeys.Add(key);
                }
            }
            
            // Remove inactive kernels
            if (inactiveKeys.Count > 0)
            {
                this._logger.LogInformation("Cleaning up {Count} inactive kernels", inactiveKeys.Count);
                
                // Parse keys to get userId and contextId
                foreach (var key in inactiveKeys)
                {
                    string[] parts = key.Split(':');
                    if (parts.Length == 2)
                    {
                        string userId = parts[0];
                        string contextId = parts[1];
                        
                        await this._kernelManager.ReleaseUserKernelAsync(userId, contextId);
                    }
                }
            }
            else
            {
                this._logger.LogInformation("No inactive kernels found");
            }
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error during kernel cleanup");
        }
    }
}