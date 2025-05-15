# Kernel Creation API Reference

This document provides details on how to interact with the Chat Copilot WebAPI to create and manage user/channel-specific kernels.

## Core Concepts

Each kernel in Chat Copilot is:
- User-specific (tied to a `userId`)
- Context-specific (tied to a `contextId`, which can represent a channel ID or other context)
- Configurable with custom settings for LLMs, plugins, and other parameters

## API Endpoints

### Create a Kernel

Creates or refreshes a kernel for a user in a specific context (e.g., channel).

**Endpoint:** `POST /api/kernel/create`

**Authentication:** Required

**Request Body:**

```json
{
  "contextId": "youtube-channel-123", // Optional, defaults to "default" if not provided
  "completionOptions": { // Optional
    "modelId": "gpt-4", // Optional
    "endpoint": "https://your-endpoint.com", // Optional
    "temperature": 0.7, // Optional, default: 0.7
    "maxTokens": 2000 // Optional, default: 2000
  },
  "embeddingOptions": { // Optional
    "modelId": "text-embedding-ada-002", // Optional
    "endpoint": "https://your-endpoint.com", // Optional
    "temperature": 0.0, // Optional, default: 0.7
    "maxTokens": 8192 // Optional, default: 2000
  },
  "enabledPlugins": ["PluginName1", "PluginName2"] // Optional
}
```

**Response:**

```json
{
  "userId": "user-123",
  "contextId": "youtube-channel-123",
  "lastAccessTime": "2023-05-13T12:34:56.789Z",
  "plugins": [
    {
      "name": "PluginName1",
      "functions": ["Function1", "Function2"]
    },
    {
      "name": "PluginName2",
      "functions": ["Function1", "Function2"]
    }
  ],
  "modelInfo": {
    "completionModelId": "gpt-4",
    "temperature": 0.7,
    "maxTokens": 2000
  }
}
```

### Create a Kernel with Full Configuration

Creates or refreshes a kernel with a complete configuration object.

**Endpoint:** `POST /api/kernel/create/full`

**Authentication:** Required

**Request Body:**

```json
{
  "userId": "user-123", // Must match the authenticated user
  "contextId": "youtube-channel-123", // Optional, defaults to "default"
  "settings": {}, // Optional dictionary for generic settings
  "completionOptions": {
    "modelId": "gpt-4",
    "endpoint": "https://your-endpoint.com",
    "temperature": 0.7,
    "maxTokens": 2000
  },
  "embeddingOptions": {
    "modelId": "text-embedding-ada-002",
    "endpoint": "https://your-endpoint.com",
    "temperature": 0.0,
    "maxTokens": 8192
  },
  "enabledPlugins": ["PluginName1", "PluginName2"],
  "apiKeys": {}, // Optional dictionary for API keys
  "contextSettings": {} // Optional dictionary for context-specific settings
}
```

**Response:** Same as the create endpoint

### Get Kernel Info

Retrieves information about the current user's kernel for a specific context.

**Endpoint:** `GET /api/kernel/info`

**Authentication:** Required

**Query Parameters:**
- `contextId` (optional): The context ID to get the kernel for. Defaults to "default".

**Response:** Same as the create endpoint responses

### Release a Kernel

Releases a kernel for the current user and context, removing it from memory.

**Endpoint:** `DELETE /api/kernel/release`

**Authentication:** Required

**Query Parameters:**
- `contextId` (optional): The context ID to release. Defaults to "default".
- `releaseAllContexts` (optional): Whether to release all kernels for the user. Defaults to false.

**Response:** 204 No Content

## Implementation Details

1. Each kernel is stored in memory with a composite key: `{userId}:{contextId}`
2. The `contextId` parameter is used to differentiate between different contexts (e.g., different channels)
3. When a new kernel is requested, it checks for an existing configuration:
   - If found, it uses those settings
   - If not, it creates a kernel with default settings
4. Kernels are automatically released after a period of inactivity by the cleanup service

## Examples

### Create a Channel-Specific Kernel

```http
POST /api/kernel/create HTTP/1.1
Host: your-api-host.com
Content-Type: application/json
Authorization: Bearer <token>

{
  "contextId": "channel-123",
  "completionOptions": {
    "modelId": "gpt-4",
    "temperature": 0.5
  },
  "enabledPlugins": ["WebSearcher"]
}
```

### Get Kernel Info for a Channel

```http
GET /api/kernel/info?contextId=channel-123 HTTP/1.1
Host: your-api-host.com
Authorization: Bearer <token>
```

### Release a Channel-Specific Kernel

```http
DELETE /api/kernel/release?contextId=channel-123 HTTP/1.1
Host: your-api-host.com
Authorization: Bearer <token>
```

## Best Practices

1. Always specify a meaningful `contextId` that uniquely identifies your channel or context
2. Release kernels when they're no longer needed to free up resources
3. Handle kernel creation errors gracefully in your application
4. Consider caching the kernel creation status in your application to avoid unnecessary API calls