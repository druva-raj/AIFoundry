using Azure.AI.Agents.Persistent;
using Azure.Core;
using Azure.Core.Diagnostics;
using Azure.Identity;
using System.Diagnostics.Tracing;

namespace Samples.Common;

/// <summary>
/// Factory class for creating AI Foundry agent clients with proper authentication.
/// </summary>
public static class AgentClientFactory
{
    // Keep a reference to prevent garbage collection
    private static AzureEventSourceListener? _listener;

    /// <summary>
    /// Creates a PersistentAgentsClient using the provided configuration.
    /// </summary>
    /// <param name="config">The configuration containing authentication and endpoint information.</param>
    /// <param name="enableHttpLogging">Enable verbose HTTP request/response logging to see full prompts.</param>
    /// <returns>A configured PersistentAgentsClient.</returns>
    public static PersistentAgentsClient CreateClient(ConfigurationHelper.AIFoundryConfig config, bool enableHttpLogging = false)
    {
        // Create Service Principal credentials
        var credential = new ClientSecretCredential(
            config.TenantId, 
            config.ClientId, 
            config.ClientSecret);

        if (enableHttpLogging)
        {
            // Enable console logging to see HTTP request/response content
            // This will show the full JSON payloads including prompts, tool definitions, etc.
            _listener = AzureEventSourceListener.CreateConsoleLogger(EventLevel.Verbose);
            Console.WriteLine("[HTTP Logging] Enabled - full request/response bodies will be logged.");
        }

        // Create and return the client
        return new PersistentAgentsClient(config.ProjectEndpoint, credential);
    }

    /// <summary>
    /// Creates a PersistentAgentsClient using DefaultAzureCredential (for managed identity scenarios).
    /// </summary>
    /// <param name="projectEndpoint">The AI Foundry project endpoint.</param>
    /// <returns>A configured PersistentAgentsClient using DefaultAzureCredential.</returns>
    public static PersistentAgentsClient CreateClientWithDefaultCredential(string projectEndpoint)
    {
        return new PersistentAgentsClient(projectEndpoint, new DefaultAzureCredential());
    }

    /// <summary>
    /// Validates that the client can connect to the AI Foundry service.
    /// </summary>
    /// <param name="client">The client to test.</param>
    /// <returns>True if the connection is successful, false otherwise.</returns>
    public static async Task<bool> ValidateConnectionAsync(PersistentAgentsClient client)
    {
        try
        {
            // Try to list agents to validate the connection
            var agents = client.Administration.GetAgentsAsync().GetAsyncEnumerator();
            await agents.MoveNextAsync();
            await agents.DisposeAsync();
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Connection validation failed: {ex.Message}");
            return false;
        }
    }
}