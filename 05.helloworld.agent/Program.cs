using System.ComponentModel;
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

var sourceName = "helloworld-agent";
var appSource = new ActivitySource(sourceName);

var resource = ResourceBuilder.CreateDefault()
    .AddService(sourceName);

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(resource)
    .SetSampler(new AlwaysOnSampler())
    .AddSource(sourceName)
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
    .UseFunctionInvocation()  // middleware that handles the tool-call loop automatically
    .UseOpenTelemetry(configure: o => o.EnableSensitiveData = true)
    .Build();


// --- Agent tools ---

AIFunction getDateTimeTool = AIFunctionFactory.Create(
    () => DateTime.UtcNow.ToString("R"),
    "GetCurrentDateTime",
    "Returns the current UTC date and time.");

AIFunction getWeatherTool = AIFunctionFactory.Create(
    ([Description("The city to get the weather for")] string city) =>
        city.Trim().ToLowerInvariant() switch
        {
            "london"   => "Overcast, 14°C, 80% chance of rain.",
            "new york" => "Partly cloudy, 22°C, mild breeze.",
            "tokyo"    => "Clear skies, 28°C, humid.",
            "sydney"   => "Sunny, 19°C, perfect beach weather.",
            "paris"    => "Light showers, 16°C.",
            _          => $"No weather data available for '{city}'."
        },
    "GetWeather",
    "Returns current weather conditions for a given city.");

AIFunction calculateTool = AIFunctionFactory.Create(
    ([Description("Left operand")] double a,
     [Description("Operator to apply: +, -, *, or /")] string op,
     [Description("Right operand")] double b) =>
        op switch
        {
            "+" => (a + b).ToString("G"),
            "-" => (a - b).ToString("G"),
            "*" => (a * b).ToString("G"),
            "/" => b == 0 ? "Error: division by zero" : (a / b).ToString("G"),
            _   => $"Unknown operator '{op}'. Use +, -, *, or /."
        },
    "Calculate",
    "Performs a basic arithmetic operation (+, -, *, /) on two numbers and returns the result.");

// ChatOptions wires the tools into every call; UseFunctionInvocation() handles the loop.
var options = new ChatOptions
{
    Tools = new List<AITool> { getDateTimeTool, getWeatherTool, calculateTool },
    ToolMode = ChatToolMode.Auto,
};

Console.WriteLine("Azure OpenAI Agent - Hello World Agent Sample");
Console.WriteLine($"Deployment: {deploymentName}");
Console.WriteLine($"Tools     : GetCurrentDateTime, GetWeather, Calculate");
Console.WriteLine();

const string systemPrompt =
    "You are a helpful assistant with access to tools for getting the current date/time, " +
    "checking weather conditions, and performing calculations. " +
    "Use your tools whenever they would help you give a more accurate answer.";

const string userPrompt =
    "What is the weather like in London and Tokyo right now? " +
    "If it is currently past noon UTC, also tell me the temperature difference between the two cities in Fahrenheit.";

Console.WriteLine($"User: {userPrompt}");
Console.WriteLine();

var messages = new List<ChatMessage>
{
    new(ChatRole.System, systemPrompt),
    new(ChatRole.User, userPrompt),
};

// Scoped using block so the span is CLOSED before ForceFlush is called.
// App Insights requires Server-kind spans to be complete before they export as requests.
using (var operation = appSource.StartActivity("run-ai-agent", ActivityKind.Server))
{
    // Custom dependency span wrapping the full agent invocation (including tool-call loop).
    // Setting gen_ai tags directly here makes this show up in App Insights Agent Preview
    // without needing to propagate tags to every child chat span.
    ChatResponse response;
    using (var agentSpan = appSource.StartActivity("Invoke Hello World Agent", ActivityKind.Client))
    {
        agentSpan?.SetTag("gen_ai.operation.name", "invoke_agent");
        agentSpan?.SetTag("gen_ai.agent.name", "helloworld-agent");
        agentSpan?.SetTag("gen_ai.system", "openai");
        agentSpan?.SetTag("gen_ai.request.model", deploymentName);

        response = await chatClient.GetResponseAsync(messages, options);

        Console.WriteLine("Agent:");
        Console.WriteLine(response.Text);
        Console.WriteLine();

        if (response.Usage is not null)
        {
            var inputTokens  = response.Usage.InputTokenCount  ?? 0;
            var outputTokens = response.Usage.OutputTokenCount ?? 0;
            Console.WriteLine($"Token usage — Input: {inputTokens}, Output: {outputTokens}, Total: {inputTokens + outputTokens}");
            
            //Set token use on the custom agent dependency
            agentSpan?.SetTag("ai.input_tokens",  inputTokens);
            agentSpan?.SetTag("ai.output_tokens", outputTokens);

            //Set tokens on the parent request
            operation?.SetTag("ai.input_tokens",  inputTokens);
            operation?.SetTag("ai.output_tokens", outputTokens);
        }

        
        agentSpan?.SetStatus(ActivityStatusCode.Ok);
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
