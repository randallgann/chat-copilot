// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Concurrent;
using System.Diagnostics;
using CopilotChat.WebApi.Models.Storage;

namespace CopilotChat.WebApi.Storage;

/// <summary>
/// A storage context that stores entities in memory.
/// </summary>
[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public class VolatileContext<T> : IStorageContext<T> where T : IStorageEntity
{
    /// <summary>
    /// Using a concurrent dictionary to store entities in memory.
    /// </summary>
#pragma warning disable CA1051 // Do not declare visible instance fields
    protected readonly ConcurrentDictionary<string, T> Entities;
#pragma warning restore CA1051 // Do not declare visible instance fields

    /// <summary>
    /// Initializes a new instance of the InMemoryContext class.
    /// </summary>
    public VolatileContext()
    {
        this.Entities = new ConcurrentDictionary<string, T>();
    }

    /// <inheritdoc/>
    public Task<IEnumerable<T>> QueryEntitiesAsync(Func<T, bool> predicate)
    {
        return Task.FromResult(this.Entities.Values.Where(predicate));
    }

    /// <inheritdoc/>
    public Task CreateAsync(T entity)
    {
        if (string.IsNullOrWhiteSpace(entity.Id))
        {
            throw new ArgumentOutOfRangeException(nameof(entity), "Entity Id cannot be null or empty.");
        }

        this.Entities.TryAdd(entity.Id, entity);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task DeleteAsync(T entity)
    {
        if (string.IsNullOrWhiteSpace(entity.Id))
        {
            throw new ArgumentOutOfRangeException(nameof(entity), "Entity Id cannot be null or empty.");
        }

        this.Entities.TryRemove(entity.Id, out _);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<T> ReadAsync(string entityId, string partitionKey)
    {
        if (string.IsNullOrWhiteSpace(entityId))
        {
            throw new ArgumentOutOfRangeException(nameof(entityId), "Entity Id cannot be null or empty.");
        }

        if (this.Entities.TryGetValue(entityId, out T? entity))
        {
            return Task.FromResult(entity);
        }

        throw new KeyNotFoundException($"Entity with id {entityId} not found.");
    }

    /// <inheritdoc/>
    public Task UpsertAsync(T entity)
    {
        if (string.IsNullOrWhiteSpace(entity.Id))
        {
            throw new ArgumentOutOfRangeException(nameof(entity), "Entity Id cannot be null or empty.");
        }

        this.Entities.AddOrUpdate(entity.Id, entity, (key, oldValue) => entity);

        return Task.CompletedTask;
    }

    private string GetDebuggerDisplay()
    {
        return this.ToString() ?? string.Empty;
    }
}

/// <summary>
/// Specialization of VolatileContext<T> for CopilotChatMessage.
/// </summary>
public class VolatileCopilotChatMessageContext : VolatileContext<CopilotChatMessage>, ICopilotChatMessageStorageContext
{
    /// <inheritdoc/>
    public Task<IEnumerable<CopilotChatMessage>> QueryEntitiesAsync(Func<CopilotChatMessage, bool> predicate, int skip, int count)
    {
        Console.WriteLine($"DEBUG VolatileContext: QueryEntitiesAsync called with skip={skip}, count={count}");
        
        // Create intermediate results to debug the stages of the pipeline
        var allEntities = this.Entities.Values.ToList();
        Console.WriteLine($"DEBUG VolatileContext: Total entities: {allEntities.Count}");
        
        var filteredEntities = allEntities.Where(predicate).ToList();
        Console.WriteLine($"DEBUG VolatileContext: After filtering: {filteredEntities.Count}");
        
        var orderedEntities = filteredEntities.OrderByDescending(m => m.Timestamp).ToList();
        Console.WriteLine($"DEBUG VolatileContext: After ordering: {orderedEntities.Count}");
        
        var skippedEntities = orderedEntities.Skip(skip).ToList();
        Console.WriteLine($"DEBUG VolatileContext: After skipping {skip}: {skippedEntities.Count}");
        
        IEnumerable<CopilotChatMessage> result;
        if (count >= 0)
        {
            result = skippedEntities.Take(count).ToList();
            Console.WriteLine($"DEBUG VolatileContext: After taking {count}: {(result as List<CopilotChatMessage>)?.Count ?? 0}");
        }
        else
        {
            result = skippedEntities;
            Console.WriteLine($"DEBUG VolatileContext: Taking all (count={count}): {skippedEntities.Count}");
        }
        
        // Return result inside Task.Run to maintain the same behavior
        return Task.FromResult(result);
    }
}
