# Azure AI Agents Starter App

A simple console application demonstrating how to create and interact with Azure AI Agents using the Azure AI Foundry SDK.

## Overview

This starter app shows how to:
- Configure an Azure AI Agents client with Azure credentials
- Create an AI agent with specific instructions
- Send messages to the agent
- Process and display agent responses
- Clean up resources after use

## Prerequisites

- .NET 9.0 SDK or later
- An Azure subscription
- An Azure AI Foundry project with a deployed model
- Azure CLI (for authentication) or appropriate Azure credentials

## Configuration

Update the `appsettings.json` file with your Azure AI Foundry project details:

```json
{
  "ProjectEndpoint": "https://your-project.services.ai.azure.com/api/projects/YourProjectName",
  "ModelDeploymentName": "gpt-4o"
}
```

### Configuration Values

- **ProjectEndpoint**: Your Azure AI Foundry project endpoint URL
  - Format: `https://<your-project-name>.services.ai.azure.com/api/projects/<ProjectName>`
  - Find this in the Azure AI Foundry portal under your project settings

- **ModelDeploymentName**: The name of your deployed AI model
  - Common values: `gpt-4o`, `gpt-4`, `gpt-35-turbo`
  - This must match a model deployment in your Azure AI Foundry project

## Authentication

This app uses `DefaultAzureCredential` which supports multiple authentication methods in the following order:

1. **Environment variables** - Set these if running in a container or CI/CD:
   - `AZURE_CLIENT_ID`
   - `AZURE_TENANT_ID`
   - `AZURE_CLIENT_SECRET`

2. **Azure CLI** - Run `az login` before executing the app

3. **Managed Identity** - Automatically works when deployed to Azure services

4. **Visual Studio** or **Visual Studio Code** credentials

## Running the Application

### Using Visual Studio
1. Open the solution in Visual Studio
2. Set `Agents` as the startup project
3. Press F5 or click "Start"

### Using Command Line

```powershell
# Restore dependencies
dotnet restore

# Build the project
dotnet build

# Run the application
dotnet run
```

## What the App Does

1. **Loads Configuration**: Reads Azure settings from `appsettings.json`
2. **Creates an Agent**: Initializes a "Math Tutor" agent with specific instructions
3. **Creates a Thread**: Starts a new conversation thread
4. **Sends a Message**: Asks the agent to solve a math equation
5. **Processes the Response**: Waits for the agent to complete processing
6. **Displays Results**: Shows the conversation including the agent's response
7. **Cleans Up**: Deletes the thread and agent to avoid resource charges

## Example Output

```
Creating Azure AI Agent...
Agent created: Math Tutor (ID: asst_abc123...)
Thread created: thread_xyz789...

[User]: I need to solve the equation `3x + 11 = 14`. Can you help me?

Processing your request...
Run completed with status: Completed

=== Conversation ===
[user]: I need to solve the equation `3x + 11 = 14`. Can you help me?
[assistant]: Of course, Jane Doe! Let's solve the equation step by step...

Cleaning up resources...
Done!
```

## Project Structure

- **BasicAgent.cs** - Main starter application (currently active)
- **StreamingResponse.cs** - Example showing streaming responses (excluded from build)
- **OpenAPI.cs** - Example showing OpenAPI integration (excluded from build)
- **appsettings.json** - Configuration file for Azure settings
- **Agents.csproj** - Project file with dependencies

## Key Dependencies

- `Azure.AI.Agents.Persistent` (v1.2.0-beta.6) - Azure AI Agents SDK
- `Azure.Identity` (v1.17.0) - Azure authentication
- `Microsoft.Extensions.Configuration` - Configuration management
- `Microsoft.Extensions.Configuration.Json` - JSON configuration support

## Next Steps

Once you're comfortable with this basic example, explore:

1. **StreamingResponse.cs** - Learn how to get real-time streaming responses
2. **OpenAPI.cs** - See how to integrate custom APIs with agents
3. Custom tools and functions
4. Multiple agents and complex workflows
5. Persistent storage of conversations

## Troubleshooting

### Authentication Errors
- Ensure you're logged in with Azure CLI: `az login`
- Verify your account has access to the Azure AI Foundry project
- Check that your credentials have the necessary permissions

### Configuration Errors
- Verify `ProjectEndpoint` is correct (check Azure AI Foundry portal)
- Confirm `ModelDeploymentName` matches a deployed model in your project
- Ensure `appsettings.json` is copied to the output directory

### Build Errors
- Make sure you're using .NET 9.0 or later: `dotnet --version`
- Restore NuGet packages: `dotnet restore`
- Clean and rebuild: `dotnet clean && dotnet build`

## Resources

- [Azure AI Foundry Documentation](https://learn.microsoft.com/azure/ai-studio/)
- [Azure AI Agents SDK](https://learn.microsoft.com/azure/ai-studio/how-to/develop/sdk-overview)
- [DefaultAzureCredential](https://learn.microsoft.com/dotnet/api/azure.identity.defaultazurecredential)

## License

This is a sample application for educational purposes.
