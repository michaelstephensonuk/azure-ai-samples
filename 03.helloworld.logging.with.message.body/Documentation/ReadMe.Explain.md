# helloworld.logging.with.message.body — How It Works

## Overview

Identical to `helloworld.logging` except for one line: `.UseOpenTelemetry(configure: o => o.EnableSensitiveData = true)`. This single flag causes the actual prompt text and response text to be recorded as span attributes on every AI call, making them visible in Application Insights traces.

## Key difference from helloworld.logging

```csharp
// helloworld.logging
.UseOpenTelemetry()

// helloworld.logging.with.message.body
.UseOpenTelemetry(configure: o => o.EnableSensitiveData = true)
```

With `EnableSensitiveData = true`, the `chat gpt-4o` dependency span gains two extra attributes:

| Attribute | Value |
|---|---|
| `gen_ai.prompt` | The full user message sent to the model |
| `gen_ai.completion` | The full response text returned by the model |

> **Production warning:** these attributes contain potentially sensitive content. Use only in environments where prompt and response data may be stored in your telemetry backend.

## Flow

```mermaid
sequenceDiagram
    participant App as Console App
    participant OTel as OpenTelemetry SDK
    participant AOAI as Azure OpenAI
    participant Console as Terminal
    participant AppIns as Application Insights

    App->>OTel: Build TracerProvider & MeterProvider
    App->>OTel: StartActivity("run-ai-prompt-inc-message-body", Server)

    App->>AOAI: IChatClient.GetResponseAsync(prompt)
    Note over App,AOAI: UseOpenTelemetry(EnableSensitiveData=true) creates child span "chat gpt-4o"<br/>Attributes: model, system, token counts<br/>+ gen_ai.prompt = "Hello! Can you tell me..."<br/>+ gen_ai.completion = "Sure! One interesting fact..."
    AOAI-->>App: ChatResponse (text + usage)

    App->>OTel: SetTag(ai.input_tokens, ai.output_tokens)
    App->>OTel: SetStatus(Ok) + EndActivity

    App->>OTel: ForceFlush (traces + metrics)
    OTel->>Console: Print spans and metrics (including message body)
    OTel->>AppIns: Export via Azure Monitor exporter
```

## What you see in App Insights

- **Transaction search → Requests**: one entry named `run-ai-prompt-inc-message-body`
- **Dependencies** (nested): `chat gpt-4o` — open Custom Properties to see `gen_ai.prompt` and `gen_ai.completion` containing the full message text
- **Metrics**: same token usage and latency metrics as `helloworld.logging`
