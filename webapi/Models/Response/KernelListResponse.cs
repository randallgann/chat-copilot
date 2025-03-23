// Copyright (c) Microsoft. All rights reserved.

namespace CopilotChat.WebApi.Models.Response;

/// <summary>
/// Response model containing a list of active kernels in the system.
/// </summary>
public class KernelListResponse
{
    /// <summary>
    /// The total number of active kernels.
    /// </summary>
    public int TotalKernels { get; set; }
    
    /// <summary>
    /// A list of active kernels.
    /// </summary>
    public List<KernelListItem> Kernels { get; set; } = new();
}

/// <summary>
/// Summary information about an active kernel.
/// </summary>
public class KernelListItem
{
    /// <summary>
    /// The user ID associated with this kernel.
    /// </summary>
    public string UserId { get; set; } = string.Empty;
    
    /// <summary>
    /// The context ID associated with this kernel.
    /// </summary>
    public string ContextId { get; set; } = string.Empty;
    
    /// <summary>
    /// The time when this kernel was last accessed.
    /// </summary>
    public DateTime LastAccessTime { get; set; }
    
    /// <summary>
    /// The age of this kernel (time since creation).
    /// </summary>
    public TimeSpan Age { get; set; }
}