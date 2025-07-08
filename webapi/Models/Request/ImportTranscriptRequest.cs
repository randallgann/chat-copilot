// Copyright (c) Microsoft. All rights reserved.

namespace CopilotChat.WebApi.Models.Request;

/// <summary>
/// Request model for importing transcript data into memory.
/// </summary>
public class ImportTranscriptRequest
{
    /// <summary>
    /// The user ID who owns the channel.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// The channel ID where the video belongs.
    /// </summary>
    public string ChannelId { get; set; } = string.Empty;

    /// <summary>
    /// The YouTube video ID.
    /// </summary>
    public string VideoId { get; set; } = string.Empty;

    /// <summary>
    /// The title of the video.
    /// </summary>
    public string VideoTitle { get; set; } = string.Empty;

    /// <summary>
    /// The original YouTube video URL.
    /// </summary>
    public string VideoUrl { get; set; } = string.Empty;

    /// <summary>
    /// The transcript segments to import.
    /// </summary>
    public List<TranscriptSegment> Segments { get; set; } = new();
}

/// <summary>
/// Represents a segment of transcript with timing information.
/// </summary>
public class TranscriptSegment
{
    /// <summary>
    /// The text content of the segment.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// The start time of the segment in seconds.
    /// </summary>
    public double StartTime { get; set; }

    /// <summary>
    /// The end time of the segment in seconds.
    /// </summary>
    public double EndTime { get; set; }
}