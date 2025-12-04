using Azure.AI.Agents.Persistent;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Quality;
using OpenTelemetry;
using Samples.Common;
using System.Text.Json;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
using ChatRole = Microsoft.Extensions.AI.ChatRole;

#pragma warning disable AIEVAL001 // Experimental API

namespace Samples.Resources;

public class AgentEvaluation : Base
{
    private readonly ConfigurationHelper.AIFoundryConfig _config;
    private readonly List<EvaluationResult> _allResults = new();

    public AgentEvaluation(PersistentAgentsClient agentClient, string modelDeploymentName, ConfigurationHelper.AIFoundryConfig config)
        : base(agentClient, modelDeploymentName)
    {
        _config = config;
    }

    public override string Name => "Agent Evaluation";
    public override string Description => "Evaluate agent responses using Microsoft.Extensions.AI.Evaluation SDK.";

    public override async Task RunAsync()
    {
        // Disable tracing for this resource alone
        using var scope = SuppressInstrumentationScope.Begin();

        DisplayHeader();
        _allResults.Clear();

        try
        {
            var chatConfiguration = CreateChatConfiguration();

            await RunBasicEvaluationAsync(chatConfiguration);
            await RunToolCallEvaluationAsync(chatConfiguration);
            await RunRetrievalEvaluationAsync(chatConfiguration);

            DisplayEvaluationSummary(_allResults);
            Console.WriteLine("\nAgent Evaluation completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during agent evaluation: {ex.Message}");
        }

        DisplayFooter();
    }

    private ChatConfiguration CreateChatConfiguration()
    {
        Console.WriteLine("Setting up evaluation chat client...");
        var credential = new ClientSecretCredential(_config.TenantId, _config.ClientId, _config.ClientSecret);
        var baseEndpointUri = new Uri(_config.ProjectEndpoint);
        var azureOpenAIEndpoint = new Uri($"{baseEndpointUri.Scheme}://{baseEndpointUri.Host}");
        
        var azureOpenAIClient = new AzureOpenAIClient(azureOpenAIEndpoint, credential);
        var chatClient = azureOpenAIClient.GetChatClient(ModelDeploymentName);
        
        return new ChatConfiguration(chatClient.AsIChatClient());
    }

    private async Task RunBasicEvaluationAsync(ChatConfiguration config)
    {
        Console.WriteLine("\n=== PART 1: Basic Q&A Quality Evaluation ===");
        
        PersistentAgent? agent = null;
        PersistentAgentThread? thread = null;

        try
        {
            agent = await AgentClient.Administration.CreateAgentAsync(
                model: ModelDeploymentName,
                name: "Foundry-Evaluation-Basic-Agent",
                instructions: "You are a helpful AI assistant. Base answers on factual knowledge.");

            thread = await AgentClient.Threads.CreateThreadAsync();

            var query = "What is the capital of France?";
            var context = "France is a country in Western Europe. Paris is the capital and largest city of France.";
            
            Console.WriteLine($"Query: {query}");
            var response = await GetAgentResponseAsync(thread, agent, query);
            Console.WriteLine($"Response: {response}");

            var result = await EvaluateQualityAsync(config, query, response, context);
            _allResults.Add(result);
            DisplayEvaluationResult(result);
        }
        finally
        {
            await CleanupAsync(agent, thread);
        }
    }

    private async Task RunToolCallEvaluationAsync(ChatConfiguration config)
    {
        Console.WriteLine("\n=== PART 2: Tool Call Accuracy Evaluation ===");

        PersistentAgent? agent = null;
        PersistentAgentThread? thread = null;
        var weatherTool = CreateWeatherTool();
        var calculatorTool = CreateCalculatorTool();

        try
        {
            agent = await AgentClient.Administration.CreateAgentAsync(
                model: ModelDeploymentName,
                name: "Foundry-Evaluation-Tool-Agent",
                instructions: "You are a helpful assistant with access to weather and calculator tools.",
                tools: [weatherTool, calculatorTool]);

            thread = await AgentClient.Threads.CreateThreadAsync();

            var query = "What's the weather like in Seattle, WA?";
            Console.WriteLine($"Query: {query}");

            var (response, toolCalls) = await GetAgentResponseWithToolCallsAsync(thread, agent, query);
            Console.WriteLine($"Response: {response}");

            var result = await EvaluateToolCallAccuracyAsync(config, query, response, toolCalls, 
                [weatherTool, calculatorTool], "Use the weather tool to get weather information.");
            
            _allResults.Add(result);
            DisplayEvaluationResult(result);
        }
        finally
        {
            await CleanupAsync(agent, thread);
        }
    }

    private async Task RunRetrievalEvaluationAsync(ChatConfiguration config)
    {
        Console.WriteLine("\n=== PART 3: Retrieval Evaluation ===");

        PersistentAgent? agent = null;
        PersistentAgentThread? thread = null;
        PersistentAgentsVectorStore? vectorStore = null;
        PersistentAgentFileInfo? uploadedFile = null;
        string filePath = "evaluation_knowledge_base.txt";

        try
        {
            await CreateKnowledgeBaseFileAsync(filePath);
            uploadedFile = await AgentClient.Files.UploadFileAsync(filePath, PersistentAgentFilePurpose.Agents);
            vectorStore = await AgentClient.VectorStores.CreateVectorStoreAsync([uploadedFile.Id], "eval_store");
            
            // Wait for processing
            await Task.Delay(TimeSpan.FromSeconds(3));

            var fileSearchResource = new FileSearchToolResource();
            fileSearchResource.VectorStoreIds.Add(vectorStore.Id);

            agent = await AgentClient.Administration.CreateAgentAsync(
                model: ModelDeploymentName,
                name: "Foundry-Evaluation-FileSearch-Agent",
                instructions: "You are a helpful product information assistant. Use file search.",
                tools: [new FileSearchToolDefinition()],
                toolResources: new ToolResources { FileSearch = fileSearchResource });

            thread = await AgentClient.Threads.CreateThreadAsync();

            var query = "What is the price of Azure AI Foundry?";
            Console.WriteLine($"Query: {query}");

            var (response, docs) = await GetAgentResponseWithRetrievalAsync(thread, agent, query);
            Console.WriteLine($"Response: {response}");
            Console.WriteLine($"Retrieved {docs.Count} docs");

            var result = await EvaluateRetrievalAsync(config, query, response, docs);
            _allResults.Add(result);
            DisplayEvaluationResult(result);
        }
        finally
        {
            if (vectorStore != null) await AgentClient.VectorStores.DeleteVectorStoreAsync(vectorStore.Id);
            if (uploadedFile != null) await AgentClient.Files.DeleteFileAsync(uploadedFile.Id);
            if (File.Exists(filePath)) File.Delete(filePath);
            await CleanupAsync(agent, thread);
        }
    }

    private async Task CreateKnowledgeBaseFileAsync(string filePath)
    {
        var content = @"Product: Azure AI Foundry
                        Price: Starting at $0.002 per 1K tokens
                        Features: Agent creation, model deployment

                        Product: Copilot Studio
                        Price: $200 per user/month";
        await File.WriteAllTextAsync(filePath, content);
    }

    #region Tool Definitions

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
                    location = new { type = "string", description = "The city and state/country" },
                    unit = new { type = "string", @enum = new[] { "celsius", "fahrenheit" } }
                },
                required = new[] { "location" }
            })));
    }

    private static FunctionToolDefinition CreateCalculatorTool()
    {
        return new FunctionToolDefinition(
            name: "calculate",
            description: "Perform mathematical calculations",
            parameters: BinaryData.FromString(JsonSerializer.Serialize(new
            {
                type = "object",
                properties = new
                {
                    expression = new { type = "string", description = "Mathematical expression" },
                    operation = new { type = "string", @enum = new[] { "basic", "percentage", "tip" } }
                },
                required = new[] { "expression" }
            })));
    }

    #endregion

    #region Agent Response Methods

    private async Task<string> GetAgentResponseAsync(PersistentAgentThread thread, PersistentAgent agent, string query)
    {
        await AgentClient.Messages.CreateMessageAsync(thread.Id, MessageRole.User, query);
        ThreadRun run = (await AgentClient.Runs.CreateRunAsync(thread.Id, agent.Id)).Value;
        run = await WaitForRunCompletionAsync(thread, run);

        if (run.Status != RunStatus.Completed) return $"[Agent failed: {run.Status}]";

        await foreach (var message in AgentClient.Messages.GetMessagesAsync(thread.Id, order: ListSortOrder.Descending))
        {
            if (message.Role == MessageRole.Agent)
            {
                foreach (var content in message.ContentItems)
                {
                    if (content is MessageTextContent text) return text.Text;
                }
            }
        }
        return "[No response]";
    }

    private async Task<(string Response, List<ToolCallInfo> ToolCalls)> GetAgentResponseWithToolCallsAsync(
        PersistentAgentThread thread, PersistentAgent agent, string query)
    {
        await AgentClient.Messages.CreateMessageAsync(thread.Id, MessageRole.User, query);
        ThreadRun run = (await AgentClient.Runs.CreateRunAsync(thread.Id, agent.Id)).Value;
        var toolCalls = new List<ToolCallInfo>();

        while (true)
        {
            await Task.Delay(500);
            run = (await AgentClient.Runs.GetRunAsync(thread.Id, run.Id)).Value;

            if (run.Status == RunStatus.RequiresAction && run.RequiredAction is SubmitToolOutputsAction submitAction)
            {
                var toolOutputs = new List<ToolOutput>();
                foreach (var toolCall in submitAction.ToolCalls)
                {
                    if (toolCall is RequiredFunctionToolCall funcCall)
                    {
                        toolCalls.Add(new ToolCallInfo { Name = funcCall.Name, Arguments = funcCall.Arguments });
                        var output = funcCall.Name switch
                        {
                            "get_weather" => JsonSerializer.Serialize(new { temperature = "65F" }),
                            "calculate" => JsonSerializer.Serialize(new { result = "13.125" }),
                            _ => "{}"
                        };
                        toolOutputs.Add(new ToolOutput(funcCall.Id, output));
                    }
                }
                run = (await AgentClient.Runs.SubmitToolOutputsToRunAsync(thread.Id, run.Id, toolOutputs)).Value;
            }
            else if (run.Status == RunStatus.Completed || run.Status == RunStatus.Failed || run.Status == RunStatus.Cancelled)
            {
                break;
            }
        }

        string response = "[No response]";
        if (run.Status == RunStatus.Completed)
        {
            await foreach (var message in AgentClient.Messages.GetMessagesAsync(thread.Id, order: ListSortOrder.Descending))
            {
                if (message.Role == MessageRole.Agent)
                {
                    foreach (var content in message.ContentItems)
                    {
                        if (content is MessageTextContent text)
                        {
                            response = text.Text;
                            break;
                        }
                    }
                    break;
                }
            }
        }
        return (response, toolCalls);
    }

    private async Task<(string Response, List<string> RetrievedDocuments)> GetAgentResponseWithRetrievalAsync(
        PersistentAgentThread thread, PersistentAgent agent, string query)
    {
        await AgentClient.Messages.CreateMessageAsync(thread.Id, MessageRole.User, query);
        ThreadRun run = (await AgentClient.Runs.CreateRunAsync(thread.Id, agent.Id)).Value;
        run = await WaitForRunCompletionAsync(thread, run);

        var docs = new List<string>();
        string response = "[No response]";

        if (run.Status == RunStatus.Completed)
        {
            await foreach (var message in AgentClient.Messages.GetMessagesAsync(thread.Id, order: ListSortOrder.Descending))
            {
                if (message.Role == MessageRole.Agent)
                {
                    foreach (var content in message.ContentItems)
                    {
                        if (content is MessageTextContent text)
                        {
                            response = text.Text;
                            foreach (var annotation in text.Annotations)
                            {
                                if (annotation is MessageTextFileCitationAnnotation citation)
                                {
                                    string? quote = citation.GetType().GetProperty("Quote")?.GetValue(citation) as string;
                                    docs.Add(!string.IsNullOrEmpty(quote) ? quote : $"[Citation: {citation.FileId}]");
                                }
                            }
                            break;
                        }
                    }
                    break;
                }
            }
        }
        if (docs.Count == 0 && response != "[No response]") docs.Add(response);
        return (response, docs);
    }

    #endregion

    #region Evaluation Methods

    private async Task<EvaluationResult> EvaluateQualityAsync(ChatConfiguration config, string query, string response, string context)
    {
        var result = new EvaluationResult { Query = query, Response = response, Context = context, Scores = new() };
        var messages = new List<ChatMessage> { new(ChatRole.User, query) };
        var modelResponse = new ChatResponse([new ChatMessage(ChatRole.Assistant, response)]);

        try
        {
            var evalResult = await new RelevanceEvaluator().EvaluateAsync(messages, modelResponse, config);
            AddScores(result, evalResult);
        }
        catch (Exception ex) { AddErrorScore(result, "Relevance", ex.Message); }

        try
        {
            var evalResult = await new GroundednessEvaluator().EvaluateAsync(messages, modelResponse, config, [new GroundednessEvaluatorContext(context)]);
            AddScores(result, evalResult);
        }
        catch (Exception ex) { AddErrorScore(result, "Groundedness", ex.Message); }

        return result;
    }

    private async Task<EvaluationResult> EvaluateToolCallAccuracyAsync(ChatConfiguration config, string query, string response, 
        List<ToolCallInfo> toolCalls, FunctionToolDefinition[] tools, string taskDesc)
    {
        var result = new EvaluationResult { Query = query, Response = response, Context = taskDesc, Scores = new() };
        var messages = new List<ChatMessage> { new(ChatRole.User, query) };
        
        var toolCallMessage = new ChatMessage(ChatRole.Assistant, "");
        int i = 0;
        foreach (var tc in toolCalls)
        {
            var args = JsonSerializer.Deserialize<Dictionary<string, object?>>(tc.Arguments) ?? new();
            toolCallMessage.Contents.Add(new FunctionCallContent($"call_{i++}_{tc.Name}", tc.Name, args));
        }
        
        var modelResponse = new ChatResponse([toolCallMessage]);
        var aiFunctions = tools.Select(t => AIFunctionFactory.Create((string _) => "mock", t.Name, t.Description)).ToArray();

        try
        {
            var evalResult = await new IntentResolutionEvaluator().EvaluateAsync(messages, modelResponse, config);
            AddScores(result, evalResult);
        }
        catch (Exception ex) { AddErrorScore(result, "IntentResolution", ex.Message); }

        try
        {
            var evalResult = await new TaskAdherenceEvaluator().EvaluateAsync(messages, modelResponse, config, [new TaskAdherenceEvaluatorContext(aiFunctions)]);
            AddScores(result, evalResult);
        }
        catch (Exception ex) { AddErrorScore(result, "TaskAdherence", ex.Message); }

        try
        {
            var evalResult = await new ToolCallAccuracyEvaluator().EvaluateAsync(messages, modelResponse, config, [new ToolCallAccuracyEvaluatorContext(aiFunctions)]);
            AddScores(result, evalResult);
        }
        catch (Exception ex) { AddErrorScore(result, "ToolCallAccuracy", ex.Message); }

        return result;
    }

    private async Task<EvaluationResult> EvaluateRetrievalAsync(ChatConfiguration config, string query, string response, List<string> docs)
    {
        var result = new EvaluationResult { Query = query, Response = response, Context = string.Join("\n", docs), Scores = new() };
        var messages = new List<ChatMessage> { new(ChatRole.User, query) };
        var modelResponse = new ChatResponse([new ChatMessage(ChatRole.Assistant, response)]);

        try
        {
            var evalResult = await new RetrievalEvaluator().EvaluateAsync(messages, modelResponse, config, [new RetrievalEvaluatorContext(docs)]);
            AddScores(result, evalResult);
        }
        catch (Exception ex) { AddErrorScore(result, "Retrieval", ex.Message); }

        return result;
    }

    private void AddScores(EvaluationResult result, Microsoft.Extensions.AI.Evaluation.EvaluationResult evalResult)
    {
        foreach (var metric in evalResult.Metrics)
        {
            double? scoreValue = null;
            string reason = "";
            string debugInfo = "";

            if (metric.Value is NumericMetric numeric)
            {
                scoreValue = numeric.Value;
                string rating = numeric.Interpretation?.Rating.ToString() ?? "N/A";
                reason = rating;
                debugInfo = $"NumericMetric(Value={numeric.Value}, Rating={rating})";
            }
            else if (metric.Value is BooleanMetric boolean)
            {
                scoreValue = (boolean.Value == true) ? 1.0 : 0.0;
                reason = "Boolean result";
                debugInfo = $"BooleanMetric(Value={boolean.Value})";
            }
            else
            {
                debugInfo = $"Unknown Type: {metric.Value?.GetType().Name}";
                reason = metric.Value?.ToString() ?? "Unknown";
            }

            Console.WriteLine($"[Evaluator] {metric.Key}: {debugInfo}");

            result.Scores[metric.Key] = new EvaluationScore 
            { 
                Name = metric.Key, 
                Score = scoreValue, 
                Reason = reason 
            };
        }
    }

    private void AddErrorScore(EvaluationResult result, string name, string error)
    {
        result.Scores[name] = new EvaluationScore { Name = name, Score = null, Reason = $"Error: {error}" };
    }

    #endregion

    #region Display Methods

    private void DisplayEvaluationResult(EvaluationResult result)
    {
        Console.WriteLine("Scores:");
        foreach (var score in result.Scores)
        {
            Console.WriteLine($"  {score.Key}: {(score.Value.Score.HasValue ? $"{score.Value.Score:F2}" : "N/A")}");
            if (!string.IsNullOrEmpty(score.Value.Reason))
            {
                Console.WriteLine($"    Reason: {score.Value.Reason}");
            }
        }
    }

    private void DisplayEvaluationSummary(List<EvaluationResult> results)
    {
        Console.WriteLine("\n=== SUMMARY ===");
        var metrics = new Dictionary<string, List<double>>();
        foreach (var r in results)
        {
            foreach (var s in r.Scores)
            {
                if (s.Value.Score.HasValue)
                {
                    if (!metrics.ContainsKey(s.Key)) metrics[s.Key] = new();
                    metrics[s.Key].Add(s.Value.Score.Value);
                }
            }
        }

        foreach (var m in metrics.OrderBy(k => k.Key))
        {
            Console.WriteLine($"  {m.Key,-20}: {m.Value.Average():F2} (n={m.Value.Count})");
        }
    }

    #endregion

    private class ToolCallInfo { public required string Name { get; set; } public required string Arguments { get; set; } }
    private class EvaluationResult { public required string Query { get; set; } public required string Response { get; set; } public required string Context { get; set; } public required Dictionary<string, EvaluationScore> Scores { get; set; } }
    private class EvaluationScore { public required string Name { get; set; } public double? Score { get; set; } public required string Reason { get; set; } }
}
