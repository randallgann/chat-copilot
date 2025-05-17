// Copyright (c) Microsoft. All rights reserved.

using System.Text;

namespace CopilotChat.WebApi.Models.Storage;

/// <summary>
/// Represents a Qdrant collection name with component parts.
/// This provides a structured way to create, parse, and manipulate collection names.
/// </summary>
public class QdrantCollectionName
{
    /// <summary>
    /// The user ID component of the collection name.
    /// </summary>
    public string UserId { get; }

    /// <summary>
    /// The context ID component of the collection name.
    /// </summary>
    public string ContextId { get; }

    /// <summary>
    /// Optional collection type (chat, document, etc.).
    /// </summary>
    public string Type { get; }

    /// <summary>
    /// Constructor for creating a new collection name from components.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="contextId">The context ID</param>
    /// <param name="type">Optional type (chat, document, etc.)</param>
    public QdrantCollectionName(string userId, string contextId, string type = "default")
    {
        UserId = userId;
        ContextId = contextId;
        Type = type;
    }

    /// <summary>
    /// Try to parse a string into a QdrantCollectionName.
    /// </summary>
    /// <param name="collectionName">The collection name to parse</param>
    /// <param name="result">The parsed result, if successful</param>
    /// <returns>True if parsing was successful</returns>
    public static bool TryParse(string collectionName, out QdrantCollectionName? result)
    {
        result = null;

        // Expected format: cc_{userId}_{contextId}_{type}
        string[] parts = collectionName.Split('_');
        if (parts.Length < 4 || parts[0] != "cc")
        {
            return false;
        }

        result = new QdrantCollectionName(parts[1], parts[2], parts.Length > 3 ? parts[3] : "default");
        return true;
    }

    /// <summary>
    /// Normalize an ID to be valid for Qdrant collection names.
    /// </summary>
    /// <param name="id">The ID to normalize</param>
    /// <returns>A normalized ID with only letters, numbers, and underscores</returns>
    public static string NormalizeId(string id)
    {
        // Replace invalid characters with underscores
        StringBuilder sb = new StringBuilder();
        foreach (char c in id)
        {
            if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
            {
                sb.Append(c);
            }
            else
            {
                sb.Append('_');
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Convert the object to a valid Qdrant collection name string.
    /// </summary>
    /// <returns>A properly formatted collection name</returns>
    public override string ToString()
    {
        string normalizedUserId = NormalizeId(UserId);
        string normalizedContextId = NormalizeId(ContextId);
        string normalizedType = NormalizeId(Type);
        
        // Format: cc_userId_contextId_type
        return $"cc_{normalizedUserId}_{normalizedContextId}_{normalizedType}";
    }
}