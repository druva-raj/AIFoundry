using Azure.AI.Agents.Persistent;
using Samples.Common;
using System.Text.Json;

namespace Samples.Resources;

/// <summary>
/// Function calling capabilities with Azure AI Foundry agents.
/// Creates an agent that can call custom functions/tools.
/// </summary>
public class FunctionCalling : Base
{
    public FunctionCalling(PersistentAgentsClient agentClient, string modelDeploymentName)
        : base(agentClient, modelDeploymentName)
    {
    }

    public override string Name => "Function Calling";

    public override string Description => 
        "Function calling capabilities by creating an agent that can use custom tools " +
        "such as weather lookup, calculation functions, and data retrieval tools.";

    public override async Task RunAsync()
    {
        DisplayHeader();

        PersistentAgent? agent = null;
        PersistentAgentThread? thread = null;

        try
        {
            // Define custom function tools
            var weatherTool = CreateWeatherTool();
            var calculatorTool = CreateCalculatorTool();
            var dateTimeTool = CreateDateTimeTool();

            Console.WriteLine("Creating agent with function calling capabilities...");
            agent = await AgentClient.Administration.CreateAgentAsync(
                model: ModelDeploymentName,
                name: "FunctionDemo-Agent",
                instructions: "You are a helpful assistant with access to several tools. " +
                             "Use the available functions when appropriate to provide accurate information. " +
                             "Always explain what function you're calling and why.",
                tools: [weatherTool, calculatorTool, dateTimeTool]);

            Console.WriteLine($"Agent created: {agent.Name} (ID: {agent.Id})");
            Console.WriteLine($"Functions available: Weather lookup, Calculator, DateTime utilities");

            // Create a conversation thread
            Console.WriteLine("Creating conversation thread...");
            thread = await AgentClient.Threads.CreateThreadAsync();

            // Demonstrate function calling scenarios
            await DemoFunctionCall(thread, agent, 
                "What's the weather like in Seattle, WA right now?");

            await DemoFunctionCall(thread, agent, 
                "Can you calculate 15% tip on a $87.50 restaurant bill?");

            await DemoFunctionCall(thread, agent, 
                "What's the current date and time?");

            await DemoFunctionCall(thread, agent, 
                "What will the date be 30 days from now?");

            // Display full conversation
            await DisplayMessagesAsync(thread.Id);

            Console.WriteLine("\nFunction Calling completed successfully!");
            Console.WriteLine("The agent successfully used multiple custom functions to provide information.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during function calling: {ex.Message}");
        }
        finally
        {
            await CleanupAsync(agent, thread);
        }

        DisplayFooter();
    }

    /// <summary>
    /// Function call scenario.
    /// </summary>
    private async Task DemoFunctionCall(PersistentAgentThread thread, PersistentAgent agent, string userMessage)
    {
        Console.WriteLine($"\n[User]: {userMessage}");
        
        await AgentClient.Messages.CreateMessageAsync(thread.Id, MessageRole.User, userMessage);

        Console.WriteLine("Processing with function calls...");
        ThreadRun run = await AgentClient.Runs.CreateRunAsync(thread.Id, agent.Id);

        // Handle function calls
        run = await WaitForRunCompletionAsync(thread, run);

        if (run.Status == RunStatus.Completed)
        {
            Console.WriteLine("Function call completed successfully!");
        }
        else
        {
            Console.WriteLine($"Run status: {run.Status}");
        }
    }

    /// <summary>
    /// Creates a weather lookup function tool.
    /// </summary>
    private static FunctionToolDefinition CreateWeatherTool()
    {
        return new FunctionToolDefinition(
            name: "get_weather",
            description: "Get current weather information for a specified location",
            parameters: BinaryData.FromString(JsonSerializer.Serialize(new
            {
                type = "object",
                properties = new
                {
                    location = new
                    {
                        type = "string",
                        description = "The city and state/country (e.g., 'Seattle, WA' or 'London, UK')"
                    },
                    unit = new
                    {
                        type = "string",
                        @enum = new[] { "celsius", "fahrenheit" },
                        description = "Temperature unit preference"
                    }
                },
                required = new[] { "location" }
            })));
    }

    /// <summary>
    /// Creates a calculator function tool.
    /// </summary>
    private static FunctionToolDefinition CreateCalculatorTool()
    {
        return new FunctionToolDefinition(
            name: "calculate",
            description: "Perform mathematical calculations including basic arithmetic, percentages, and common formulas",
            parameters: BinaryData.FromString(JsonSerializer.Serialize(new
            {
                type = "object",
                properties = new
                {
                    expression = new
                    {
                        type = "string",
                        description = "Mathematical expression to evaluate (e.g., '15% of 87.50', '120 * 0.15', 'sqrt(16)')"
                    },
                    operation = new
                    {
                        type = "string",
                        @enum = new[] { "basic", "percentage", "tip", "compound_interest" },
                        description = "Type of calculation to perform"
                    }
                },
                required = new[] { "expression" }
            })));
    }

    /// <summary>
    /// Creates a date/time utility function tool.
    /// </summary>
    private static FunctionToolDefinition CreateDateTimeTool()
    {
        return new FunctionToolDefinition(
            name: "datetime_utility",
            description: "Get current date/time information or perform date calculations",
            parameters: BinaryData.FromString(JsonSerializer.Serialize(new
            {
                type = "object",
                properties = new
                {
                    operation = new
                    {
                        type = "string",
                        @enum = new[] { "current", "add_days", "subtract_days", "format", "timezone_convert" },
                        description = "DateTime operation to perform"
                    },
                    days = new
                    {
                        type = "integer",
                        description = "Number of days to add or subtract (for add_days/subtract_days operations)"
                    },
                    format = new
                    {
                        type = "string",
                        description = "Date format string (for format operation)"
                    },
                    timezone = new
                    {
                        type = "string",
                        description = "Target timezone (for timezone_convert operation)"
                    }
                },
                required = new[] { "operation" }
            })));
    }
}