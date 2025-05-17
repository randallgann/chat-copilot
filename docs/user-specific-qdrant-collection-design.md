# Chat Plugin User-Specific Qdrant Collection Design

## Background

Currently, the Chat Copilot application already has a robust mechanism for managing user-specific Qdrant collections through the `QdrantCollectionManager` service. Each user-context pair gets a dedicated Qdrant collection for vector storage, providing proper data isolation. However, the Chat plugin may not be properly utilizing these user-specific collections for semantic memory retrieval.

## Current Implementation

The current implementation has the following components:

1. **QdrantCollectionManager**: Creates and manages collections with names in the format `cc_{normalizedUserId}_{contextId}_{type}`.

2. **KernelMemoryRetriever**: Used by the Chat plugin to query memories based on user input, but it may not be using the correct user-specific collections.

3. **SearchMemoryAsync Method**: When querying memories, it uses a memory index name (`MemoryIndexName`) specified in the application configuration, but may not properly use the user-specific collections.

## Problem Statement

The Chat plugin currently uses a generic collection named "chatmemory" for retrieving semantic memories rather than using the user-specific Qdrant collections that are created for each user-context combination. This means that all users might be retrieving memories from the same collection instead of their isolated, dedicated collections.

## Proposed Solution

Modify the Chat plugin and KernelMemoryRetriever to use the user-specific Qdrant collections for memory retrieval. This will ensure that when a specific user is chatting within a specific context (channel), the system uses the dedicated Qdrant collection for that user-context pair.

## Implementation Plan

### 1. Update the KernelMemoryRetriever Class

1. Inject the `QdrantCollectionManager` into the `KernelMemoryRetriever` constructor.
2. Modify the `QueryMemoriesAsync` method to use the user-specific collection:
   - Get the user ID from the chat session
   - Use the QdrantCollectionManager to get the appropriate collection name for the user and context
   - Pass this collection name to the memory client when searching

### 2. Update the Chat Plugin

1. Ensure the Chat plugin passes the correct user ID and context ID to the KernelMemoryRetriever.
2. Modify the `GetChatResponseAsync` method to include contextId when querying memories.

### 3. Update SearchMemoryAsync Extension Method

1. Modify the `SearchMemoryAsync` extension method in `KernelMemoryClientExtensions.cs` to use the appropriate collection name.
2. Update the method signature to include userId and contextId parameters.

## Detailed Implementation Steps

### 1. Update KernelMemoryRetriever Class

```csharp
public class KernelMemoryRetriever
{
    private readonly PromptsOptions _promptOptions;
    private readonly ChatSessionRepository _chatSessionRepository;
    private readonly IKernelMemory _memoryClient;
    private readonly List<string> _memoryNames;
    private readonly ILogger _logger;
    private readonly QdrantCollectionManager _qdrantCollectionManager; // New field

    // Update constructor to include QdrantCollectionManager
    public KernelMemoryRetriever(
        IOptions<PromptsOptions> promptOptions,
        ChatSessionRepository chatSessionRepository,
        IKernelMemory memoryClient,
        QdrantCollectionManager qdrantCollectionManager, // New parameter
        ILogger logger)
    {
        this._promptOptions = promptOptions.Value;
        this._chatSessionRepository = chatSessionRepository;
        this._memoryClient = memoryClient;
        this._qdrantCollectionManager = qdrantCollectionManager; // Initialize new field
        this._logger = logger;

        this._memoryNames = new List<string>
        {
            this._promptOptions.DocumentMemoryName,
            this._promptOptions.LongTermMemoryName,
            this._promptOptions.WorkingMemoryName
        };
    }

    // Update QueryMemoriesAsync to use user-specific collection
    public async Task<(string, IDictionary<string, CitationSource>)> QueryMemoriesAsync(
        [Description("Query to match.")] string query,
        [Description("Chat ID to query history from")]
        string chatId,
        [Description("Maximum number of tokens")]
        int tokenLimit,
        [Description("User ID for the session")]
        string userId,
        [Description("Context ID for the session")]
        string contextId)
    {
        // Rest of the implementation remains the same
        // ...

        async Task SearchMemoryAsync(string memoryName, bool isGlobalMemory = false)
        {
            // Use the correct collection name for this user and context
            string searchIndex = isGlobalMemory
                ? this._promptOptions.MemoryIndexName
                : this._qdrantCollectionManager.GetCollectionNameString(userId, contextId, "memory");

            // Ensure the collection exists
            if (!isGlobalMemory)
            {
                await this._qdrantCollectionManager.EnsureCollectionExistsAsync(userId, contextId, "memory");
            }

            var searchResult =
                await this._memoryClient.SearchAsync(
                    query,
                    searchIndex,
                    // Rest of parameters remain the same
                    // ...
                );

            // Process results
            // ...
        }

        // Rest of implementation remains the same
        // ...
    }
}
```

### 2. Update ChatPlugin Class

```csharp
public class ChatPlugin
{
    // Existing fields...
    
    // Update GetChatResponseAsync to pass userId and contextId to QueryMemoriesAsync
    private async Task<CopilotChatMessage> GetChatResponseAsync(
        string chatId, 
        string userId,
        KernelArguments chatContext, 
        CopilotChatMessage userMessage, 
        CancellationToken cancellationToken)
    {
        // Existing code...

        // Get the contextId from the chat session
        ChatSession? chatSession = null;
        if (!await this._chatSessionRepository.TryFindByIdAsync(chatId, callback: v => chatSession = v))
        {
            throw new ArgumentException($"Chat session {chatId} not found.");
        }
        
        // Use the contextId from the chat session or default to "default"
        string contextId = chatSession?.ContextId ?? "default";
        
        // Query relevant semantic and document memories, now with userId and contextId
        await this.UpdateBotResponseStatusOnClientAsync(chatId, "Extracting semantic and document memories", cancellationToken);
        (var memoryText, var citationMap) = await this._kernelMemoryRetriever.QueryMemoriesAsync(
            userIntent, 
            chatId, 
            chatMemoryTokenBudget,
            userId,
            contextId);
            
        // Rest of the implementation remains the same
        // ...
    }
    
    // Update constructor to pass QdrantCollectionManager to KernelMemoryRetriever
    public ChatPlugin(
        Kernel kernel,
        IKernelMemory memoryClient,
        ChatMessageRepository chatMessageRepository,
        ChatSessionRepository chatSessionRepository,
        IHubContext<MessageRelayHub> messageRelayHubContext,
        IOptions<PromptsOptions> promptOptions,
        IOptions<DocumentMemoryOptions> documentImportOptions,
        QdrantCollectionManager qdrantCollectionManager, // New parameter
        ILogger logger,
        AzureContentSafety? contentSafety = null)
    {
        // Other initializations...
        
        this._kernelMemoryRetriever = new KernelMemoryRetriever(
            promptOptions, 
            chatSessionRepository, 
            memoryClient,
            qdrantCollectionManager, // New parameter
            logger);
        
        // Rest of the constructor...
    }
}
```

### 3. Update KernelMemoryClientExtensions

```csharp
public static class KernelMemoryClientExtensions
{
    // Update the SearchMemoryAsync method to support user-specific collections
    public static async Task<SearchResult> SearchMemoryAsync(
        this IKernelMemory memoryClient,
        string indexName,
        string query,
        float relevanceThreshold,
        string chatId,
        string? memoryName = null,
        string? userId = null,
        string? contextId = null,
        QdrantCollectionManager? qdrantCollectionManager = null,
        CancellationToken cancellationToken = default)
    {
        // If userId, contextId, and qdrantCollectionManager are provided, use user-specific collection
        string effectiveIndexName = indexName;
        if (userId != null && contextId != null && qdrantCollectionManager != null)
        {
            effectiveIndexName = qdrantCollectionManager.GetCollectionNameString(userId, contextId, "memory");
            await qdrantCollectionManager.EnsureCollectionExistsAsync(userId, contextId, "memory");
        }
        
        var filter = new MemoryFilter();
        filter.ByTag(MemoryTags.TagChatId, chatId);

        if (!string.IsNullOrWhiteSpace(memoryName))
        {
            filter.ByTag(MemoryTags.TagMemory, memoryName);
        }

        var searchResult =
            await memoryClient.SearchAsync(
                query,
                effectiveIndexName,
                filter,
                null,
                relevanceThreshold,
                -1,
                cancellationToken: cancellationToken);

        return searchResult;
    }
    
    // Other methods remain the same...
}
```

## Testing Plan

1. **Unit Tests**:
   - Write unit tests for the updated `KernelMemoryRetriever` class
   - Verify it correctly uses the user-specific collection names

2. **Integration Tests**:
   - Test the Chat plugin with multiple users and contexts
   - Verify that each user gets memories only from their dedicated collections

3. **Manual Testing**:
   - Create multiple test users and have them create memories in different contexts
   - Verify that users cannot access each other's memories
   - Verify that a user's memories in one context don't appear in another context

## Risks and Mitigations

1. **Backward Compatibility**:
   - Risk: Existing memories may be lost if the collection naming scheme changes
   - Mitigation: Implement a migration script to copy data from the old collection to the new user-specific collections

2. **Performance**:
   - Risk: Increased overhead from managing multiple collections
   - Mitigation: Monitor performance and optimize queries if needed

3. **Collection Management**:
   - Risk: Potential for collection proliferation with many users and contexts
   - Mitigation: Implement a cleanup process for unused collections

## Conclusion

This design will enhance Chat Copilot's memory isolation by ensuring that the Chat plugin uses the correct user-specific Qdrant collections for retrieving semantic memories. The implementation builds on the existing infrastructure for collection management and requires minimal changes to the current architecture.