using System.Diagnostics;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;


// appsettings.json is gitignored (contains real keys). Environment variables override it,
// so CI/CD and container deployments can inject secrets without touching the file.
var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var endpoint = config["AzureOpenAI:Endpoint"]
    ?? throw new InvalidOperationException("AzureOpenAI:Endpoint is not set in appsettings.json");
var apiKey = config["AzureOpenAI:ApiKey"]
    ?? throw new InvalidOperationException("AzureOpenAI:ApiKey is not set in appsettings.json");
var deploymentName = config["AzureOpenAI:DeploymentName"] ?? "gpt-4o";


IChatClient chatClient = new AzureOpenAIClient(
    new Uri(endpoint),
    new AzureKeyCredential(apiKey))
    .GetChatClient(deploymentName)
    .AsIChatClient()
    .AsBuilder()
    .Build();

Console.WriteLine("Azure OpenAI Chat - Hello World");
Console.WriteLine($"Deployment: {deploymentName}");
Console.WriteLine();
Console.WriteLine("Sending prompt...");
Console.WriteLine();


var response = await chatClient.GetResponseAsync(
    "Hello! Can you tell me one interesting fact about artificial intelligence?");

Console.WriteLine("Response:");
Console.WriteLine(response.Text);
Console.WriteLine();

if (response.Usage is not null)
{
    var inputTokens  = response.Usage.InputTokenCount  ?? 0;
    var outputTokens = response.Usage.OutputTokenCount ?? 0;
    Console.WriteLine($"Token usage — Input: {inputTokens}, Output: {outputTokens}, Total: {inputTokens + outputTokens}");
}

Console.WriteLine("Done");
