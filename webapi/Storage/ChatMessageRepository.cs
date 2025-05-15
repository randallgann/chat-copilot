// Copyright (c) Microsoft. All rights reserved.

using CopilotChat.WebApi.Models.Storage;

namespace CopilotChat.WebApi.Storage;

/// <summary>
/// A repository for chat messages.
/// </summary>
public class ChatMessageRepository : CopilotChatMessageRepository
{
    /// <summary>
    /// Initializes a new instance of the ChatMessageRepository class.
    /// </summary>
    /// <param name="storageContext">The storage context.</param>
    public ChatMessageRepository(ICopilotChatMessageStorageContext storageContext)
        : base(storageContext)
    {
    }

    /// <summary>
    /// Finds chat messages by chat id.
    /// </summary>
    /// <param name="chatId">The chat id.</param>
    /// <param name="skip">Number of messages to skip before starting to return messages.</param>
    /// <param name="count">The number of messages to return. -1 returns all messages.</param>
    /// <returns>A list of ChatMessages matching the given chatId sorted from most recent to oldest.</returns>
    public Task<IEnumerable<CopilotChatMessage>> FindByChatIdAsync(string chatId, int skip = 0, int count = -1)
    {
        // Debug: Log the chatId parameter
        Console.WriteLine($"DEBUG: FindByChatIdAsync called with chatId='{chatId}', skip={skip}, count={count}");

        // Access the storage context directly to debug the entities
        var storageContext = this.StorageContext as ICopilotChatMessageStorageContext;
        if (storageContext != null)
        {
            // Examine the underlying collection via reflection to see what's there
            var volatileContext = storageContext as VolatileCopilotChatMessageContext;
            if (volatileContext != null)
            {
                var entitiesField = typeof(VolatileContext<CopilotChatMessage>).GetField("Entities",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

                if (entitiesField != null)
                {
                    var entities = entitiesField.GetValue(volatileContext) as System.Collections.Concurrent.ConcurrentDictionary<string, CopilotChatMessage>;

                    if (entities != null)
                    {
                        Console.WriteLine($"DEBUG: Repository contains {entities.Count} total messages");

                        foreach (var entity in entities.Values)
                        {
                            bool matches = entity.ChatId == chatId;
                            Console.WriteLine($"DEBUG: Entity found: id='{entity.Id}', chatId='{entity.ChatId}', match={matches}");

                            if (!matches && entity.ChatId.Equals(chatId, StringComparison.OrdinalIgnoreCase))
                            {
                                Console.WriteLine($"DEBUG: Case-insensitive match found but case-sensitive failed!");
                            }
                        }

                        // Get results from the predicate
                        var results = entities.Values.Where(e => e.ChatId == chatId).ToList();
                        Console.WriteLine($"DEBUG: Found {results.Count} matching messages for chatId '{chatId}'");
                    }
                    else
                    {
                        Console.WriteLine("DEBUG: Could not access entities dictionary");
                    }
                }
                else
                {
                    Console.WriteLine("DEBUG: Could not find Entities field via reflection");
                }
            }
            else
            {
                Console.WriteLine("DEBUG: Storage context is not a VolatileCopilotChatMessageContext");
            }
        }
        else
        {
            Console.WriteLine("DEBUG: Storage context is not an ICopilotChatMessageStorageContext");
        }

        return base.QueryEntitiesAsync(e => e.ChatId == chatId, skip, count);
    }

    /// <summary>
    /// Finds the most recent chat message by chat id.
    /// </summary>
    /// <param name="chatId">The chat id.</param>
    /// <returns>The most recent ChatMessage matching the given chatId.</returns>
    public async Task<CopilotChatMessage> FindLastByChatIdAsync(string chatId)
    {
        var chatMessages = await this.FindByChatIdAsync(chatId, 0, 1);
        var first = chatMessages.MaxBy(e => e.Timestamp);
        return first ?? throw new KeyNotFoundException($"No messages found for chat '{chatId}'.");
    }
}
