# foundry.agent.service.via.agent.sdk

A .NET 10 console app that calls an **agent already created and deployed in Azure AI Foundry** (Agent Service) directly via the **Azure AI Projects SDK** — no local agent definition, tools, or Microsoft Agent Framework (MAF) layer involved.

Unlike samples 05/07/09, the agent's instructions, model, and tools all live server-side in Azure AI Foundry. This app just references it by **name + version** and asks it a question.

## What this sample shows

- Connecting to an Azure AI Foundry project with `AIProjectClient` and `DefaultAzureCredential` (no API key — your Azure identity is the credential)
- Referencing a deployed agent with `AgentReference(name, version)`
- Getting a `ProjectResponsesClient` scoped to that agent via `projectClient.OpenAI.GetProjectResponsesClientForAgent(...)`
- Calling the agent with `CreateResponseAsync(prompt)` and reading `response.GetOutputText()`
- Injecting custom HTTP headers (correlation ID, customer ID, username, ...) onto every outbound request via a `PipelinePolicy` added with `ClientPipelineOptions.AddPolicy(...)`, so they can be picked up by an AI gateway (APIM, App Gateway, etc.) sitting in front of Foundry and surfaced in the telemetry it logs to App Insights — e.g. attributing AI token usage/cost to a specific customer or user

## Key NuGet packages

| Package | Purpose |
|---|---|
| `Azure.AI.Projects` 2.0.0-beta.2 | `AIProjectClient` — connects to the Foundry project (also pulls in `Azure.AI.Projects.Agents` and `Azure.AI.Extensions.OpenAI`) |
| `Azure.AI.Extensions.OpenAI` (transitive) | `AgentReference` / `ProjectResponsesClient` — OpenAI Responses API scoped to a Foundry agent |
| `Azure.Identity` 1.19.0 | `DefaultAzureCredential` |

> These are beta packages — types have moved between namespaces across recent versions. `AgentReference` and `ProjectResponsesClient` currently live in `Azure.AI.Extensions.OpenAI`, not `Azure.AI.Projects.Agents`, despite the package name.

## Prerequisites

- An agent already created and deployed in your Azure AI Foundry project (via the Foundry portal, or `projectClient.Agents.CreateAgentVersionAsync(...)`)
- Signed in with `az login` — `DefaultAzureCredential` uses your Azure CLI identity, and your account needs access to the Foundry project
- If you hit `AADSTS...interaction required` / `AzureCliCredential authentication failed`, re-consent with:
  ```bash
  az login --scope https://ai.azure.com/.default
  ```

## Configuration

Copy `appsettings.template.json` to `appsettings.json` and fill in your values:

```json
{
  "Foundry": {
    "Endpoint": "https://<your-foundry-resource>.services.ai.azure.com/api/projects/<your-project>",
    "AgentName": "<your-agent-name>",
    "AgentVersion": "1",
    "UserPrompt": "Hello! Tell me a joke.",
    "CustomHeaders": {
      "x-correlation-id": "",
      "x-customer-id": "<your-customer-id>",
      "x-username": "<your-username-or-email>"
    }
  }
}
```

`appsettings.json` is gitignored. Values can also be supplied via environment variables (`Foundry__CustomHeaders__x-customer-id`, etc.). `CustomHeaders` accepts any number of entries — add/remove keys freely; `x-correlation-id` gets a fresh GUID per run if left blank, everything else is sent exactly as configured.

## Running locally

Press **F5** in VS Code (select the `foundry.agent.service.via.agent.sdk` configuration) or:

```bash
dotnet run
```


## Notes

- No `ApiKey` here — auth is entirely `DefaultAzureCredential` against the Foundry project endpoint.
- `AgentVersion` matters: Foundry agents are versioned, and `AgentReference` pins the call to a specific version rather than always following "latest".
- This sample intentionally skips OpenTelemetry/App Insights (unlike samples 05/07/09) to keep the new Foundry Agent SDK surface isolated and easy to read; the same `ActivitySource` + Azure Monitor exporter pattern from those samples can be layered on top if you want to compare telemetry for this path too.
