// Copyright (c) Microsoft. All rights reserved.

using CopilotChat.WebApi.Models.Storage;

namespace CopilotChat.WebApi.Storage;

/// <summary>
/// A repository for chat sessions.
/// </summary>
public class ChatSessionRepository : Repository<ChatSession>
{
    private readonly ChatParticipantRepository _participantRepository;

    /// <summary>
    /// Initializes a new instance of the ChatSessionRepository class.
    /// </summary>
    /// <param name="storageContext">The storage context.</param>
    /// <param name="participantRepository">The chat participant repository.</param>
    public ChatSessionRepository(
        IStorageContext<ChatSession> storageContext,
        ChatParticipantRepository participantRepository)
        : base(storageContext)
    {
        this._participantRepository = participantRepository;
    }

    /// <summary>
    /// Retrieves all chat sessions.
    /// </summary>
    /// <returns>A list of ChatMessages.</returns>
    public Task<IEnumerable<ChatSession>> GetAllChatsAsync()
    {
        return base.StorageContext.QueryEntitiesAsync(e => true);
    }

    /// <summary>
    /// Find chat sessions by user ID and context ID.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="contextId">The context ID (e.g., channelId).</param>
    /// <returns>A list of chat sessions matching the criteria.</returns>
    public async Task<IEnumerable<ChatSession>> FindByUserIdAndContextIdAsync(string userId, string contextId)
    {
        // First, get all chat sessions for this user through the participants
        var participants = await this._participantRepository.FindByUserIdAsync(userId);
        
        // Then filter by context ID
        var result = new List<ChatSession>();
        foreach (var participant in participants)
        {
            // Get the chat session
            ChatSession? chat = null;
            if (await this.TryFindByIdAsync(participant.ChatId, callback: c => chat = c))
            {
                // Check if the chat has the matching context ID
                if (chat!.ContextId == contextId)
                {
                    result.Add(chat);
                }
            }
        }
        
        return result;
    }
}
