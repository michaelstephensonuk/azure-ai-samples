# helloworld.logging — How It Works

## Overview

This sample makes a single chat request to Azure OpenAI and records the full observability signal — a trace span, child dependency span, and token usage metrics — without capturing the actual prompt or response text. It is the baseline for all other samples in this workspace.

## Key components

| Component | Role |
|---|---|
| `AzureOpenAIClient` | Connects directly to Azure OpenAI using an API key |
| `IChatClient` | Provider-neutral abstraction from `Microsoft.Extensions.AI` |
| `UseOpenTelemetry()` | Middleware that auto-instruments each `GetResponseAsync` call |
| `TracerProvider` | OpenTelemetry pipeline routing spans to Console + App Insights |
| `MeterProvider` | OpenTelemetry pipeline routing metrics to Console + App Insights |

## Flow

```mermaid
sequenceDiagram
    participant App as Console App
    participant OTel as OpenTelemetry SDK
    participant AOAI as Azure OpenAI
    participant Console as Terminal
    participant AppIns as Application Insights

    App->>OTel: Build TracerProvider & MeterProvider
    App->>OTel: StartActivity("run-ai-prompt", Server)
    Note over App,OTel: Root span opens — kind=Server so App Insights classifies it as a Request

    App->>AOAI: IChatClient.GetResponseAsync(prompt)
    Note over App,AOAI: UseOpenTelemetry middleware creates child span "chat gpt-4o"<br/>Attributes: model, system, token counts<br/>No prompt/response text (EnableSensitiveData = false)
    AOAI-->>App: ChatResponse (text + usage)

    App->>OTel: SetTag(ai.input_tokens, ai.output_tokens)
    App->>OTel: SetStatus(Ok) + EndActivity
    Note over App,OTel: Root span closes and is handed to the exporter queue

    App->>OTel: ForceFlush (traces + metrics)
    OTel->>Console: Print spans and metrics to terminal
    OTel->>AppIns: Export via Azure Monitor exporter
```

## What you see in App Insights

- **Transaction search → Requests**: one entry named `run-ai-prompt`
- **Dependencies** (nested under that request): `chat gpt-4o` with duration and token count attributes
- **Metrics → customMetrics**: `gen_ai.client.token.usage` (input/output split) and `gen_ai.client.operation.duration`
