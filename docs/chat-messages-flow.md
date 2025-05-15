Create a unique kernel for userid/channelid
{
"userId": "your-user-id",
"contextId": "youtube-channel-123",
"settings": {},
"completionOptions": {
"modelId": "gpt-4o",
"endpoint": "",
"temperature": 0.7,
"maxTokens": 2000
},
"embeddingOptions": {
"modelId": "text-embedding-ada-002",
"endpoint": "",
"temperature": 0.0,
"maxTokens": 8191
},
"enabledPlugins": [],
"apiKeys": {},
"contextSettings": {}
}

Given a userid and contextid/channelid
userid - c05c61eb-65e4-4223-915a-fe72b0c9ece1
contextid - youtube-channel-123

Create a chatSession
"chatSession": {
"id": "a2d5d12d-81f1-4a04-9083-efafeb78b93d",
"title": "new chat",

Add messages to that chatSession using the chatId, along with providing an input message and the contextid
curl -X 'POST' \
 'http://localhost:8080/chats/a2d5d12d-81f1-4a04-9083-efafeb78b93d/messages' \
 -H 'accept: _/_' \
 -H 'Content-Type: application/json' \
 -d '{
"input": "Hello",
"variables": [
{
"key": "messageType",
"value": "message"
}
],
"contextId": "channel1"
}'

We go into ChatController ChatAsync - check chatSessionRepo / fail if chatSession id not found - check the participant / fail if not - get the contextid from the ask or chat or "default" - get the user kernel

- load plugins into kernel
- using the chatplugin for the kernel call chatAsync
  We go into ChatPlugin>ChatAsync
- go into SaveNewMessageAsync
- on chatMessageRepository call CreateAsync
- the ChatPlugin object holds - \_chatMessageRepository with ConcurrentDict of messages - get the llm response - return it
  Back out to ChatController
- message from llm is returned
  {
  "value": "Microsoft.SemanticKernel.KernelArguments",
  "variables": [
  {
  "key": "messageType",
  "value": "message"
  },
  {
  "key": "userId",
  "value": "c05c61eb-65e4-4223-915a-fe72b0c9ece1"
  },
  {
  "key": "userName",
  "value": "Default User"
  },
  {
  "key": "chatId",
  "value": "638bf8e2-6537-4e40-9f75-fd3fdcb671cf"
  },
  {
  "key": "message",
  "value": "Hello"
  },
  {
  "key": "input",
  "value": "Hello! How can I assist you today?"
  },
  {
  "key": "tokenUsage",
  "value": "{\"metaPromptTemplate\":1304,\"responseCompletion\":9}"
  }
  ]
  }

Given a chatid get all the messages for that chat

- In ChatHistoryController>GetChatMessagesAsync
  - search the \_sessionRepo for the chatId to make sure it exists
