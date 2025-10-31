using Samples.Common;
using Samples.Resources;
using Azure.AI.Agents.Persistent;

namespace Samples;

/// <summary>
/// Azure AI Foundry Sample Suite
/// Interactive sample application showcasing various Azure AI Foundry capabilities.
/// </summary>
class Program
{
    private static ConfigurationHelper.AIFoundryConfig? _config;
    private static PersistentAgentsClient? _agentClient;

    static async Task Main(string[] args)
    {
        Console.WriteLine("Azure AI Foundry Sample Suite");
        Console.WriteLine("===============================");
        Console.WriteLine();

        // Load and validate configuration
        if (!await InitializeAsync())
        {
            Console.WriteLine("Initialization failed. Please check your configuration and try again.");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
            return;
        }

        // Run the main sample loop
        await RunSampleLoopAsync();

        Console.WriteLine("Thank you for exploring Azure AI Foundry!");
    }

    /// <summary>
    /// Initializes the application by loading configuration and creating the agent client.
    /// </summary>
    private static async Task<bool> InitializeAsync()
    {
        try
        {
            Console.WriteLine("Loading configuration...");
            _config = ConfigurationHelper.LoadConfiguration();

            if (!ConfigurationHelper.ValidateConfiguration(_config))
            {
                return false;
            }

            ConfigurationHelper.DisplayConfiguration(_config);

            Console.WriteLine("Creating AI Foundry client...");
            _agentClient = AgentClientFactory.CreateClient(_config);

            Console.WriteLine("Validating connection...");
            if (!await AgentClientFactory.ValidateConnectionAsync(_agentClient))
            {
                Console.WriteLine("Failed to connect to Azure AI Foundry. Please check your configuration.");
                return false;
            }

            Console.WriteLine("Successfully connected to Azure AI Foundry!");
            Console.WriteLine();
            return true;
        }
        catch (Azure.RequestFailedException azEx)
        {
            Console.WriteLine("\n═══════════════════════════════════════════════════════════");
            Console.WriteLine("Azure Connection Error");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine($"Status Code: {azEx.Status}");
            Console.WriteLine($"Error Code: {azEx.ErrorCode}");
            Console.WriteLine($"Message: {azEx.Message}");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            return false;
        }
        catch (FileNotFoundException fnfEx)
        {
            Console.WriteLine("\n═══════════════════════════════════════════════════════════");
            Console.WriteLine("Configuration File Not Found");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine($"Message: {fnfEx.Message}");
            Console.WriteLine("Please ensure appsettings.development.json exists.");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine("\n═══════════════════════════════════════════════════════════");
            Console.WriteLine("Initialization Error");
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine($"Error Type: {ex.GetType().Name}");
            Console.WriteLine($"Message: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            return false;
        }
    }

    /// <summary>
    /// Runs the main sample selection and execution loop.
    /// </summary>
    private static async Task RunSampleLoopAsync()
    {
        var samples = GetAvailableSamples();

        while (true)
        {
            DisplayMainMenu(samples);
            
            var choice = GetUserChoice(samples.Count);
            
            if (choice == samples.Count + 1) // Exit option
            {
                Console.WriteLine("Goodbye!");
                break;
            }

            if (choice >= 1 && choice <= samples.Count)
            {
                var selectedSample = samples[choice - 1];
                Console.Clear();
                
                try
                {
                    await selectedSample.RunAsync();
                }
                catch (Azure.RequestFailedException azEx)
                {
                    Console.WriteLine("\n═══════════════════════════════════════════════════════════");
                    Console.WriteLine("Azure Request Failed");
                    Console.WriteLine("═══════════════════════════════════════════════════════════");
                    Console.WriteLine($"Status Code: {azEx.Status}");
                    Console.WriteLine($"Error Code: {azEx.ErrorCode}");
                    Console.WriteLine($"Message: {azEx.Message}");
                    Console.WriteLine("═══════════════════════════════════════════════════════════");
                    Console.WriteLine("\nPress any key to return to the main menu...");
                    Console.ReadKey();
                    Console.Clear();
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("\n═══════════════════════════════════════════════════════════");
                    Console.WriteLine("Operation Cancelled");
                    Console.WriteLine("═══════════════════════════════════════════════════════════");
                    Console.WriteLine("The operation was cancelled by the user or timed out.");
                    Console.WriteLine("═══════════════════════════════════════════════════════════");
                    Console.WriteLine("\nPress any key to return to the main menu...");
                    Console.ReadKey();
                    Console.Clear();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("\n═══════════════════════════════════════════════════════════");
                    Console.WriteLine("Sample Execution Error");
                    Console.WriteLine("═══════════════════════════════════════════════════════════");
                    Console.WriteLine($"Error Type: {ex.GetType().Name}");
                    Console.WriteLine($"Message: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                    }
                    Console.WriteLine("═══════════════════════════════════════════════════════════");
                    Console.WriteLine("\nPress any key to return to the main menu...");
                    Console.ReadKey();
                    Console.Clear();
                }
            }
            else
            {
                Console.WriteLine("Invalid choice. Please try again.");
                await Task.Delay(1500);
                Console.Clear();
            }
        }
    }

    /// <summary>
    /// Gets the list of available samples.
    /// </summary>
    private static List<Base> GetAvailableSamples()
    {
        return new List<Base>
        {
            new BasicAgent(_agentClient!, _config!.ModelDeploymentName),
            new MCP(_agentClient!, _config!.ModelDeploymentName),
            new FunctionCalling(_agentClient!, _config!.ModelDeploymentName),
            new Streaming(_agentClient!, _config!.ModelDeploymentName),
            new ConnectedAgent(_agentClient!, _config!.ModelDeploymentName),
            new AzureAISearchAgent(_agentClient!, _config!.ModelDeploymentName, _config!),
            new ErrorHandling(_agentClient!, _config!.ModelDeploymentName),
        };
    }

    /// <summary>
    /// Displays the main menu with available sample options.
    /// </summary>
    private static void DisplayMainMenu(List<Base> samples)
    {
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine("Azure AI Foundry Sample Suite");
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine();
        Console.WriteLine("Select a sample to run:");
        Console.WriteLine();

        for (int i = 0; i < samples.Count; i++)
        {
            var sample = samples[i];
            Console.WriteLine($"  {i + 1}. {sample.Name}");
            Console.WriteLine($"     {sample.Description}");
            Console.WriteLine();
        }

        Console.WriteLine($"  {samples.Count + 1}. Exit");
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════");
    }

    /// <summary>
    /// Gets and validates user input for sample selection.
    /// </summary>
    private static int GetUserChoice(int maxChoice)
    {
        while (true)
        {
            Console.Write($"Enter your choice (1-{maxChoice + 1}): ");
            
            if (int.TryParse(Console.ReadLine(), out int choice))
            {
                return choice;
            }
            
            Console.WriteLine("Please enter a valid number.");
        }
    }
}