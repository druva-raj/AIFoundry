# Azure AI Foundry Samples

Interactive examples demonstrating Azure AI Foundry capabilities.

## Features

- **Basic Agent**: AI agent creation and conversation
- **MCP Integration**: Microsoft Learn documentation search via Model Context Protocol  
- **Function Calling**: Custom tools (weather, calculator, datetime)
- **Streaming**: Real-time response generation

## Project Structure

```
Samples/
├── Program.cs                   # Main entry point
├── Common/
│   ├── Base.cs                 # Base class for samples
│   ├── ConfigurationHelper.cs  # Environment configuration
│   └── AgentClientFactory.cs   # Client factory
└── Resources/
    ├── BasicAgent.cs           # Basic agent sample
    ├── MCP.cs                  # MCP integration sample
    ├── FunctionCalling.cs      # Function calling sample
    └── Streaming.cs            # Streaming sample
```

## Prerequisites

- .NET 9.0 or later
- Azure AI Foundry project with deployed model
- Service Principal with appropriate permissions

## Configuration

Set environment variables:

```bash
PROJECT_ENDPOINT=https://your-project.cognitiveservices.azure.com/
MODEL_DEPLOYMENT_NAME=your-model-deployment-name
TENANT_ID=your-tenant-id
CLIENT_ID=your-client-id
CLIENT_SECRET=your-client-secret
```

## Usage

1. Build: `dotnet build`
2. Run: `dotnet run`
3. Select sample from menu