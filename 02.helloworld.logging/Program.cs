using System.Diagnostics;
using Azure;
using Azure.AI.OpenAI;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

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
var appInsightsConnectionString = config["ApplicationInsights:ConnectionString"]
    ?? throw new InvalidOperationException("ApplicationInsights:ConnectionString is not set in appsettings.json");

var appSource = new ActivitySource("helloworld-logging");

var resource = ResourceBuilder.CreateDefault()
    .AddService("helloworld-logging");

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(resource)
    .SetSampler(new AlwaysOnSampler()) // send 100% of traces to App Insights
    .AddSource("helloworld-logging")
    .AddSource("Microsoft.Extensions.AI")
    .AddSource("Experimental.Microsoft.Extensions.AI")  // ME.AI 10.7.0 uses Experimental. prefix for traces too
    .AddConsoleExporter()
    .AddAzureMonitorTraceExporter(o =>
    {
        o.ConnectionString = appInsightsConnectionString;
        o.SamplingRatio = 1.0f;  // send 100% — default adaptive sampling drops ~2/3 at low volume
    })
    .Build();

using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .SetResourceBuilder(resource)
    .AddMeter("Experimental.Microsoft.Extensions.AI")
    .AddConsoleExporter()
    .AddAzureMonitorMetricExporter(o => o.ConnectionString = appInsightsConnectionString)
    .Build();

IChatClient chatClient = new AzureOpenAIClient(
    new Uri(endpoint),
    new AzureKeyCredential(apiKey))
    .GetChatClient(deploymentName)
    .AsIChatClient()
    .AsBuilder()
    .UseOpenTelemetry()
    .Build();

Console.WriteLine("Azure OpenAI Chat - Hello World with Application Insights");

Console.WriteLine($"Deployment: {deploymentName}");
Console.WriteLine();
Console.WriteLine("Sending prompt...");
Console.WriteLine();

// Scoped using block so the span is CLOSED before ForceFlush is called.
// App Insights requires Server-kind spans to be complete before they export as requests.
using (var operation = appSource.StartActivity("run-ai-prompt", ActivityKind.Server))
{
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
        operation?.SetTag("ai.input_tokens",  inputTokens);
        operation?.SetTag("ai.output_tokens", outputTokens);
    }

    operation?.SetStatus(ActivityStatusCode.Ok);
} // ← span ends here, handed to the exporter queue

// Now flush — the span is complete so it will be included
Console.WriteLine();
Console.WriteLine("Flushing telemetry...");
tracerProvider.ForceFlush();
meterProvider.ForceFlush();
Thread.Sleep(5000); // give the exporter a moment to send the telemetry before the app exits
Console.WriteLine("Done. Traces and token-usage metrics sent to Application Insights.");
