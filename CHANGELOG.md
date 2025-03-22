# Chat Copilot Changelog

This document tracks significant changes to the Chat Copilot codebase, particularly focusing on feature additions, architecture improvements, and critical fixes.

## Purpose
This changelog serves as a reference for developers working on Chat Copilot to track the evolution of the codebase and understand the context behind changes.

## Changes

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