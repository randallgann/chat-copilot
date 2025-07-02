# Token Tracking System in Chat Copilot

This document provides an overview of the token tracking system implemented in Chat Copilot, explaining how it works, its key components, and how it's used throughout the application to manage token usage with AI models.

## Overview

The token tracking system in Chat Copilot provides a way to monitor and manage token consumption when interacting with AI models (primarily OpenAI models). This is important for several reasons:

1. **Cost Management**: Tokens directly correlate to API costs
2. **Context Window Management**: Ensures conversations stay within model context limits
3. **Performance Optimization**: Helps optimize memory usage and response times
4. **Troubleshooting**: Provides insight into where tokens are being used

## Core Components

### TokenUtils Class

The primary class responsible for token-related utilities is the `TokenUtils` class located in `webapi/Plugins/Utils/TokenUtils.cs`. This static class offers several key functions:

#### Token Counting

```csharp
// Calculate tokens in a string using SharpToken with cl100k_base encoding (compatible with GPT models)
public static int TokenCount(string text)
{
    var tokens = s_tokenizer.Encode(text);
    return tokens.Count;
}
```

#### Semantic Function Tracking

The class maintains a dictionary of semantic functions used in the system:

```csharp
internal static readonly Dictionary<string, string> SemanticFunctions = new()
{
    { "SystemAudienceExtraction", "audienceExtraction" },
    { "SystemIntentExtraction", "userIntentExtraction" },
    { "SystemMetaPrompt", "metaPromptTemplate" },
    { "SystemCompletion", "responseCompletion" },
    { "SystemCognitive_WorkingMemory", "workingMemoryExtraction" },
    { "SystemCognitive_LongTermMemory", "longTermMemoryExtraction" }
};
```

#### Token Usage Extraction

The system extracts token usage information from AI model responses:

```csharp
internal static string? GetFunctionTokenUsage(FunctionResult result, ILogger logger)
{
    if (result.Metadata is null ||
        !result.Metadata.TryGetValue("Usage", out object? usageObject) || usageObject is null)
    {
        logger.LogWarning("No usage metadata provided");
        return null;
    }

    var tokenUsage = 0;
    try
    {
        var jsonObject = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(usageObject));
        tokenUsage = jsonObject.GetProperty("TotalTokens").GetInt32();
    }
    catch (KeyNotFoundException)
    {
        logger.LogError("Usage details not found in model result.");
        return null;
    }

    return tokenUsage.ToString(CultureInfo.InvariantCulture);
}
```

This method is designed to extract token usage information from the metadata returned by AI model completions. It's used throughout the application to track token consumption.

#### Chat Message Token Counting

The class also provides methods to calculate token costs for chat messages:

```csharp
internal static int GetContextMessageTokenCount(AuthorRole authorRole, string? content)
{
    return TokenCount($"role:{authorRole.Label}") + TokenCount($"content:{content}\n");
}

internal static int GetContextMessagesTokenCount(ChatHistory chatHistory)
{
    var tokenCount = 0;
    foreach (var message in chatHistory)
    {
        tokenCount += GetContextMessageTokenCount(message.Role, message.Content);
    }
    return tokenCount;
}
```

### Where Token Tracking Is Used

#### ChatPlugin Class

The `ChatPlugin` class (in `webapi/Plugins/Chat/ChatPlugin.cs`) is the primary consumer of token tracking. It:

1. Tracks tokens used in system instructions
2. Manages token budget for chat history
3. Calculates token usage for semantic memories
4. Tracks tokens in prompt templates
5. Ensures the overall context stays within model limits

Key methods include:

```csharp
private int GetMaxRequestTokenBudget()
{
    const int ExtraOpenAiMessageTokens = 20;
    return this._promptOptions.CompletionTokenLimit
           - ExtraOpenAiMessageTokens
           - this._promptOptions.ResponseTokenLimit
           - this._promptOptions.FunctionCallingTokenLimit;
}

private Dictionary<string, int> GetTokenUsages(KernelArguments kernelArguments, string? content = null)
{
    var tokenUsageDict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    // Total token usage of each semantic function
    foreach (string function in TokenUtils.SemanticFunctions.Values)
    {
        if (kernelArguments.TryGetValue($"{function}TokenUsage", out object? tokenUsage))
        {
            if (tokenUsage is string tokenUsageString)
            {
                tokenUsageDict.Add(function, int.Parse(tokenUsageString, CultureInfo.InvariantCulture));
            }
        }
    }

    if (content != null)
    {
        tokenUsageDict.Add(TokenUtils.SemanticFunctions["SystemCompletion"]!, TokenUtils.TokenCount(content));
    }

    return tokenUsageDict;
}
```

#### SemanticChatMemoryExtractor Class

The `SemanticChatMemoryExtractor` class (in `webapi/Plugins/Chat/SemanticChatMemoryExtractor.cs`) uses token tracking to:

1. Calculate token budgets for memory extraction
2. Track token usage during memory generation
3. Store token usage information in kernel arguments

This is done through code like:

```csharp
// Get token usage from ChatCompletion result and add to context
string? tokenUsage = TokenUtils.GetFunctionTokenUsage(result, logger);
if (tokenUsage is not null)
{
    // Since there are multiple memory types, total token usage is calculated by cumulating the token usage of each memory type.
    kernelArguments[TokenUtils.GetFunctionKey($"SystemCognitive_{memoryType}")] = tokenUsage;
}
else
{
    logger.LogError("Unable to determine token usage for {0}", $"SystemCognitive_{memoryType}");
}
```

## Token Flow Through The System

1. **User Input**: When a user sends a message, the system begins tracking token usage
2. **System Components**: Token usage is tracked for various system components:
   - System instructions
   - Audience extraction
   - User intent extraction
   - Working memory extraction
   - Long-term memory extraction
   - Chat history retrieval
   - Response generation
3. **Storage**: Token usage information is stored in the chat message data
4. **Reporting**: Total token usage is reported back to the client

## Current Issue and Fix

There is a current issue in the token tracking system where a `KeyNotFoundException` is thrown when attempting to access the "TotalTokens" property in the Usage metadata. This happens because when using `ResponseFormat = "json_object"` in OpenAI settings, the structure of the response can be different.

The proposed fix involves making the `GetFunctionTokenUsage` method more resilient by:

1. Adding support for different response formats
2. Implementing better error handling
3. Providing fallbacks when token information is not available

```csharp
internal static string? GetFunctionTokenUsage(FunctionResult result, ILogger logger)
{
    if (result.Metadata is null ||
        !result.Metadata.TryGetValue("Usage", out object? usageObject) || usageObject is null)
    {
        logger.LogWarning("No usage metadata provided");
        return null;
    }

    var tokenUsage = 0;
    try
    {
        var jsonObject = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(usageObject));

        // Try to get TotalTokens property (traditional format)
        if (jsonObject.TryGetProperty("TotalTokens", out JsonElement totalTokensElement))
        {
            tokenUsage = totalTokensElement.GetInt32();
        }
        // Try newer format with completion_tokens + prompt_tokens
        else if (jsonObject.TryGetProperty("CompletionTokens", out JsonElement completionTokens) &&
                 jsonObject.TryGetProperty("PromptTokens", out JsonElement promptTokens))
        {
            tokenUsage = completionTokens.GetInt32() + promptTokens.GetInt32();
        }
        // If structure is different, try to log the actual structure for debugging
        else
        {
            logger.LogWarning("Unexpected Usage format in model result: {Format}", JsonSerializer.Serialize(usageObject));
            // Fallback to a token count estimate based on the response text length
            tokenUsage = TokenCount(result.ToString()) * 2; // Rough estimate
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error parsing Usage details in model result.");
        return null;
    }

    return tokenUsage.ToString(CultureInfo.InvariantCulture);
}
```

## Best Practices for Managing Tokens

1. **Budget Allocation**: Properly divide token budget between system components
2. **Context Pruning**: Remove less relevant information when approaching token limits
3. **Error Handling**: Implement robust error handling for token tracking failures
4. **Logging**: Log token usage for monitoring and optimization
5. **Fallbacks**: Implement fallbacks when token information is unavailable

## Future Improvements

1. **UI Reporting**: Add token usage reporting to the UI for user awareness
2. **Cost Tracking**: Associate token usage with actual costs
3. **Quota Management**: Implement user or system quotas for token usage
4. **Alternative Models**: Support token tracking for alternative AI models
5. **Optimization Strategies**: Develop more sophisticated token allocation strategies

## Conclusion

The token tracking system in Chat Copilot is a critical component that ensures the application can efficiently manage its interactions with AI models. By properly tracking and managing tokens, the system can provide a better user experience while controlling costs and staying within technical limitations.
