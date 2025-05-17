# Chat Copilot Changelog

This document tracks significant changes to the Chat Copilot codebase, particularly focusing on feature additions, architecture improvements, and critical fixes.

## Purpose
This changelog serves as a reference for developers working on Chat Copilot to track the evolution of the codebase and understand the context behind changes.

## Changes

### 2025-05-16: Refactored Qdrant Collection Naming to Object-Oriented Approach

Improved Qdrant collection naming system by implementing an object-oriented approach with QdrantCollectionName class.

**Key Changes:**
- Created QdrantCollectionName class to encapsulate collection name components
- Updated collection naming pattern to use prefix "cc_" (Chat Copilot)
- Added support for collection types beyond just user-context pairs
- Improved parsing and validation of collection names

**Implementation Details:**
- Collections now follow a `cc_{userId}_{contextId}_{type}` naming pattern
- Added TryParse method for converting string names back to objects
- Centralized NormalizeId functionality in the QdrantCollectionName class
- Updated QdrantCollectionManager to use the new object-oriented approach

**Benefits:**
- Better encapsulation of naming logic
- More flexibility for future changes to naming conventions
- Support for different collection types (chat, document, etc.)
- Improved maintainability with proper object-oriented design

### 2025-05-16: Added Per-User Qdrant Collections for Kernel Memory

Implemented automatic creation of user-specific Qdrant collections for each kernel, ensuring proper data isolation across users and contexts.

**Key Changes:**
- Created QdrantCollectionManager to handle collection lifecycle and naming
- Updated KernelManager to automatically create Qdrant collections for each user-context pair
- Added API endpoints for listing and managing Qdrant collections
- Each user now gets dedicated vector storage with predictable naming convention

**Implementation Details:**
- Collections follow a `user_{normalizedUserId}_{contextId}` naming pattern
- Collections are created automatically when a kernel is created
- Implemented HTTP endpoints for collection management:
  - GET /api/kernel/collections - Lists all collections for current user
  - DELETE /api/kernel/collections/{userId} - Deletes collections for a specific user
- Added codebase security by verifying user permissions for collection operations

**Benefits:**
- Proper data isolation between users
- Improved security and privacy for multi-user deployments
- Collections persist between kernel usage (unlike in-memory kernels which are cleaned up)
- Better memory organization and management for context-specific data

### 2025-05-16: Improved PassThroughAuthenticationHandler with Custom User Headers

Enhanced the PassThroughAuthenticationHandler to support custom user IDs and names via HTTP headers, resolving issues with kernel user identification.

**Key Changes:**
- Modified PassThroughAuthenticationHandler to accept X-User-Id and X-User-Name headers
- Added fallback to default values when headers are not present
- Updated documentation in CLAUDE.md to explain the new feature
- Fixed issue where all kernels were being created with the same user ID

**Implementation Details:**
- Added constants for header names (X-User-Id and X-User-Name)
- Implemented header value extraction in HandleAuthenticateAsync method
- Added logging for when custom headers are used
- Maintains backward compatibility by using defaults when headers are absent

**Benefits:**
- Enables testing of multi-user scenarios without using full authentication
- Resolves issue where all kernels were created with the same user ID
- Simplifies development and testing with multiple simulated users
- Maintains compatibility with existing code that relies on default values

## Changes

### 2025-05-15: Fixed Critical Bug in Volatile Storage Message Retrieval

Fixed a bug in the chat message retrieval system where messages stored in volatile memory weren't being properly returned to clients.

**Key Changes:**
- Modified `VolatileCopilotChatMessageContext.QueryEntitiesAsync` to immediately materialize query results
- Replaced `Task.Run` with `Task.FromResult` to prevent async threading issues
- Added diagnostic logging to track message retrieval pipeline

**Implementation Details:**
- The original implementation used LINQ with deferred execution, causing potential race conditions
- When the query was defined and when it was actually executed, the state of the collection could change
- Fixed by using `.ToList()` to immediately materialize each step of the LINQ query
- Ensures that the filtering, ordering, and pagination operations execute immediately
- Replaced asynchronous `Task.Run` with synchronous `Task.FromResult` to maintain execution context

**Benefits:**
- Fixed empty results being returned even when messages existed in the repository
- Improved reliability of in-memory storage for development/testing
- Added comprehensive logging for message retrieval debugging
- Eliminated potential race conditions in async message handling

## Changes

### 2025-05-11: Google Cloud Secret Manager Integration for Secure API Key Management

Implemented secure API key management using Google Cloud Secret Manager, allowing for more secure deployment in production environments.

**Key Changes:**
- Added Google Cloud Secret Manager integration for retrieving OpenAI API keys
- Created a robust fallback mechanism for development environments
- Improved error handling and debugging for secret retrieval
- Updated Docker configuration to support service account key mounting
- Configured Qdrant vector database integration for document storage

**Implementation Details:**
- Created GoogleCloudSecretManagerExtensions class with methods to:
  - Authenticate with GCP using service account key files
  - Retrieve secrets from GCP Secret Manager
  - Validate API keys to ensure proper format
  - Gracefully fall back to environment variables if needed
- Enhanced configuration pipeline to prioritize secrets from GCP Secret Manager
- Added support for explicit secret path configuration via environment variables
- Created directory structure in Docker container for mounting service account keys
- Configured Qdrant endpoint to use proper Docker networking

**Benefits:**
- Increased security by removing hardcoded API keys from configuration files
- Better separation of development and production environments
- More robust handling of API key retrieval failures
- Enhanced logging and debugging for secret management
- Proper integration with Docker containerization
- Support for vector storage and retrieval in Qdrant

## Changes

### 2025-05-02: API Request Format Documentation for Chat Messages

Added documentation for the required request format when posting messages to the chat endpoint.

**Key Details:**
- The POST endpoint for chat messages (`/chats/{chatId}/messages`) requires a `messageType` parameter.
- This parameter must be provided as a key-value pair in the `variables` array of the request body.
- Valid messageType values are "Message" (standard chat), "Plan", or "Document" (for uploaded documents).

**Correct Request Format:**
```json
{
  "input": "Your message text here",
  "variables": [
    {
      "key": "messageType",
      "value": "Message"
    }
  ],
  "contextId": "default"
}
```

**Implementation Details:**
- The `ChatPlugin.ChatAsync` method requires a `messageType` parameter that is passed from the variables collection.
- This value determines how the message is processed and stored in the chat history.
- Failure to include the messageType results in a "Missing argument for function parameter 'messageType'" error.

## Changes

### 2025-03-28: HTTP-only Configuration with SignalR Optimization

Configured the webapi to use HTTP-only communication and optimized SignalR for in-cluster communication.

**Key Changes:**
- Removed HTTPS requirements to support cluster-internal communication
- Configured CORS to allow any origin during development
- Enhanced SignalR settings for optimal WebSocket performance
- Updated Docker configuration to remove HTTPS dependencies

**Implementation Details:**
- Changed Kestrel configuration to use HTTP on port 8080 with binding to all interfaces:
  ```json
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://0.0.0.0:8080"
      }
    }
  }
  ```
- Updated CORS service configuration to properly handle wildcard origin:
  ```csharp
  if (allowedOrigins.Length == 1 && allowedOrigins[0] == "*")
  {
      // Allow any origin
      policy.AllowAnyOrigin()
          .WithMethods("POST", "GET", "PUT", "DELETE", "PATCH")
          .AllowAnyHeader();
  }
  ```
- Optimized SignalR for HTTP WebSockets:
  ```csharp
  builder.Services.AddSignalR(options =>
  {
      options.EnableDetailedErrors = true;
      options.KeepAliveInterval = TimeSpan.FromMinutes(1);
      options.MaximumReceiveMessageSize = 1024 * 1024; // 1MB
  });
  ```
- Removed HTTPS certificate handling from Docker build

**Benefits:**
- Simplified deployment in Kubernetes or Docker environments
- Eliminated need for HTTPS certificates in development/testing
- Improved real-time communication performance
- Allowed any frontend service to connect during development
- Reduced configuration complexity for in-cluster communication

### 2025-03-28: Docker Build Fix for Preview Packages

Fixed an issue with Docker builds failing due to Semantic Kernel preview package dependencies.

**Key Changes:**
- Updated `docker/webapi/Dockerfile.simple` to properly handle preview/alpha packages
- Added proper copying of `Directory.Build.props` and `Directory.Packages.props` to the Docker build context
- Created a custom NuGet.config directly in the container with `includePrerelease` set to `true`
- Fixed package restore errors for Microsoft.SemanticKernel.* preview and alpha packages

**Fix Details:**
```dockerfile
# Copy central package management files
COPY Directory.Build.props .
COPY Directory.Packages.props .

# Create a custom NuGet.config that enables preview packages
RUN echo '<?xml version="1.0" encoding="utf-8"?><configuration><packageSources><clear /><add key="nuget.org" value="https://api.nuget.org/v3/index.json" /></packageSources><packageSourceMapping><packageSource key="nuget.org"><package pattern="*" /></packageSource></packageSourceMapping><config><add key="globalPackagesFolder" value="/src/nuget-packages" /><add key="includePrerelease" value="true" /></config><packageRestore><add key="enabled" value="True" /><add key="automatic" value="True" /></packageRestore></configuration>' > /src/NuGet.config

# Use the custom NuGet.config
RUN dotnet restore webapi/CopilotChatWebApi.csproj --configfile "/src/NuGet.config"
```

**Benefits:**
- Fixed Docker build errors with Semantic Kernel preview/alpha packages
- Ensured Docker builds respect central package version management
- Documented solution for similar issues in the future

### 2025-03-21: Context-Specific Chat Sessions and Kernels

Enhanced the application architecture to support context-specific chat sessions and kernels. This allows users to create different chat environments for different contexts (e.g., different YouTube channels), each with its own configuration, memory, and conversation history.

**Key Changes:**
- Added `ContextId` property to ChatSession model with default value for backward compatibility
- Modified ChatSessionRepository to filter by both userId and contextId
- Updated API models to pass contextId through the application:
  - Enhanced CreateChatParameters to include contextId
  - Added contextId to Ask model for message processing
- Updated ChatHistoryController with new endpoints for context-specific operations:
  - GetChatSessionsByContextIdAsync to filter chats by context
  - Updated GetAllChatSessionsAsync to support optional context filtering
- Improved ChatController to select the appropriate kernel based on contextId
- Modified kernel dependency injection:
  - Removed direct Kernel dependency in favor of IKernelManager
  - Updated MaintenanceMiddleware to work without direct kernel dependency
- Enhanced IKernelManager interface to support contextId parameter
- Modified KernelManager to index kernels by "userId:contextId" composite key
- Created UserKernelInfo class to store kernel instances with user and context information
- Added UserKernelConfig class to store user and context-specific settings
- Implemented IUserKernelConfigRepository and UserKernelConfigRepository for persistent storage
- Created KernelCleanupService as a background service to manage inactive kernels
- Added UserConfigController with endpoints for managing context-specific configurations:
  - GetUserConfigAsync - Get configuration for current context
  - GetUserConfigByContextAsync - Get configuration for specific context
  - GetAllUserConfigsAsync - Get all configurations for a user
  - UpdateUserConfigAsync - Update configuration for current context
  - ResetUserConfigAsync - Reset to default configuration
  - ResetUserConfigByContextAsync - Reset specific context configuration
  - ResetAllUserConfigsAsync - Reset all user configurations
- Enhanced SemanticKernelProvider to support user-specific model configurations
- Modified SemanticKernelExtensions to remove direct kernel registration
- Updated Program.cs to register the new kernel management services
- Ensured backward compatibility for existing chat sessions and API clients

**Implementation Details:**
- Composite Key Strategy: Using "userId:contextId" format to uniquely identify kernel instances
- Lazy Loading: Kernels are created on-demand and cached for subsequent requests
- Resource Management: Background cleanup for inactive kernels to prevent memory leaks
- Default Fallbacks: Using "default" as the contextId when none is provided
- Context Isolation: Each context has independent memory, plugins, and settings
- Configuration Inheritance: Context configs can inherit from user's default settings
- Thread Safety: Using ConcurrentDictionary for managing concurrent kernel access
- Error Resilience: Proper exception handling to prevent kernel creation failures
- Dependency Resolution: Direct DI registration of IKernelManager for simpler lifecycle

**Benefits:**
- Support for channel-specific chat sessions and memory
- Isolation of conversation contexts for the same user
- Per-context configuration for AI models and plugins
- Better organization of conversations by topic or channel
- Improved scalability by removing unnecessary dependencies
- Foundation for more advanced context-aware features
- Memory efficiency through proper kernel lifecycle management
- Enhanced user customization capabilities
- Better organization of related conversations

### 2025-03-22: Kernel Management API Enhancements

Enhanced the kernel management API with new endpoints to create and manage kernels directly through Swagger UI. This provides administrators and developers with greater control over kernel instances.

**Key Changes:**
- Added new endpoints to the `KernelController` to create kernels programmatically:
  - `POST /api/kernel/create` - Create a kernel with simplified options using the new `CreateKernelRequest` model
  - `POST /api/kernel/create/full` - Create a kernel with full configuration options using the complete `UserKernelConfig` model
- Created a new request model `CreateKernelRequest` to simplify kernel creation from Swagger UI
- Implemented automatic saving of kernel configurations during creation
- Added proper error handling and validation for kernel creation requests
- Ensured created kernels reflect user-specified parameters like model options and plugins

**Benefits:**
- Ability to create kernels on-demand through Swagger UI
- More control over kernel lifecycle for testing and development
- Simpler API for creating kernels with default options
- Advanced API for creating fully customized kernels
- Easy kernel instance management for administrators

### 2025-03-21: Per-User Kernel Implementation

Added a comprehensive per-user kernel management system according to the design in the class diagram. This allows each user to have their own isolated Semantic Kernel instance with user-specific configurations for AI models, plugins, and other settings.

**Key Changes:**
- Created core models for managing user kernel configurations (`LLMOptions`, `UserKernelConfig`, `UserKernelInfo`)
- Implemented a repository pattern for storing user kernel configurations (`IUserKernelConfigRepository`, `UserKernelConfigRepository`)
- Added a kernel management service (`IKernelManager`, `KernelManager`) to handle user-specific kernels
- Created a background cleanup service (`KernelCleanupService`) to manage inactive kernels
- Updated `SemanticKernelProvider` to support user-specific configurations
- Modified `SemanticKernelExtensions` to register kernels through the kernel manager
- Added a new controller (`UserConfigController`) for managing user-specific configurations
- Updated `MaintenanceMiddleware` to clear kernels during maintenance
- Added configuration support in `appsettings.json` for kernel management settings

**Benefits:**
- Isolation between user sessions, preventing cross-user contamination
- Support for user-specific AI model settings (endpoints, models, parameters)
- Ability for users to customize their experience with different plugins and settings
- Improved resource management through cleanup of inactive kernels
- Foundation for future user-specific features