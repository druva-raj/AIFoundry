using Azure;
using Azure.AI.Agents.Persistent;
using Azure.Identity;
using Microsoft.Extensions.Configuration;

namespace Agents;

/// <summary>
/// A simple starter app demonstrating how to create and interact with an Azure AI Agent.
/// This example creates a Math Tutor agent that can solve mathematical equations.
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        // Load configuration from appsettings.json
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var projectEndpoint = configuration["ProjectEndpoint"];
        var modelDeploymentName = configuration["ModelDeploymentName"];
        var tenantId = configuration["AzureAd:TenantId"];
        var clientId = configuration["AzureAd:ClientId"];
        var clientSecret = configuration["AzureAd:ClientSecret"];

        // Validate configuration
        if (string.IsNullOrEmpty(projectEndpoint) || string.IsNullOrEmpty(modelDeploymentName))
        {
            Console.WriteLine("Error: Please configure ProjectEndpoint and ModelDeploymentName in appsettings.json");
            return;
        }

        if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            Console.WriteLine("Error: Please configure AzureAd:TenantId, AzureAd:ClientId, and AzureAd:ClientSecret in appsettings.json");
            return;
        }

        Console.WriteLine("Creating Azure AI Agent with Service Principal authentication...");
        
        // Initialize the client with Service Principal credentials
        var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
        PersistentAgentsClient client = new(projectEndpoint, credential);

        // Create an AI agent configured as a math tutor
        PersistentAgent agent = await client.Administration.CreateAgentAsync(
            model: modelDeploymentName,
            name: "BasicAgent",
            instructions: "You are a personal math tutor. Write and run code to answer math questions."
        );
        Console.WriteLine($"Agent created: {agent.Name} (ID: {agent.Id})");

        // Create a new conversation thread
        PersistentAgentThread thread = await client.Threads.CreateThreadAsync();
        Console.WriteLine($"Thread created: {thread.Id}");

        // Send a user message to the thread
        Console.WriteLine("\n[User]: I need to solve the equation `3x + 11 = 14`. Can you help me?");
        await client.Messages.CreateMessageAsync(
            thread.Id,
            MessageRole.User,
            "I need to solve the equation `3x + 11 = 14`. Can you help me?");

        // Create a run to process the message
        Console.WriteLine("\nProcessing your request...");
        ThreadRun run = await client.Runs.CreateRunAsync(
            thread.Id,
            agent.Id,
            additionalInstructions: "Please address the user as Jane Doe. The user has a premium account.");

        // Poll until the run completes
        do
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500));
            run = await client.Runs.GetRunAsync(thread.Id, run.Id);
        }
        while (run.Status == RunStatus.Queued
            || run.Status == RunStatus.InProgress
            || run.Status == RunStatus.RequiresAction);

        Console.WriteLine($"Run completed with status: {run.Status}\n");

        // Retrieve and display all messages in the thread
        var messages = client.Messages.GetMessagesAsync(
            threadId: thread.Id,
            order: ListSortOrder.Ascending);

        Console.WriteLine("=== Conversation ===");
        await foreach (var message in messages)
        {
            foreach (MessageContent content in message.ContentItems)
            {
                switch (content)
                {
                    case MessageTextContent textItem:
                        Console.WriteLine($"[{message.Role}]: {textItem.Text}");
                        break;
                }
            }
        }

        // Clean up resources
        Console.WriteLine("\nCleaning up resources...");
        await client.Threads.DeleteThreadAsync(threadId: thread.Id);
        await client.Administration.DeleteAgentAsync(agentId: agent.Id);
        Console.WriteLine("Done!");
    }
}