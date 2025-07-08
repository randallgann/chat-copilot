// Copyright (c) Microsoft. All rights reserved.

using System.IO;
using CopilotChat.WebApi.Auth;
using CopilotChat.WebApi.Extensions;
using CopilotChat.WebApi.Models.Request;
using CopilotChat.WebApi.Models.Response;
using CopilotChat.WebApi.Models.Storage;
using CopilotChat.WebApi.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.KernelMemory;

namespace CopilotChat.WebApi.Controllers;

/// <summary>
/// Controller for importing transcript data into memory.
/// </summary>
[ApiController]
[Route("api/memory")]
public class TranscriptImportController : ControllerBase
{
    private readonly ILogger<TranscriptImportController> _logger;
    private readonly IAuthInfo _authInfo;
    private readonly Services.QdrantCollectionManager _qdrantCollectionManager;
    private readonly ChatSessionRepository _chatSessionRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="TranscriptImportController"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="authInfo">The authentication info.</param>
    /// <param name="qdrantCollectionManager">The Qdrant collection manager.</param>
    /// <param name="chatSessionRepository">The chat session repository.</param>
    public TranscriptImportController(
        ILogger<TranscriptImportController> logger,
        IAuthInfo authInfo,
        Services.QdrantCollectionManager qdrantCollectionManager,
        ChatSessionRepository chatSessionRepository)
    {
        this._logger = logger;
        this._authInfo = authInfo;
        this._qdrantCollectionManager = qdrantCollectionManager;
        this._chatSessionRepository = chatSessionRepository;
    }

    /// <summary>
    /// Imports transcript data into memory for the specified user and channel.
    /// </summary>
    /// <param name="memoryClient">The kernel memory client.</param>
    /// <param name="request">The import request containing transcript data.</param>
    /// <returns>Import status and collection information.</returns>
    [HttpPost]
    [Route("import-transcript")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [Authorize]
    public async Task<IActionResult> ImportTranscriptAsync(
        [FromServices] IKernelMemory memoryClient,
        [FromBody] ImportTranscriptRequest request)
    {
        try
        {
            // Validate request
            if (string.IsNullOrEmpty(request.UserId) || string.IsNullOrEmpty(request.ChannelId))
            {
                return this.BadRequest("UserId and ChannelId are required.");
            }

            if (string.IsNullOrEmpty(request.VideoId) || request.Segments == null || !request.Segments.Any())
            {
                return this.BadRequest("VideoId and Segments are required.");
            }

            // Sanitize input for logging
            var sanitizedUserId = GetSanitizedParameter(request.UserId);
            var sanitizedChannelId = GetSanitizedParameter(request.ChannelId);
            var sanitizedVideoId = GetSanitizedParameter(request.VideoId);

            this._logger.LogInformation(
                "Starting transcript import for user {UserId}, channel {ChannelId}, video {VideoId} with {SegmentCount} segments",
                sanitizedUserId, sanitizedChannelId, sanitizedVideoId, request.Segments.Count);

            // Note: Channel validation is handled by the calling service (API service)
            // We trust that the API service has already validated user permissions

            // Ensure the collection exists
            await this._qdrantCollectionManager.EnsureCollectionExistsAsync(request.UserId, request.ChannelId, "default");
            
            // Get collection name
            string collectionName = this._qdrantCollectionManager.GetCollectionNameString(request.UserId, request.ChannelId, "default");

            // Generate unique import ID
            string importId = Guid.NewGuid().ToString();

            // Import each transcript segment
            int successfulImports = 0;
            for (int i = 0; i < request.Segments.Count; i++)
            {
                var segment = request.Segments[i];
                
                // Skip empty segments
                if (string.IsNullOrWhiteSpace(segment.Text))
                {
                    continue;
                }

                try
                {
                    // Create document ID with video and segment info
                    string documentId = $"video_{request.VideoId}_{i:000}";
                    
                    // Create formatted content with metadata
                    string formattedContent = $"""
                        Video: {request.VideoTitle}
                        URL: {request.VideoUrl}
                        Time: {segment.StartTime:F1}s - {segment.EndTime:F1}s
                        
                        {segment.Text}
                        """;
                    
                    // Create stream from text content
                    using var stream = new MemoryStream();
                    using var writer = new StreamWriter(stream);
                    await writer.WriteAsync(formattedContent);
                    await writer.FlushAsync();
                    stream.Position = 0;
                    
                    // Create upload request with metadata
                    var uploadRequest = new DocumentUploadRequest
                    {
                        DocumentId = documentId,
                        Index = collectionName,
                        Files = new List<DocumentUploadRequest.UploadedFile> 
                        { 
                            new($"transcript_{request.VideoId}_{i:000}.txt", stream) 
                        },
                        Steps = new List<string> { "extract", "partition", "gen_embeddings", "save_records" }
                    };
                    
                    // Add tags
                    uploadRequest.Tags.Add("user_id", request.UserId);
                    uploadRequest.Tags.Add("channel_id", request.ChannelId);
                    uploadRequest.Tags.Add("video_id", request.VideoId);
                    uploadRequest.Tags.Add("video_title", request.VideoTitle);
                    uploadRequest.Tags.Add("video_url", request.VideoUrl);
                    uploadRequest.Tags.Add("start_time", segment.StartTime.ToString("F1"));
                    uploadRequest.Tags.Add("end_time", segment.EndTime.ToString("F1"));
                    uploadRequest.Tags.Add("content_type", "video_transcript");
                    uploadRequest.Tags.Add("import_id", importId);
                    
                    // Import document
                    await memoryClient.ImportDocumentAsync(uploadRequest);
                    
                    successfulImports++;
                    
                    this._logger.LogDebug(
                        "Imported segment {SegmentIndex} for video {VideoId} into collection {CollectionName}",
                        i, sanitizedVideoId, collectionName);
                }
                catch (Exception segmentException)
                {
                    this._logger.LogError(segmentException,
                        "Failed to import segment {SegmentIndex} for video {VideoId}: {Error}",
                        i, sanitizedVideoId, segmentException.Message);
                    
                    // Continue with other segments instead of failing the entire import
                }
            }

            this._logger.LogInformation(
                "Completed transcript import for video {VideoId}. Successfully imported {SuccessCount} of {TotalCount} segments",
                sanitizedVideoId, successfulImports, request.Segments.Count);

            // Return success response
            return this.Ok(new ImportTranscriptResponse(
                importId: importId,
                status: successfulImports > 0 ? "success" : "failed",
                collectionName: collectionName,
                segmentsImported: successfulImports,
                error: successfulImports == 0 ? "No segments were successfully imported" : null
            ));
        }
        catch (Exception ex) when (!ex.IsCriticalException())
        {
            this._logger.LogError(ex, "Error importing transcript for video {VideoId}: {Error}", 
                GetSanitizedParameter(request.VideoId), ex.Message);
            
            return this.StatusCode(StatusCodes.Status500InternalServerError, 
                new ImportTranscriptResponse(
                    importId: string.Empty,
                    status: "error",
                    collectionName: string.Empty,
                    segmentsImported: 0,
                    error: "An error occurred while importing the transcript"
                ));
        }
    }

    #region Private

    private static string GetSanitizedParameter(string parameterValue)
    {
        return parameterValue.Replace(Environment.NewLine, string.Empty, StringComparison.Ordinal);
    }

    #endregion
}