using Azure.AI.Agents.Persistent;
using Samples.Common;
using System.Diagnostics;

namespace Samples.Resources;

/// <summary>
/// MCP (Model Context Protocol) integration with Azure AI Foundry.
/// Creates an agent that can use MCP tools to search Microsoft Learn documentation.
/// </summary>
public class MCP : Base
{
    private static readonly ActivitySource ActivitySource = new("MCPAgentSample");
    private const string MCP_SERVER_LABEL = "search_mslearn_docs";
    private const string MCP_SERVER_URL = "https://learn.microsoft.com/api/mcp";

    public MCP(PersistentAgentsClient agentClient, string modelDeploymentName)
        : base(agentClient, modelDeploymentName)
    {
    }

    public override string Name => "MCP Integration";

    public override string Description => 
        "Model Context Protocol integration by creating an agent that can search " +
        "Microsoft Learn documentation using MCP tools. The agent will answer questions about " +
        "Azure services by searching official documentation.";

    public override async Task RunAsync()
    {
        DisplayHeader();

        PersistentAgent? agent = null;
        PersistentAgentThread? thread = null;

        try
        {
            using var activity = ActivitySource.StartActivity("MCP Agent Interaction");
            activity?.SetTag("mcp.server.label", MCP_SERVER_LABEL);
            activity?.SetTag("mcp.server.url", MCP_SERVER_URL);

            // Create MCP tool definition
            Console.WriteLine($"Setting up MCP integration with {MCP_SERVER_URL}...");
            MCPToolDefinition mcpTool = new(MCP_SERVER_LABEL, MCP_SERVER_URL);

            // Create an AI agent with MCP tools
            Console.WriteLine("Creating agent with MCP capabilities...");
            agent = await AgentClient.Administration.CreateAgentAsync(
                model: ModelDeploymentName,
                name: "mslearn-mcp-agent",
                instructions: "You are a helpful agent that can use MCP tools to assist users. " +
                             "Use the available MCP tools to answer questions and perform tasks. " +
                             "When searching for information, provide comprehensive and accurate responses " +
                             "based on the official Microsoft documentation.",
                tools: [mcpTool]);

            Console.WriteLine($"Agent created: {agent.Name} (ID: {agent.Id})");
            activity?.SetTag("agent.id", agent.Id);
            activity?.SetTag("agent.name", agent.Name);

            // Create a conversation thread
            Console.WriteLine("Creating conversation thread...");
            thread = await AgentClient.Threads.CreateThreadAsync();
            Console.WriteLine($"Thread created: {thread.Id}");
            activity?.SetTag("thread.id", thread.Id);

            // Demonstrate MCP functionality with a sample query
            string userQuestion = "How to connect to Cosmos DB via Python SDK?";
            Console.WriteLine($"\n[User]: {userQuestion}");
            
            // Create message in thread
            await AgentClient.Messages.CreateMessageAsync(
                thread.Id,
                MessageRole.User,
                userQuestion);

            // Set up MCP tool resources with auto-approval
            MCPToolResource mcpToolResource = new(MCP_SERVER_LABEL);
            mcpToolResource.RequireApproval = new MCPApproval("never"); // Auto-approve for demo
            ToolResources toolResources = mcpToolResource.ToToolResources();

            // Create and run the agent
            Console.WriteLine("\nProcessing request with MCP tools...");
            ThreadRun run = await AgentClient.Runs.CreateRunAsync(thread, agent, toolResources);

            // Wait for completion and handle any required actions
            run = await WaitForRunCompletionAsync(thread, run);

            // Check run status
            activity?.SetTag("run.status", run.Status.ToString());
            if (run.Status != RunStatus.Completed)
            {
                Console.WriteLine($"Run did not complete successfully. Status: {run.Status}");
                activity?.SetStatus(ActivityStatusCode.Error, $"Run failed with status: {run.Status}");
                if (run.LastError != null)
                {
                    Console.WriteLine($"Error: {run.LastError.Message}");
                    activity?.SetTag("run.error", run.LastError.Message);
                }
                return;
            }
            activity?.SetStatus(ActivityStatusCode.Ok);

            // Display the run steps (tool calls made)
            await DisplayRunStepsAsync(run);

            // Display the conversation
            await DisplayMessagesAsync(thread.Id);

            Console.WriteLine("\nMCP completed successfully!");
            Console.WriteLine("The agent successfully used MCP tools to search Microsoft Learn documentation.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during MCP: {ex.Message}");
        }
        finally
        {
            // Clean up resources
            //await CleanupAsync(agent, thread);
        }

        DisplayFooter();
    }

    /// <summary>
    /// Displays the run steps to show what MCP tools were called and adds telemetry.
    /// </summary>
    private async Task DisplayRunStepsAsync(ThreadRun run)
    {
        Console.WriteLine("\n=== MCP Tool Calls ===");
        
        IReadOnlyList<RunStep> runSteps = [.. AgentClient.Runs.GetRunSteps(run: run)];
        
        foreach (RunStep step in runSteps)
        {
            // Create a span for each run step with detailed telemetry
            using var stepActivity = ActivitySource.StartActivity($"RunStep_{step.Type}");
            stepActivity?.SetTag("gen_ai.system", "az.ai.agents");
            stepActivity?.SetTag("gen_ai.thread.id", run.ThreadId);
            stepActivity?.SetTag("gen_ai.agent.id", run.AssistantId);
            stepActivity?.SetTag("gen_ai.thread.run.id", run.Id);
            stepActivity?.SetTag("gen_ai.run_step.id", step.Id);
            stepActivity?.SetTag("gen_ai.run_step.type", step.Type.ToString());
            stepActivity?.SetTag("gen_ai.run_step.status", step.Status.ToString());
            
            stepActivity?.SetTag("gen_ai.run_step.created_at", step.CreatedAt.ToUnixTimeSeconds());
            if (step.CompletedAt.HasValue)
                stepActivity?.SetTag("gen_ai.run_step.completed_at", step.CompletedAt.Value.ToUnixTimeSeconds());
            
            // Add usage information if available
            if (step.Usage != null)
            {
                stepActivity?.SetTag("gen_ai.usage.input_tokens", step.Usage.PromptTokens);
                stepActivity?.SetTag("gen_ai.usage.output_tokens", step.Usage.CompletionTokens);
                stepActivity?.SetTag("gen_ai.usage.total_tokens", step.Usage.TotalTokens);
            }

            if (step.StepDetails is RunStepToolCallDetails toolCallDetails)
            {
                var toolCallsList = new List<object>();
                int toolCallIndex = 0;
                
                foreach (var toolCall in toolCallDetails.ToolCalls)
                {
                    if (toolCall is RunStepMcpToolCall mcpToolCall)
                    {
                        Console.WriteLine($"MCP Tool Call: {mcpToolCall.ServerLabel}.{mcpToolCall.Name}");
                        Console.WriteLine($"   ID: {mcpToolCall.Id}");
                        Console.WriteLine($"   Server: {mcpToolCall.ServerLabel}");
                        Console.WriteLine($"   Arguments: {mcpToolCall.Arguments}");
                        Console.WriteLine($"   Output: {mcpToolCall.Output ?? "(no output)"}");
                        Console.WriteLine();

                        // Add detailed telemetry for MCP tool call
                        stepActivity?.SetTag($"gen_ai.tool_call.{toolCallIndex}.id", mcpToolCall.Id);
                        stepActivity?.SetTag($"gen_ai.tool_call.{toolCallIndex}.type", "mcp");
                        stepActivity?.SetTag($"gen_ai.tool_call.{toolCallIndex}.server_label", mcpToolCall.ServerLabel);
                        stepActivity?.SetTag($"gen_ai.tool_call.{toolCallIndex}.name", mcpToolCall.Name);
                        stepActivity?.SetTag($"gen_ai.tool_call.{toolCallIndex}.arguments", mcpToolCall.Arguments);
                        if (!string.IsNullOrEmpty(mcpToolCall.Output))
                            stepActivity?.SetTag($"gen_ai.tool_call.{toolCallIndex}.output_length", mcpToolCall.Output.Length);

                        toolCallsList.Add(new
                        {
                            id = mcpToolCall.Id,
                            type = "mcp",
                            server_label = mcpToolCall.ServerLabel,
                            name = mcpToolCall.Name,
                            arguments = mcpToolCall.Arguments,
                            has_output = !string.IsNullOrEmpty(mcpToolCall.Output)
                        });
                        toolCallIndex++;
                    }
                    else if (toolCall is RunStepFunctionToolCall functionToolCall)
                    {
                        Console.WriteLine($"Function Call: {functionToolCall.Name}");
                        Console.WriteLine($"   ID: {functionToolCall.Id}");
                        Console.WriteLine($"   Arguments: {functionToolCall.Arguments}");
                        Console.WriteLine();

                        stepActivity?.SetTag($"gen_ai.tool_call.{toolCallIndex}.id", functionToolCall.Id);
                        stepActivity?.SetTag($"gen_ai.tool_call.{toolCallIndex}.type", "function");
                        stepActivity?.SetTag($"gen_ai.tool_call.{toolCallIndex}.name", functionToolCall.Name);
                        stepActivity?.SetTag($"gen_ai.tool_call.{toolCallIndex}.arguments", functionToolCall.Arguments);
                        toolCallIndex++;
                    }
                }

                // Add JSON summary of all tool calls
                if (toolCallsList.Count > 0)
                {
                    stepActivity?.SetTag("gen_ai.tool_calls.count", toolCallsList.Count);
                    stepActivity?.SetTag("gen_ai.tool_calls.summary", System.Text.Json.JsonSerializer.Serialize(toolCallsList));
                }
            }
            else if (step.StepDetails is RunStepActivityDetails activityDetails)
            {
                foreach (RunStepDetailsActivity activity in activityDetails.Activities)
                {
                    foreach (KeyValuePair<string, ActivityFunctionDefinition> activityFunction in activity.Tools)
                    {
                        Console.WriteLine($"Function: {activityFunction.Key}");
                        Console.WriteLine($"   Description: {activityFunction.Value.Description}");
                        
                        stepActivity?.SetTag($"gen_ai.activity.function.{activityFunction.Key}.description", activityFunction.Value.Description);
                        
                        if (activityFunction.Value.Parameters.Properties.Count > 0)
                        {
                            Console.WriteLine("   Parameters:");
                            var paramNames = new List<string>();
                            foreach (KeyValuePair<string, FunctionArgument> arg in activityFunction.Value.Parameters.Properties)
                            {
                                Console.WriteLine($"     • {arg.Key} ({arg.Value.Type})");
                                if (!string.IsNullOrEmpty(arg.Value.Description))
                                    Console.WriteLine($"       {arg.Value.Description}");
                                paramNames.Add(arg.Key);
                            }
                            stepActivity?.SetTag($"gen_ai.activity.function.{activityFunction.Key}.parameters", string.Join(",", paramNames));
                        }
                        else
                        {
                            Console.WriteLine("   No parameters required");
                        }
                        Console.WriteLine();
                    }
                }
            }
            else if (step.StepDetails is RunStepMessageCreationDetails messageDetails)
            {
                stepActivity?.SetTag("gen_ai.message.id", messageDetails.MessageCreation.MessageId);
                Console.WriteLine($"Message Created: {messageDetails.MessageCreation.MessageId}");
            }
        }
        
        // Add a small delay to make this method properly async
        await Task.Delay(1);
    }
}