# Chat Copilot Development Guidelines

## Build & Run Commands
- **Backend**: `dotnet run --project webapi/CopilotChatWebApi.csproj`
- **Frontend**: `cd webapp && yarn start`
- **Full App**: `./scripts/Start.ps1` (PowerShell) or `./scripts/start.sh` (Linux/Mac)
- **Run single test**: `dotnet test integration-tests/ChatCopilotIntegrationTests.csproj --filter "FullyQualifiedName~TestName"`
- **Lint frontend**: `cd webapp && yarn lint`
- **Format frontend**: `cd webapp && yarn format:fix`

## Code Style
- **C#**: Use .NET analyzers (already enabled in project)
- **TypeScript**: Use ESLint and Prettier with project settings
- **Imports**: Group by source (framework, then internal)
- **Naming**: 
  - C#: PascalCase for classes/methods/properties, camelCase for variables
  - TypeScript: PascalCase for types/interfaces/components, camelCase for variables/functions
- **Error Handling**: Use try/catch blocks with meaningful error messages
- **Async**: Always use async/await pattern with proper error handling
- **Type Safety**: Use strong typing, avoid 'any' in TypeScript
- **Components**: Create small, reusable components with props interfaces
- **Documentation**: Add XML comments to C# public APIs, JSDoc to complex TypeScript functions
- **Authentication**: Follow MS Identity patterns for auth features

## Multi-Kernel Implementation Class Diagram

```mermaid
classDiagram
    class IKernelManager {
        <<interface>>
        +GetUserKernelAsync(string userId, string? contextId) Task~Kernel~
        +ReleaseUserKernelAsync(string userId, string? contextId, bool releaseAllUserContexts) Task
        +ClearAllKernelsAsync() Task
    }

    class KernelManager {
        <<singleton>>
        -ConcurrentDictionary~string, UserKernelInfo~ _userKernels
        -ILogger _logger
        -IServiceProvider _serviceProvider
        -IUserKernelConfigRepository _configRepository
        +GetUserKernelAsync(string userId, string? contextId) Task~Kernel~
        +ReleaseUserKernelAsync(string userId, string? contextId, bool releaseAllUserContexts) Task
        +ClearAllKernelsAsync() Task
        -CreateUserKernelInfoAsync(string userId, string contextId) Task~UserKernelInfo~
        -ApplyUserConfigToKernel(Kernel kernel, UserKernelConfig config) void
    }
    
    note for KernelManager "Registered as a singleton in DI\nManages UserKernelInfo objects, not Kernels directly\nDictionary key is composite 'userId:contextId'"

    class UserKernelInfo {
        +Kernel Kernel
        +DateTime LastAccessTime
        +string UserId
        +string ContextId
        +UpdateLastAccessTime() void
        +GetKernel() Kernel
    }
    
    note for UserKernelInfo "Container for context-specific Kernel\nContextId identifies specific context (e.g., YouTube channel)"

    class UserKernelConfig {
        +string UserId
        +string ContextId
        +Dictionary~string, object~ Settings
        +LLMOptions CompletionOptions
        +LLMOptions EmbeddingOptions
        +List~string~ EnabledPlugins
        +Dictionary~string, string~ ApiKeys
        +Dictionary~string, object~ ContextSettings
    }
    
    note for UserKernelConfig "User and context-specific overrides\nContextSettings for context-specific data"

    class IUserKernelConfigRepository {
        <<interface>>
        +GetConfigAsync(string userId, string? contextId) Task~UserKernelConfig~
        +GetUserConfigsAsync(string userId) Task~IEnumerable~UserKernelConfig~~
        +SaveConfigAsync(UserKernelConfig config) Task
        +DeleteConfigAsync(string userId, string? contextId) Task
        +DeleteAllUserConfigsAsync(string userId) Task
    }

    class UserKernelConfigRepository {
        -IStorageContext _storageContext
        +GetConfigAsync(string userId, string? contextId) Task~UserKernelConfig~
        +GetUserConfigsAsync(string userId) Task~IEnumerable~UserKernelConfig~~
        +SaveConfigAsync(UserKernelConfig config) Task
        +DeleteConfigAsync(string userId, string? contextId) Task
        +DeleteAllUserConfigsAsync(string userId) Task
        -FindByUserIdAsync(string userId) Task~IEnumerable~UserKernelConfig~~
        -FindByUserIdAndContextIdAsync(string userId, string contextId) Task~IEnumerable~UserKernelConfig~~
    }

    class LLMOptions {
        +string ModelId
        +string Endpoint
        +float Temperature
        +int MaxTokens
    }

    class SemanticKernelProvider {
        -IConfiguration _configuration
        -IHttpClientFactory _httpClientFactory
        -IServiceProvider _serviceProvider
        +GetCompletionKernel(UserKernelConfig userConfig) Kernel
        -InitializeCompletionKernel(UserKernelConfig userConfig) Kernel
        -ApplyDefaultOrUserConfig(UserKernelConfig userConfig) KernelConfig
    }

    class KernelCleanupService {
        <<background service>>
        -IKernelManager _kernelManager
        -ILogger _logger
        -TimeSpan _cleanupInterval
        -TimeSpan _kernelMaxInactiveTime
        +ExecuteAsync(CancellationToken stoppingToken) Task
        -CleanupKernelsAsync() Task
    }
    
    class UserConfigController {
        -IUserKernelConfigRepository _configRepository
        -IAuthInfo _authInfo
        -IKernelManager _kernelManager
        +GetUserConfigAsync(string? contextId) Task~IActionResult~
        +GetAllUserConfigsAsync() Task~IActionResult~
        +UpdateUserConfigAsync(UserKernelConfig config) Task~IActionResult~
        +ResetUserConfigAsync(string? contextId) Task~IActionResult~
        +ResetAllUserConfigsAsync() Task~IActionResult~
    }

    class ChatController {
        -IKernelManager _kernelManager
        -IAuthInfo _authInfo
        +ChatAsync(...) Task~IActionResult~
    }

    class ChatMemoryController {
        -IKernelManager _kernelManager
        -IAuthInfo _authInfo
        +Various memory methods
    }

    class PluginController {
        -IKernelManager _kernelManager
        -IAuthInfo _authInfo
        +Various plugin methods
    }

    class MaintenanceMiddleware {
        -IKernelManager _kernelManager
        +InvokeAsync(...) Task
    }

    IKernelManager <|.. KernelManager : implements
    KernelManager o-- UserKernelInfo : contains
    KernelManager --> SemanticKernelProvider : uses
    KernelManager --> IUserKernelConfigRepository : uses
    IUserKernelConfigRepository <|.. UserKernelConfigRepository : implements
    UserKernelConfig o-- LLMOptions : contains
    KernelCleanupService --> IKernelManager : uses
    UserConfigController --> IUserKernelConfigRepository : uses
    UserConfigController --> IKernelManager : uses
    ChatController --> IKernelManager : uses
    ChatMemoryController --> IKernelManager : uses
    PluginController --> IKernelManager : uses
    MaintenanceMiddleware --> IKernelManager : uses
```

## Channel-Specific Chat Implementation Class Diagram

```mermaid
classDiagram
    class ChatSession {
        +string Id
        +string Title
        +DateTimeOffset CreatedOn
        +string SystemDescription
        +float MemoryBalance
        +HashSet~string~ EnabledPlugins
        +string ContextId [NEW]
        +string Version
        +string Partition
    }
    
    class ChatSessionRepository {
        -IStorageContext<ChatSession> _storageContext
        +GetAllChatsAsync() Task~IEnumerable~ChatSession~~
        +FindByChatIdAsync(string chatId) Task~ChatSession~
        +FindByUserIdAndContextIdAsync(string userId, string contextId) Task~IEnumerable~ChatSession~~ [NEW]
    }
    
    class ChatParticipant {
        +string Id
        +string UserId
        +string ChatId
        +string Partition
    }
    
    class ChatParticipantRepository {
        -IStorageContext<ChatParticipant> _storageContext
        +FindByUserIdAsync(string userId) Task~IEnumerable~ChatParticipant~~
        +IsUserInChatAsync(string userId, string chatId) Task~bool~
    }
    
    class ChatHistoryController {
        -ChatSessionRepository _sessionRepository
        -ChatParticipantRepository _participantRepository
        -IAuthInfo _authInfo
        +CreateChatAsync(CreateChatParameters parameters) Task~IActionResult~
        +GetAllChatSessionsAsync() Task~IActionResult~
        +GetChatSessionsByContextIdAsync(string contextId) Task~IActionResult~ [NEW]
    }
    
    class CreateChatParameters {
        +string Title
        +string SystemDescription
        +float MemoryBalance
        +string? ContextId [NEW]
    }
    
    class UserKernelInfo {
        +string UserId
        +string ContextId
        +Kernel Kernel
        +DateTime LastAccessTime
    }
    
    class KernelManager {
        -ConcurrentDictionary~string, UserKernelInfo~ _userKernels
        +GetUserKernelAsync(string userId, string? contextId) Task~Kernel~
    }
    
    class ChatController {
        -IKernelManager _kernelManager
        +ChatAsync(Kernel kernel, Ask ask, Guid chatId, string? contextId) Task~IActionResult~
    }
    
    class IKernelManager {
        <<interface>>
        +GetUserKernelAsync(string userId, string? contextId) Task~Kernel~
    }
    
    ChatSession -- ChatSessionRepository: managed by
    ChatParticipant -- ChatParticipantRepository: managed by
    ChatHistoryController --> ChatSessionRepository: uses
    ChatHistoryController --> ChatParticipantRepository: uses
    ChatHistoryController ..> CreateChatParameters: creates from
    ChatController --> IKernelManager: uses
    KernelManager ..|> IKernelManager: implements
    KernelManager o-- UserKernelInfo: contains
    ChatController ..> ChatSession: retrieves
```