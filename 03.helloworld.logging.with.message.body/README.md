# helloworld.logging.with.message.body

A minimal .NET 10 console app demonstrating how to call **Azure OpenAI** via the **Microsoft.Extensions.AI** abstraction layer, with full **OpenTelemetry** observability exported to **Azure Application Insights** — including the **full prompt and response text** in every trace span.

## What this sample shows

- Connecting to Azure OpenAI using `AzureOpenAIClient` wrapped as `IChatClient` via `Microsoft.Extensions.AI`
- Sending a chat prompt and printing the response plus token usage
- Enabling **sensitive data logging** (`EnableSensitiveData = true`) so that the prompt text and response text are captured as span attributes in every trace
- Instrumenting the AI call with OpenTelemetry using the built-in `UseOpenTelemetry()` middleware
- Exporting traces and metrics to Application Insights using the Azure Monitor OpenTelemetry exporter
- Structuring a root "request" span so the AI call appears as a nested dependency in App Insights Transaction search

> **How this differs from `helloworld.logging`:** the only difference is `.UseOpenTelemetry(configure: o => o.EnableSensitiveData = true)`. Without that flag the span attributes record only metadata (model, token counts, latency). With it, `gen_ai.prompt` and `gen_ai.completion` attributes are added to the span containing the actual message text. Use with caution in production — prompt/response content may include PII.

## Telemetry produced

| Signal | Name | What it captures |
|---|---|---|
| Trace (request) | `run-ai-prompt-inc-message-body` | Root span for the console operation |
| Trace (dependency) | `chat gpt-4o` | The Azure OpenAI call with `gen_ai.prompt` and `gen_ai.completion` attributes |
| Metric | `gen_ai.client.token.usage` | Input and output token counts, split by `gen_ai.token.type` |
| Metric | `gen_ai.client.operation.duration` | Latency of each AI call in seconds |

Activity source and meter are both under `Experimental.Microsoft.Extensions.AI` (the prefix used by ME.AI 10.x while the GenAI semantic conventions are still stabilising).

## Key NuGet packages

| Package | Purpose |
|---|---|
| `Azure.AI.OpenAI` 2.9.0-beta.1 | Azure OpenAI client |
| `Microsoft.Extensions.AI` 10.7.0 | `IChatClient` abstraction + OpenTelemetry middleware |
| `Microsoft.Extensions.AI.OpenAI` 10.7.0 | Bridge: `AsIChatClient()` extension |
| `Azure.Monitor.OpenTelemetry.Exporter` 1.8.2 | Exports OTel traces and metrics to App Insights |
| `OpenTelemetry.Exporter.Console` 1.16.0 | Prints spans and metrics to the terminal during local dev |

## Configuration

Copy `appsettings.template.json` to `appsettings.json` and fill in your values:

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://<your-resource>.openai.azure.com/",
    "ApiKey": "<your-azure-openai-key>",
    "DeploymentName": "gpt-4o"
  },
  "ApplicationInsights": {
    "ConnectionString": "<your-app-insights-connection-string>"
  }
}
```

`appsettings.json` is gitignored. Secrets can also be supplied via environment variables using double-underscore notation:

```
AzureOpenAI__ApiKey=...
ApplicationInsights__ConnectionString=...
```

## Running locally

Press **F5** in VS Code (select the `helloworld.logging.with.message.body` configuration) or:

```bash
dotnet run
```

## Querying telemetry in App Insights

**Transaction search** → filter by Request → `run-ai-prompt-inc-message-body` (with `chat gpt-4o` nested as a dependency).

**Token usage (KQL):**
```kql
customMetrics
| where name == "gen_ai.client.token.usage"
| extend tokenType = tostring(customDimensions["gen_ai.token.type"])
| summarize totalTokens = sum(value) by tokenType, bin(timestamp, 1h)
| order by timestamp desc
```

## Notes

- `SamplingRatio = 1.0f` is set on the Azure Monitor exporter so every run ships to App Insights. The default adaptive sampling drops ~2/3 of traces at low volume, which makes local testing unreliable.
- `tracerProvider.ForceFlush()` and `meterProvider.ForceFlush()` are called explicitly before exit because the Azure Monitor exporter uses a background queue — without these a short-lived console app exits before telemetry ships.
- The root `run-ai-prompt` span uses `ActivityKind.Server` so App Insights classifies it as a request rather than an InProc dependency.
