# Roadmap: Per-User Kernel Implementation for Chat Copilot

This document outlines the implementation plan for refactoring Chat Copilot to support multiple users by providing a dedicated Semantic Kernel instance per user.

## Current Architecture

The current implementation uses a scoped Kernel instance that's created per request:
- `SemanticKernelProvider` (singleton) creates a base kernel
- Each request gets a fresh clone of this kernel via DI
- Controllers receive the kernel through dependency injection
- No user-specific persistence of kernel state across requests

## Implementation Plan

### 1. Create a Kernel Management Service

**New File: `/webapi/Services/KernelManager.cs`**
- Create a service to manage and cache kernel instances per user
- Create a `UserKernelInfo` class to hold:
  - The user's Kernel instance
  - Metadata like last access time
  - User ID (PostgreSQL UUID, e.g. "79c1a50b-ed7a-426c-a510-f995a87a3350") for tracking
- Implement methods to:
  - Get a kernel for a specific user (create if not exists)
  - Release/dispose a kernel when no longer needed
  - Clear all kernels for maintenance or redeployment
- Use thread-safe collections (ConcurrentDictionary<string, UserKernelInfo>) to handle concurrent requests
  - Key: User's UUID string from database
  - Value: UserKernelInfo object containing the user's Kernel
- Register as a singleton in DI container to maintain a global cache of UserKernelInfo objects

### 2. Modify SemanticKernelProvider

**File to Modify: `/webapi/Services/SemanticKernelProvider.cs`**
- Refactor to be a factory for kernels rather than holding a single kernel instance
- Remove the cached `_kernel` instance
- Modify `GetCompletionKernel()` to always create a fresh kernel

### 3. Update DI Registration in SemanticKernelExtensions

**File to Modify: `/webapi/Extensions/SemanticKernelExtensions.cs`**
- Update the service registration to include the KernelManager
- Remove the scoped registration of the Kernel itself
- Register the KernelManager as a singleton

### 4. Update ChatController

**File to Modify: `/webapi/Controllers/ChatController.cs`**
- Inject IKernelManager instead of Kernel
- Get the user-specific kernel with `await kernelManager.GetUserKernelAsync(authInfo.UserId)`
- Ensure all kernel operations use the user-specific kernel

### 5. Update Other Controllers that Use Kernel

**Files to Check and Update:**
- `/webapi/Controllers/ChatMemoryController.cs`
- `/webapi/Controllers/PluginController.cs`
- Any other controller that injects the Kernel directly

For each controller:
1. Inject IKernelManager instead of Kernel
2. Get the user-specific kernel with `await kernelManager.GetUserKernelAsync(authInfo.UserId)`

### 6. Add Kernel Expiration/Cleanup Logic

**New File: `/webapi/Services/KernelCleanupService.cs`**
- Implement a background service for cleaning up unused kernels
- Add tracking of last access time for each kernel
- Periodically remove kernels that haven't been used recently
- Add cleanup on low memory conditions (optional)

### 7. Add User-Specific Kernel Configuration

**New Files:**
- `/webapi/Models/Storage/UserKernelConfig.cs`
- `/webapi/Storage/IUserKernelConfigRepository.cs`
- `/webapi/Storage/UserKernelConfigRepository.cs`
- `/webapi/Controllers/UserConfigController.cs`

**Implementation Details:**
- Create a model to store user-specific configuration:
  ```csharp
  public class UserKernelConfig
  {
      public string UserId { get; set; }
      public Dictionary<string, object> Settings { get; set; }
      public LLMOptions CompletionOptions { get; set; }
      public LLMOptions EmbeddingOptions { get; set; }
      public List<string> EnabledPlugins { get; set; }
      public Dictionary<string, string> ApiKeys { get; set; }
  }

  public class LLMOptions
  {
      public string ModelId { get; set; }
      public string Endpoint { get; set; }
      public float Temperature { get; set; }
      public int MaxTokens { get; set; }
  }
  ```

- Create a repository for persisting user configurations:
  ```csharp
  public interface IUserKernelConfigRepository
  {
      Task<UserKernelConfig> GetConfigAsync(string userId);
      Task SaveConfigAsync(UserKernelConfig config);
      Task DeleteConfigAsync(string userId);
  }
  ```

- Modify KernelManager to apply user configurations when creating UserKernelInfo objects:
  ```csharp
  private async Task<UserKernelInfo> CreateUserKernelInfoAsync(string userId)
  {
      var userConfig = await _configRepository.GetConfigAsync(userId);
      var kernel = _semanticKernelProvider.GetCompletionKernel(userConfig);
      // Apply additional user-specific settings
      
      return new UserKernelInfo
      {
          UserId = userId,
          Kernel = kernel,
          LastAccessTime = DateTime.UtcNow
      };
  }
  
  public async Task<Kernel> GetUserKernelAsync(string userId)
  {
      // Try to get existing UserKernelInfo from dictionary
      if (!_userKernels.TryGetValue(userId, out var userKernelInfo))
      {
          // Create new UserKernelInfo if not found
          userKernelInfo = await CreateUserKernelInfoAsync(userId);
          _userKernels.TryAdd(userId, userKernelInfo);
      }
      else
      {
          // Update access time for existing UserKernelInfo
          userKernelInfo.UpdateLastAccessTime();
      }
      
      return userKernelInfo.GetKernel();
  }
  ```

- Create a controller for managing user configurations:
  ```csharp
  [ApiController]
  public class UserConfigController : ControllerBase
  {
      private readonly IUserKernelConfigRepository _configRepository;
      private readonly IAuthInfo _authInfo;

      // Methods to get, update, and reset user configuration
  }
  ```

### 8. Update Program.cs

**File to Modify: `/webapi/Program.cs`**
- Register the KernelManager in the DI container
- Register the background cleanup service if implementing cleanup
- Register the user configuration repository
- Update any other services that might depend on direct kernel injection

### 9. Update MaintenanceMiddleware

**File to Check: `/webapi/Services/MaintenanceMiddleware.cs`**
- Update to clear all kernels when entering maintenance mode
- Ensure proper resource cleanup during application shutdown

### 10. Testing Strategy

1. **Unit Tests**:
   - Create tests for KernelManager
   - Verify proper kernel lifecycle management
   - Test concurrency scenarios
   - Test user configuration application logic

2. **Integration Tests**:
   - Test with multiple simulated users
   - Verify kernel isolation between users
   - Test performance under load
   - Test persistence and retrieval of user configurations

3. **Feature Tests**:
   - Verify all existing features work with per-user kernels
   - Test plugin registration in user-specific kernels
   - Test memory cleanup and resource management
   - Test configuration preferences across user sessions

### 11. UI Enhancement (Optional)

- Add user configuration settings to the frontend:
  - Model selection for completion and embedding
  - Temperature and token settings
  - API key management (with proper security measures)
  - Plugin enabling/disabling

### 12. Performance Monitoring

- Add metrics to track:
  - Number of active kernels
  - Memory usage per kernel
  - Kernel creation/disposal rate
  - Cache hit ratio for kernel retrieval
  - Configuration load/save operations

## Considerations and Challenges

1. **Memory Usage**: Each kernel consumes memory, so implement expiration for unused kernels
2. **Concurrency**: Ensure thread-safety for all kernel operations
3. **State Isolation**: Verify complete isolation of state between user kernels
4. **Guest Users**: Handle special cases for unauthenticated/guest users properly
5. **Error Handling**: Gracefully handle errors in kernel creation or retrieval
6. **Deployment**: Consider the impact on scaling and resource requirements
7. **Configuration Security**: Ensure sensitive settings like API keys are properly encrypted
8. **Defaults**: Provide sensible defaults when user configuration is not specified
9. **Validation**: Validate user configurations to prevent misconfiguration or service abuse

## Timeline Estimate

1. Kernel Manager implementation: 2-3 days
2. Controller updates: 2-3 days
3. User configuration implementation: 2-3 days
4. Testing: 3-4 days
5. Performance tuning: 1-2 days

Total estimated time: 10-15 days