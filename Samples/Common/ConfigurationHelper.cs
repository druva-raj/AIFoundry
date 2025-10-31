using Microsoft.Extensions.Configuration;

namespace Samples.Common;

/// <summary>
/// Helper class for loading and validating configuration settings.
/// </summary>
public static class ConfigurationHelper
{
    /// <summary>
    /// Configuration settings required for AI Foundry demos.
    /// </summary>
    public class AIFoundryConfig
    {
        public required string ProjectEndpoint { get; set; }
        public required string ModelDeploymentName { get; set; }
        public required string TenantId { get; set; }
        public required string ClientId { get; set; }
        public required string ClientSecret { get; set; }
        public string? AzureAIConnectionId { get; set; }
        public string? AzureAISearchIndexName { get; set; }
    }

    /// <summary>
    /// Loads configuration from appsettings.json and environment variables.
    /// </summary>
    /// <returns>A validated configuration object.</returns>
    /// <exception cref="InvalidOperationException">Thrown when required configuration is missing.</exception>
    public static AIFoundryConfig LoadConfiguration()
    {
        var configRoot = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.development.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        var config = new AIFoundryConfig
        {
            ProjectEndpoint = configRoot["PROJECT_ENDPOINT"] 
                ?? throw new InvalidOperationException("PROJECT_ENDPOINT is not configured"),
            ModelDeploymentName = configRoot["MODEL_DEPLOYMENT_NAME"] 
                ?? throw new InvalidOperationException("MODEL_DEPLOYMENT_NAME is not configured"),
            TenantId = configRoot["TENANT_ID"] 
                ?? throw new InvalidOperationException("TENANT_ID is not configured"),
            ClientId = configRoot["CLIENT_ID"] 
                ?? throw new InvalidOperationException("CLIENT_ID is not configured"),
            ClientSecret = configRoot["CLIENT_SECRET"] 
                ?? throw new InvalidOperationException("CLIENT_SECRET is not configured"),
            AzureAIConnectionId = configRoot["AZURE_AI_CONNECTION_ID"],
            AzureAISearchIndexName = configRoot["AZURE_AI_SEARCH_INDEX_NAME"]
        };

        return config;
    }

    /// <summary>
    /// Displays the current configuration (masking sensitive information).
    /// </summary>
    /// <param name="config">The configuration to display.</param>
    public static void DisplayConfiguration(AIFoundryConfig config)
    {
        Console.WriteLine("Configuration:");
        Console.WriteLine($"  Project Endpoint: {config.ProjectEndpoint}");
        Console.WriteLine($"  Model Deployment: {config.ModelDeploymentName}");
        Console.WriteLine($"  Tenant ID: {config.TenantId}");
        Console.WriteLine($"  Client ID: {config.ClientId}");
        Console.WriteLine($"  Client Secret: {"*".PadLeft(config.ClientSecret.Length, '*')}");
        Console.WriteLine();
    }

    /// <summary>
    /// Validates that all required configuration values are present and non-empty.
    /// </summary>
    /// <param name="config">The configuration to validate.</param>
    /// <returns>True if configuration is valid, false otherwise.</returns>
    public static bool ValidateConfiguration(AIFoundryConfig config)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(config.ProjectEndpoint))
            errors.Add("PROJECT_ENDPOINT is required");

        if (string.IsNullOrWhiteSpace(config.ModelDeploymentName))
            errors.Add("MODEL_DEPLOYMENT_NAME is required");

        if (string.IsNullOrWhiteSpace(config.TenantId))
            errors.Add("TENANT_ID is required");

        if (string.IsNullOrWhiteSpace(config.ClientId))
            errors.Add("CLIENT_ID is required");

        if (string.IsNullOrWhiteSpace(config.ClientSecret))
            errors.Add("CLIENT_SECRET is required");

        if (errors.Count > 0)
        {
            Console.WriteLine("Configuration errors:");
            foreach (var error in errors)
            {
                Console.WriteLine($"  - {error}");
            }
            Console.WriteLine();
            Console.WriteLine("Please set the required environment variables and try again.");
            return false;
        }

        return true;
    }
}