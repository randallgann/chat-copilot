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

## Per-User Kernel Implementation Class Diagram

```mermaid
classDiagram
    class IKernelManager {
        <<interface>>
        +GetUserKernelAsync(string userId) Task~Kernel~
        +ReleaseUserKernelAsync(string userId) Task
        +ClearAllKernelsAsync() Task
    }

    class KernelManager {
        <<singleton>>
        -ConcurrentDictionary~string, UserKernelInfo~ _userKernels
        -ILogger _logger
        -IServiceProvider _serviceProvider
        -IUserKernelConfigRepository _configRepository
        +GetUserKernelAsync(string userId) Task~Kernel~
        +ReleaseUserKernelAsync(string userId) Task
        +ClearAllKernelsAsync() Task
        -CreateUserKernelInfoAsync(string userId) Task~UserKernelInfo~
        -ApplyUserConfigToKernel(Kernel kernel, UserKernelConfig config) void
    }
    
    note for KernelManager "Registered as a singleton in DI\nManages UserKernelInfo objects, not Kernels directly\nDictionary key is UUID (e.g., '79c1a50b-ed7a-426c-a510-f995a87a3350')"

    class UserKernelInfo {
        +Kernel Kernel
        +DateTime LastAccessTime
        +string UserId
        +UpdateLastAccessTime() void
        +GetKernel() Kernel
    }
    
    note for UserKernelInfo "Container for user-specific Kernel\nUserId is PostgreSQL UUID primary key"

    class UserKernelConfig {
        +string UserId
        +Dictionary~string, object~ Settings
        +LLMOptions CompletionOptions
        +LLMOptions EmbeddingOptions
        +List~string~ EnabledPlugins
        +Dictionary~string, string~ ApiKeys
    }
    
    note for UserKernelConfig "User-specific overrides\nfor kernel configuration"

    class IUserKernelConfigRepository {
        <<interface>>
        +GetConfigAsync(string userId) Task~UserKernelConfig~
        +SaveConfigAsync(UserKernelConfig config) Task
        +DeleteConfigAsync(string userId) Task
    }

    class UserKernelConfigRepository {
        -IStorageContext _storageContext
        +GetConfigAsync(string userId) Task~UserKernelConfig~
        +SaveConfigAsync(UserKernelConfig config) Task
        +DeleteConfigAsync(string userId) Task
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
        +GetUserConfigAsync() Task~IActionResult~
        +UpdateUserConfigAsync(UserKernelConfig config) Task~IActionResult~
        +ResetUserConfigAsync() Task~IActionResult~
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
    ChatController --> IKernelManager : uses
    ChatMemoryController --> IKernelManager : uses
    PluginController --> IKernelManager : uses
    MaintenanceMiddleware --> IKernelManager : uses
```