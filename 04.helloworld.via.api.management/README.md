# helloworld.via.api.management

A .NET 10 console app demonstrating how to call **Azure OpenAI** via **Azure API Management (APIM)** using the `OpenAIClient` from the base OpenAI SDK, with **OpenTelemetry** observability exported to **Azure Application Insights**.

## What this sample shows

- Using `OpenAIClient` (not `AzureOpenAIClient`) to call an APIM gateway that proxies Azure OpenAI
- Injecting the **APIM subscription key** via a custom `HttpClient` header (`Ocp-Apim-Subscription-Key`) rather than through the SDK's built-in auth
- Why `AzureOpenAIClient` cannot be used here: it hardcodes `/openai/deployments/{name}/chat/completions` into every request URL, which won't match a typical APIM operation path
- APIM is responsible for rewriting the URL, adding the Azure OpenAI `api-key`, and forwarding to the backend
- OpenTelemetry tracing and metrics exported to Application Insights

## How authentication works

| Hop | Auth mechanism |
|---|---|
| Code → APIM | `Ocp-Apim-Subscription-Key` header (added to `HttpClient.DefaultRequestHeaders`) |
| APIM → Azure OpenAI | `api-key` header injected by the APIM inbound policy (key stored as a Named Value) |

## Telemetry produced

| Signal | Name | What it captures |
|---|---|---|
| Trace (request) | `run-ai-prompt-via-apim` | Root span for the console operation |
| Trace (dependency) | `chat gpt-4o` | The call through APIM to Azure OpenAI |
| Metric | `gen_ai.client.token.usage` | Input and output token counts |
| Metric | `gen_ai.client.operation.duration` | End-to-end latency including APIM processing |

Activity source and meter are both under `Experimental.Microsoft.Extensions.AI`.

## Key NuGet packages

| Package | Purpose |
|---|---|
| `Azure.AI.OpenAI` 2.9.0-beta.1 | Provides the base `OpenAIClient` |
| `Microsoft.Extensions.AI` 10.7.0 | `IChatClient` abstraction + OpenTelemetry middleware |
| `Microsoft.Extensions.AI.OpenAI` 10.7.0 | Bridge: `AsIChatClient()` extension |
| `Azure.Monitor.OpenTelemetry.Exporter` 1.8.2 | Exports OTel traces and metrics to App Insights |
| `OpenTelemetry.Exporter.Console` 1.16.0 | Prints spans and metrics to the terminal during local dev |

## Configuration

Copy `appsettings.template.json` to `appsettings.json` and fill in your values:

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://<your-apim-gateway>.azure-api.net/<your-api-path>/",
    "ApiKey": "<your-apim-subscription-key>",
    "DeploymentName": "gpt-4o"
  },
  "ApplicationInsights": {
    "ConnectionString": "<your-app-insights-connection-string>"
  }
}
```

> `Endpoint` is your **APIM gateway URL** (not the Azure OpenAI resource URL). `ApiKey` is your **APIM subscription key** — the real Azure OpenAI key is held in APIM as a Named Value.

`appsettings.json` is gitignored. Secrets can also be supplied via environment variables:

```
AzureOpenAI__ApiKey=...
ApplicationInsights__ConnectionString=...
```

## Running locally

Press **F5** in VS Code (select the `helloworld.via.api.management` configuration) or:

```bash
dotnet run
```

## APIM policy required

The APIM API needs an inbound policy to rewrite the URL and inject the Azure OpenAI key. See `ReadMe.APIM.md` in the workspace root for a full policy example.

## Querying telemetry in App Insights

**Transaction search** → filter by Request → `run-ai-prompt-via-apim`.

**Token usage (KQL):**
```kql
customMetrics
| where name == "gen_ai.client.token.usage"
| extend tokenType = tostring(customDimensions["gen_ai.token.type"])
| summarize totalTokens = sum(value) by tokenType, bin(timestamp, 1h)
| order by timestamp desc
```

## Notes

- `OpenAIClient` is used instead of `AzureOpenAIClient` because `AzureOpenAIClient` appends `/openai/deployments/{name}/chat/completions` to the endpoint URL, which will not match a standard APIM operation configured at `/chat/completions`.
- The `ApiKeyCredential("placeholder")` passed to `OpenAIClient` is intentionally a dummy — the real auth is the `Ocp-Apim-Subscription-Key` header added directly to the underlying `HttpClient`.
- `SamplingRatio = 1.0f` ensures all traces ship to App Insights.
- `ForceFlush()` is called before exit to drain the Azure Monitor exporter's background queue.
- The root `run-ai-prompt-via-apim` span uses `ActivityKind.Server` so App Insights classifies it as a request.
