// Copyright (c) Microsoft. All rights reserved.

using CopilotChat.WebApi.Models.Kernel;

namespace CopilotChat.WebApi.Storage;

/// <summary>
/// Repository for user kernel configurations.
/// </summary>
public class UserKernelConfigRepository : Repository<UserKernelConfig>, IUserKernelConfigRepository
{
    /// <summary>
    /// Initializes a new instance of the UserKernelConfigRepository class.
    /// </summary>
    /// <param name="storageContext">The storage context.</param>
    public UserKernelConfigRepository(IStorageContext<UserKernelConfig> storageContext)
        : base(storageContext)
    {
    }

    /// <inheritdoc/>
    public async Task<UserKernelConfig?> GetConfigAsync(string userId, string? contextId = null)
    {
        contextId ??= "default";
        var configs = await this.FindByUserIdAndContextIdAsync(userId, contextId);
        return configs.FirstOrDefault();
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<UserKernelConfig>> GetUserConfigsAsync(string userId)
    {
        return await this.FindByUserIdAsync(userId);
    }

    /// <inheritdoc/>
    public async Task SaveConfigAsync(UserKernelConfig config)
    {
        if (string.IsNullOrEmpty(config.ContextId))
        {
            config.ContextId = "default";
        }

        var existingConfig = await this.GetConfigAsync(config.UserId, config.ContextId);
        if (existingConfig != null)
        {
            config.Id = existingConfig.Id;
            config.CreatedOn = existingConfig.CreatedOn;
            config.UpdatedOn = DateTimeOffset.UtcNow;
            await this.UpsertAsync(config);
        }
        else
        {
            config.Id = Guid.NewGuid().ToString();
            config.CreatedOn = DateTimeOffset.UtcNow;
            config.UpdatedOn = DateTimeOffset.UtcNow;
            await this.CreateAsync(config);
        }
    }

    /// <inheritdoc/>
    public async Task DeleteConfigAsync(string userId, string? contextId = null)
    {
        if (contextId == null)
        {
            // Delete only the default context
            var config = await this.GetConfigAsync(userId, "default");
            if (config != null)
            {
                await this.DeleteAsync(config);
            }
        }
        else
        {
            // Delete the specific context
            var config = await this.GetConfigAsync(userId, contextId);
            if (config != null)
            {
                await this.DeleteAsync(config);
            }
        }
    }

    /// <inheritdoc/>
    public async Task DeleteAllUserConfigsAsync(string userId)
    {
        var configs = await this.FindByUserIdAsync(userId);
        foreach (var config in configs)
        {
            await this.DeleteAsync(config);
        }
    }

    /// <summary>
    /// Find configs by user ID.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns>The configs for the specified user.</returns>
    private Task<IEnumerable<UserKernelConfig>> FindByUserIdAsync(string userId)
    {
        return this.StorageContext.QueryEntitiesAsync(c => c.UserId == userId);
    }

    /// <summary>
    /// Find configs by user ID and context ID.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="contextId">The context ID.</param>
    /// <returns>The configs for the specified user and context.</returns>
    private Task<IEnumerable<UserKernelConfig>> FindByUserIdAndContextIdAsync(string userId, string contextId)
    {
        return this.StorageContext.QueryEntitiesAsync(c => c.UserId == userId && c.ContextId == contextId);
    }
}