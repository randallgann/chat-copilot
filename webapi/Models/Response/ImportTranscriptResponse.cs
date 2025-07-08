// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace CopilotChat.WebApi.Models.Response;

/// <summary>
/// Response model for transcript import operation.
/// </summary>
public class ImportTranscriptResponse
{
    /// <summary>
    /// Unique identifier for the import operation.
    /// </summary>
    [JsonPropertyName("importId")]
    public string ImportId { get; set; } = string.Empty;

    /// <summary>
    /// Status of the import operation.
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// The name of the Qdrant collection where vectors are stored.
    /// </summary>
    [JsonPropertyName("collectionName")]
    public string CollectionName { get; set; } = string.Empty;

    /// <summary>
    /// Number of segments successfully imported.
    /// </summary>
    [JsonPropertyName("segmentsImported")]
    public int SegmentsImported { get; set; }

    /// <summary>
    /// Error message if import failed.
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; set; }

    public ImportTranscriptResponse(string importId, string status, string collectionName, int segmentsImported = 0, string? error = null)
    {
        this.ImportId = importId;
        this.Status = status;
        this.CollectionName = collectionName;
        this.SegmentsImported = segmentsImported;
        this.Error = error;
    }
}