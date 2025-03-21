﻿// Copyright (c) Microsoft. All rights reserved.

using System.ComponentModel.DataAnnotations;
using CopilotChat.WebApi.Options;

namespace CopilotChat.WebApi.Models.Request;

public class Ask
{
    [Required, NotEmptyOrWhitespace]
    public string Input { get; set; } = string.Empty;

    public IEnumerable<KeyValuePair<string, string>> Variables { get; set; } = Enumerable.Empty<KeyValuePair<string, string>>();
    
    /// <summary>
    /// Optional context ID for the chat (e.g., channelId for YouTube channels).
    /// When provided, it will be used to select the appropriate kernel.
    /// </summary>
    public string? ContextId { get; set; }
}
