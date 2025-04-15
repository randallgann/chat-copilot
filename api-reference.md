# Chat Copilot API Reference

## Overview

Chat Copilot is a conversational AI application built on Semantic Kernel that provides multi-user, context-specific chat capabilities. This document describes the API interface for programmatic interaction with Chat Copilot.

## Base URL

The default base URL for API endpoints is `http://localhost:8080` when running locally.

## Authentication

All endpoints require authentication. The user ID is used to manage user-specific resources like kernels and chat sessions.

## Core Concepts

- **Chat Sessions**: Conversations between users and the AI
- **Kernels**: User and context-specific Semantic Kernel instances
- **Context IDs**: Optional identifiers that isolate chat sessions and kernels for different application contexts (e.g., YouTube channels, Slack workspaces)
- **User Configs**: User and context-specific configurations for AI models and plugins

## API Endpoints

### Chat Management

#### Send Chat Message
- **Endpoint**: `POST /chats/{chatId}/messages`
- **Description**: Sends a message to a chat session and gets a response
- **Path Parameters**:
  - `chatId` (GUID): The ID of the chat session
- **Request Body**: `Ask` object
  ```json
  {
    "input": "Your message here",
    "variables": [
      { "key": "variable-name", "value": "variable-value" }
    ],
    "contextId": "optional-context-id"
  }
  ```
- **Response**: `AskResult` object
  ```json
  {
    "value": "Bot response text",
    "variables": [
      { "key": "variable-name", "value": "variable-value" }
    ]
  }
  ```

#### Create Chat Session
- **Endpoint**: `POST /chats`
- **Description**: Creates a new chat session
- **Request Body**: `CreateChatParameters` object
  ```json
  {
    "title": "Chat Title",
    "contextId": "optional-context-id"
  }
  ```
- **Response**: `CreateChatResponse` object
  ```json
  {
    "chatSession": {
      "id": "guid",
      "title": "Chat Title",
      "createdOn": "timestamp",
      "systemDescription": "system prompt",
      "memoryBalance": 0.5,
      "enabledPlugins": [],
      "contextId": "optional-context-id"
    },
    "initialBotMessage": {
      "id": "guid",
      "chatId": "guid",
      "role": "bot",
      "content": "initial message",
      "createdOn": "timestamp"
    }
  }
  ```

#### Get Chat Sessions
- **Endpoint**: `GET /chats`
- **Description**: Gets all chat sessions for the current user
- **Query Parameters**:
  - `contextId` (optional): Filter chats by context ID
- **Response**: Array of `ChatSession` objects

#### Get Chat Sessions by Context
- **Endpoint**: `GET /chats/context/{contextId}`
- **Description**: Gets chat sessions filtered by context ID
- **Path Parameters**:
  - `contextId`: The context ID to filter by
- **Response**: Array of `ChatSession` objects

#### Get Chat Messages
- **Endpoint**: `GET /chats/{chatId}/messages`
- **Description**: Gets messages for a specific chat session
- **Path Parameters**:
  - `chatId` (GUID): The ID of the chat session
- **Query Parameters**:
  - `skip` (optional, default: 0): Number of messages to skip
  - `count` (optional, default: -1): Maximum number of messages to return
- **Response**: Array of `CopilotChatMessage` objects

#### Edit Chat Session
- **Endpoint**: `PATCH /chats/{chatId}`
- **Description**: Edits a chat session's properties
- **Path Parameters**:
  - `chatId` (GUID): The ID of the chat session
- **Request Body**: `EditChatParameters` object
  ```json
  {
    "title": "Updated Title",
    "systemDescription": "Updated System Description",
    "memoryBalance": 0.7
  }
  ```
- **Response**: Updated `ChatSession` object

#### Delete Chat Session
- **Endpoint**: `DELETE /chats/{chatId}`
- **Description**: Deletes a chat session and all associated data
- **Path Parameters**:
  - `chatId` (GUID): The ID of the chat session

### Kernel Management

#### Create Kernel
- **Endpoint**: `POST /api/kernel/create`
- **Description**: Creates a new kernel for the current user
- **Request Body**: `CreateKernelRequest` object
  ```json
  {
    "contextId": "optional-context-id",
    "completionOptions": {
      "modelId": "model-id",
      "endpoint": "endpoint-url",
      "temperature": 0.7,
      "maxTokens": 2000
    },
    "embeddingOptions": {
      "modelId": "embedding-model-id",
      "endpoint": "embedding-endpoint-url"
    },
    "enabledPlugins": ["plugin1", "plugin2"]
  }
  ```
- **Response**: `KernelInfoResponse` object

#### Get Kernel Info
- **Endpoint**: `GET /api/kernel/info`
- **Description**: Gets information about the current user's kernel
- **Query Parameters**:
  - `contextId` (optional): Context ID for the kernel
- **Response**: `KernelInfoResponse` object
  ```json
  {
    "userId": "user1",
    "contextId": "context1",
    "lastAccessTime": "timestamp",
    "plugins": [
      {
        "name": "GitHubPlugin",
        "functions": ["function1", "function2"]
      }
    ],
    "modelInfo": {
      "completionModelId": "model-id",
      "embeddingModelId": "embedding-model-id",
      "temperature": 0.7,
      "maxTokens": 2000
    }
  }
  ```

#### Release Kernel
- **Endpoint**: `DELETE /api/kernel/release`
- **Description**: Releases the current user's kernel
- **Query Parameters**:
  - `contextId` (optional): Context ID for the kernel
  - `releaseAllContexts` (optional, default: false): Whether to release all contexts

### User Configuration

#### Get User Config
- **Endpoint**: `GET /userconfig`
- **Description**: Gets the current user's kernel configuration
- **Query Parameters**:
  - `contextId` (optional): Context ID for the configuration
- **Response**: `UserKernelConfig` object
  ```json
  {
    "userId": "user1",
    "contextId": "context1",
    "settings": {},
    "completionOptions": {
      "modelId": "model-id",
      "endpoint": "endpoint-url",
      "temperature": 0.7,
      "maxTokens": 2000
    },
    "embeddingOptions": {
      "modelId": "embedding-model-id",
      "endpoint": "embedding-endpoint-url"
    },
    "enabledPlugins": ["plugin1", "plugin2"],
    "apiKeys": {},
    "contextSettings": {}
  }
  ```

#### Update User Config
- **Endpoint**: `POST /userconfig`
- **Description**: Updates the current user's kernel configuration
- **Request Body**: `UserKernelConfig` object
- **Response**: Updated `UserKernelConfig` object

#### Reset User Config
- **Endpoint**: `POST /userconfig/reset`
- **Description**: Resets a specific context configuration
- **Query Parameters**:
  - `contextId` (optional): Context ID for the configuration

## Common Workflow

1. **Create or select a chat session**
   - Create a new chat session with `POST /chats`
   - Or get existing chat sessions with `GET /chats`

2. **Configure the kernel (optional)**
   - Get current config with `GET /userconfig`
   - Update config with `POST /userconfig`

3. **Send messages and receive responses**
   - Send a message with `POST /chats/{chatId}/messages`
   - The response includes the AI's reply

4. **Manage chat sessions**
   - Edit properties with `PATCH /chats/{chatId}`
   - Delete with `DELETE /chats/{chatId}`

## Context ID Usage

The `contextId` parameter allows isolating conversations and configurations for different application contexts:

- When creating a chat: `POST /chats` with `contextId` in the request body
- When sending a message: `POST /chats/{chatId}/messages` with `contextId` in the request body
- When managing kernels: Include `contextId` as a query parameter
- When managing user configs: Include `contextId` as a query parameter

If not provided, a default context is used.

## Error Handling

All endpoints return standard HTTP status codes:
- 200/201: Success
- 400: Bad request (invalid parameters)
- 403: Forbidden (unauthorized access)
- 404: Resource not found
- 500: Server error