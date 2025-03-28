# Minimal Chat Copilot WebAPI Setup

This setup provides a minimal configuration for running just the Chat Copilot WebAPI component without Azure services, RabbitMQ, or the web frontend.

## Configuration Options

The minimal setup is configured to use:

1. **Local Vector Database**: 
   - Qdrant for vector storage (included in docker-compose)
   - SimpleVectorDb as a fallback option (file-based, no external dependencies)

2. **Local Text Generation**: 
   - Configured to use Ollama by default
   - Expects Ollama to be running on the host machine at http://localhost:11434
   - You must run Ollama separately with `llama2` model pulled

3. **Authentication**: 
   - Set to "None" for simplicity
   - Can be changed in the Dockerfile.minimal or via environment variables

4. **Document Processing**:
   - Uses Tesseract OCR for image text extraction
   - SimpleFileStorage for document storage

## Building and Running

### Option 1: Using Docker Compose (recommended)

```bash
# From the project root directory
cd docker
docker-compose -f docker-compose.minimal.yaml up --build
```

This will start:
- The Chat Copilot WebAPI on port 8080
- Qdrant vector database on port 6333

### Option 2: Build and Run WebAPI Only

```bash
# From the project root directory
docker build -f docker/webapi/Dockerfile.minimal -t chat-copilot-webapi-minimal .
docker run -p 8080:8080 --add-host=host.docker.internal:host-gateway chat-copilot-webapi-minimal
```

## Using with an External LLM

The minimal setup is configured to use Ollama for text generation and embeddings. You need to:

1. Install Ollama from https://ollama.ai/
2. Pull the llama2 model:
   ```bash
   ollama pull llama2
   ```
3. Start Ollama:
   ```bash
   ollama serve
   ```

You can change the LLM settings by modifying the environment variables in the docker-compose file or by mounting a custom appsettings.json file.

## Testing

The Chat Copilot WebAPI will be available at: http://localhost:8080

You can check the API status using:
```bash
curl http://localhost:8080/healthz
```

## Troubleshooting

- If you're using a different Ollama model, update the model name in the Docker environment variables
- For connection issues to Ollama, check that it's running and accessible from Docker
- If you prefer a different vector database, you can disable Qdrant and use SimpleVectorDb by commenting out the Qdrant-related environment variables