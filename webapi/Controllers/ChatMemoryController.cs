// Copyright (c) Microsoft. All rights reserved.

using CopilotChat.WebApi.Auth;
using CopilotChat.WebApi.Extensions;
using CopilotChat.WebApi.Models.Request;
using CopilotChat.WebApi.Models.Storage;
using CopilotChat.WebApi.Options;
using CopilotChat.WebApi.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.KernelMemory;

namespace CopilotChat.WebApi.Controllers;

/// <summary>
/// Controller for retrieving kernel memory data of chat sessions.
/// </summary>
[ApiController]
public class ChatMemoryController : ControllerBase
{
    private readonly ILogger<ChatMemoryController> _logger;

    private readonly PromptsOptions _promptOptions;

    private readonly ChatSessionRepository _chatSessionRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatMemoryController"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="promptsOptions">The prompts options.</param>
    /// <param name="chatSessionRepository">The chat session repository.</param>
    private readonly IAuthInfo _authInfo;
    private readonly Services.QdrantCollectionManager _qdrantCollectionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatMemoryController"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="promptsOptions">The prompts options.</param>
    /// <param name="chatSessionRepository">The chat session repository.</param>
    /// <param name="authInfo">The authentication info.</param>
    /// <param name="qdrantCollectionManager">The Qdrant collection manager.</param>
    public ChatMemoryController(
        ILogger<ChatMemoryController> logger,
        IOptions<PromptsOptions> promptsOptions,
        ChatSessionRepository chatSessionRepository,
        IAuthInfo authInfo,
        Services.QdrantCollectionManager qdrantCollectionManager)
    {
        this._logger = logger;
        this._promptOptions = promptsOptions.Value;
        this._chatSessionRepository = chatSessionRepository;
        this._authInfo = authInfo;
        this._qdrantCollectionManager = qdrantCollectionManager;
    }

    /// <summary>
    /// Gets the kernel memory for the chat session.
    /// </summary>
    /// <param name="memoryClient">The kernel memory client.</param>
    /// <param name="chatId">The chat id.</param>
    /// <param name="type">Type of memory. Must map to a member of <see cref="SemanticMemoryType"/>.</param>
    [HttpGet]
    [Route("chats/{chatId:guid}/memories")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Authorize(Policy = AuthPolicyName.RequireChatParticipant)]
    public async Task<IActionResult> GetSemanticMemoriesAsync(
        [FromServices] IKernelMemory memoryClient,
        [FromRoute] string chatId,
        [FromQuery] string type)
    {
        // Sanitize the log input by removing new line characters.
        // https://github.com/microsoft/chat-copilot/security/code-scanning/1
        var sanitizedChatId = GetSanitizedParameter(chatId);
        var sanitizedMemoryType = GetSanitizedParameter(type);

        // Map the requested memoryType to the memory store container name
        if (!this._promptOptions.TryGetMemoryContainerName(type, out string memoryContainerName))
        {
            this._logger.LogWarning("Memory type: {0} is invalid.", sanitizedMemoryType);
            return this.BadRequest($"Memory type: {sanitizedMemoryType} is invalid.");
        }

        // Make sure the chat session exists.
        ChatSession? chatSession = null;
        if (!await this._chatSessionRepository.TryFindByIdAsync(chatId, callback: v => chatSession = v))
        {
            this._logger.LogWarning("Chat session: {0} does not exist.", sanitizedChatId);
            return this.BadRequest($"Chat session: {sanitizedChatId} does not exist.");
        }

        // Get user ID from auth info
        string userId = this._authInfo.UserId;
        // Get the context ID from the chat session
        string contextId = chatSession?.ContextId ?? "default";

        // Determine whether to use user-specific collection
        bool useUserCollection = !string.IsNullOrEmpty(userId);

        // Ensure the user-specific collection exists if we're using it
        if (useUserCollection)
        {
            await this._qdrantCollectionManager.EnsureCollectionExistsAsync(userId, contextId, "memory");
            this._logger.LogInformation("Using user-specific collection for user {UserId} with context {ContextId}", userId, contextId);
        }

        // Gather the requested kernel memory.
        // Will use a dummy query since we don't care about relevance.
        // minRelevanceScore is set to 0.0 to return all memories.
        List<string> memories = new();
        try
        {
            // Determine the collection name to use
            string collectionName = this._promptOptions.MemoryIndexName;

            // Use user-specific collection if we have a userId
            if (useUserCollection)
            {
                collectionName = this._qdrantCollectionManager.GetCollectionNameString(userId, contextId, "memory");
                this._logger.LogInformation("Using user-specific collection {CollectionName} for searching memories", collectionName);
            }

            // Search for memory items
            var searchResult = await memoryClient.SearchMemoryAsync(
                collectionName,  // Use the determined collection name
                "*",
                relevanceThreshold: 0,
                resultCount: -1,
                chatId,
                memoryContainerName);

            foreach (var memory in searchResult.Results.SelectMany(c => c.Partitions))
            {
                memories.Add(memory.Text);
            }
        }
        catch (Exception connectorException) when (!connectorException.IsCriticalException())
        {
            // A store exception might be thrown if the collection does not exist, depending on the memory store connector.
            this._logger.LogError(connectorException, "Cannot search collection for user {UserId} with context {ContextId}", userId, contextId);
        }

        return this.Ok(memories);
    }

    #region Private

    private static string GetSanitizedParameter(string parameterValue)
    {
        return parameterValue.Replace(Environment.NewLine, string.Empty, StringComparison.Ordinal);
    }

    # endregion
}
