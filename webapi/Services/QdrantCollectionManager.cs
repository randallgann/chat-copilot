// Copyright (c) Microsoft. All rights reserved.

using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CopilotChat.WebApi.Models.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;

namespace CopilotChat.WebApi.Services;

/// <summary>
/// Manages Qdrant vector database collections for user-specific kernels.
/// Each user-context combination gets its own isolated collection in Qdrant.
/// </summary>
public class QdrantCollectionManager
{
    private readonly ILogger<QdrantCollectionManager> _logger;
    private readonly string _qdrantEndpoint;
    private readonly string? _qdrantApiKey;
    private readonly HttpClient _httpClient;
    private readonly int _vectorSize = 1536; // Fixed dimension for OpenAI text-embedding-ada-002 model

    /// <summary>
    /// Initializes a new instance of the QdrantCollectionManager class.
    /// </summary>
    /// <param name="configuration">Application configuration</param>
    /// <param name="httpClientFactory">HTTP client factory</param>
    /// <param name="logger">Logger</param>
    public QdrantCollectionManager(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<QdrantCollectionManager> logger)
    {
        this._logger = logger;
        this._httpClient = httpClientFactory.CreateClient();

        // Get Qdrant configuration from settings
        var qdrantConfig = configuration.GetSection("KernelMemory:Services:Qdrant");
        this._qdrantEndpoint = qdrantConfig["Endpoint"] ?? "http://localhost:6333";
        this._qdrantApiKey = qdrantConfig["APIKey"];
        
        // NOTE: We use a fixed vector size for OpenAI text-embedding-ada-002 model
        // Do NOT use EmbeddingModelMaxTokenTotal as that refers to the token limit, not the vector dimension
        
        this._logger.LogInformation("QdrantCollectionManager initialized with endpoint: {Endpoint}", this._qdrantEndpoint);
    }

    /// <summary>
    /// Get the collection name object for a specific user and context.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="contextId">The context ID</param>
    /// <param name="type">Optional type of collection (chat, document, etc.)</param>
    /// <returns>A QdrantCollectionName object</returns>
    public QdrantCollectionName GetCollectionName(string userId, string contextId, string type = "default")
    {
        return new QdrantCollectionName(userId, contextId, type);
    }
    
    /// <summary>
    /// Get the collection name string for a specific user and context.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="contextId">The context ID</param>
    /// <param name="type">Optional type of collection (chat, document, etc.)</param>
    /// <returns>A string representation of the collection name</returns>
    public string GetCollectionNameString(string userId, string contextId, string type = "default")
    {
        return GetCollectionName(userId, contextId, type).ToString();
    }

    /// <summary>
    /// Ensure a collection exists for the given user and context.
    /// Creates the collection if it doesn't already exist.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="contextId">The context ID</param>
    /// <param name="type">Optional type of collection (chat, document, etc.)</param>
    /// <returns>True if the collection was created or already exists</returns>
    public async Task<bool> EnsureCollectionExistsAsync(string userId, string contextId, string type = "default")
    {
        QdrantCollectionName collectionName = GetCollectionName(userId, contextId, type);
        string collectionNameString = collectionName.ToString();

        try
        {
            // Check if collection exists
            if (await CollectionExistsAsync(collectionNameString))
            {
                this._logger.LogInformation("Collection {CollectionName} already exists", collectionNameString);
                return true;
            }

            // Create the collection
            bool created = await CreateCollectionAsync(collectionNameString);
            if (created)
            {
                this._logger.LogInformation("Created new collection {CollectionName} for user {UserId} with context {ContextId} and type {Type}",
                    collectionNameString, userId, contextId, type);
                return true;
            }

            this._logger.LogError("Failed to create collection {CollectionName}", collectionNameString);
            return false;
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error ensuring collection exists for {CollectionName}", collectionNameString);
            return false;
        }
    }

    /// <summary>
    /// Delete a collection for the given user and context.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="contextId">The context ID</param>
    /// <param name="type">Optional type of collection (chat, document, etc.)</param>
    /// <returns>True if the collection was deleted or didn't exist</returns>
    public async Task<bool> DeleteCollectionAsync(string userId, string contextId, string type = "default")
    {
        QdrantCollectionName collectionName = GetCollectionName(userId, contextId, type);
        string collectionNameString = collectionName.ToString();

        try
        {
            // Check if collection exists
            if (!await CollectionExistsAsync(collectionNameString))
            {
                this._logger.LogInformation("Collection {CollectionName} does not exist, nothing to delete", collectionNameString);
                return true;
            }

            // Delete the collection
            var request = new HttpRequestMessage(HttpMethod.Delete, $"{this._qdrantEndpoint}/collections/{collectionNameString}");
            if (!string.IsNullOrEmpty(this._qdrantApiKey))
            {
                request.Headers.Add("api-key", this._qdrantApiKey);
            }

            var response = await this._httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                this._logger.LogInformation("Deleted collection {CollectionName}", collectionNameString);
                return true;
            }

            this._logger.LogError("Failed to delete collection {CollectionName}: {StatusCode}",
                collectionNameString, response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error deleting collection {CollectionName}", collectionNameString);
            return false;
        }
    }

    /// <summary>
    /// List all collections.
    /// </summary>
    /// <returns>A list of collection names</returns>
    public async Task<IEnumerable<string>> ListCollectionsAsync()
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{this._qdrantEndpoint}/collections");
            if (!string.IsNullOrEmpty(this._qdrantApiKey))
            {
                request.Headers.Add("api-key", this._qdrantApiKey);
            }

            var response = await this._httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<QdrantCollectionsResponse>(content);

                if (result != null && result.Result != null && result.Result.Collections != null)
                {
                    return result.Result.Collections.Select(c => c.Name);
                }
            }

            this._logger.LogError("Failed to list collections: {StatusCode}", response.StatusCode);
            return Enumerable.Empty<string>();
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error listing collections");
            return Enumerable.Empty<string>();
        }
    }

    /// <summary>
    /// Check if a collection exists.
    /// </summary>
    private async Task<bool> CollectionExistsAsync(string collectionName)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{this._qdrantEndpoint}/collections/{collectionName}");
            if (!string.IsNullOrEmpty(this._qdrantApiKey))
            {
                request.Headers.Add("api-key", this._qdrantApiKey);
            }

            var response = await this._httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error checking if collection {CollectionName} exists", collectionName);
            return false;
        }
    }

    /// <summary>
    /// Create a new collection.
    /// </summary>
    private async Task<bool> CreateCollectionAsync(string collectionName)
    {
        try
        {
            var createRequest = new CreateCollectionRequest
            {
                Vectors = new CollectionVectorParams
                {
                    Size = this._vectorSize,
                    Distance = "Cosine"
                }
            };

            var json = JsonSerializer.Serialize(createRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Put, $"{this._qdrantEndpoint}/collections/{collectionName}");
            request.Content = content;

            if (!string.IsNullOrEmpty(this._qdrantApiKey))
            {
                request.Headers.Add("api-key", this._qdrantApiKey);
            }

            var response = await this._httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error creating collection {CollectionName}", collectionName);
            return false;
        }
    }

    // Removed NormalizeId method since it's now in QdrantCollectionName class

    #region Qdrant API Models

    private class QdrantCollectionsResponse
    {
        public CollectionsResult? Result { get; set; }
        public bool Status { get; set; }
        public string? Time { get; set; }
    }

    private class CollectionsResult
    {
        public List<CollectionInfo>? Collections { get; set; }
    }

    private class CollectionInfo
    {
        public string Name { get; set; } = string.Empty;
    }

    private class CreateCollectionRequest
    {
        [JsonPropertyName("vectors")]
        public CollectionVectorParams Vectors { get; set; } = new CollectionVectorParams();
    }

    private class CollectionVectorParams
    {
        [JsonPropertyName("size")]
        public int Size { get; set; }

        [JsonPropertyName("distance")]
        public string Distance { get; set; } = "Cosine";
    }

    #endregion
}