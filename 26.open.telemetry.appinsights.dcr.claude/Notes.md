
## Aim
The aim is to provide an alternative to using a OTEL Collector for Claude development.

Instead we will create an App Insights instance with OLTP Support

We will then use a Data Collection Rule (DCR) to ingest telemetry

We will setup vscode to be able to authenticate with the DCR using Entra

Telemetry from claude will be ingested as we use VS Code.

## Pre Req

- Make sure Microsoft.Monitor is registered for your subscription
- Create an App Insights with OLTP support

This image shows the setup for App Insights
26.open.telemetry.appinsights.dcr.claude\image-1.png

- 

## Setup

1) We get the endpoint for DCR from App Insights

This image shows it
26.open.telemetry.appinsights.dcr.claude\image-2.png

2) Add the Monitoring Metrics Publisher RBAC role

Add this role for the DCR

Watch for a gotcha, You need to add it for the right place.  In my case I used a managed workspace and rather than adding the RBAC role on App Insights i need to add it to the managed resource group


2) Add a bash script to do the authentication

Create a file called generate-otel-headers.sh

```
#!/bin/bash
# generate-otel-headers.sh
TOKEN=$(az account get-access-token --resource "https://monitor.azure.com" --query accessToken -o tsv)
echo "{\"Authorization\": \"Bearer $TOKEN\"}"

# Note that this script requires the Azure CLI to be installed and authenticated. You can run this script to generate the necessary OpenTelemetry headers for sending telemetry data to Azure Monitor.
# You need to have the Azure CLI installed 
# and be logged in to your Azure account for this script to work. The script retrieves an access token for the Azure Monitor resource and formats it as a JSON object with the appropriate Authorization header.
# You also need the Monitoring Metrics Publisher role assigned to your Azure account to send telemetry data to Azure Monitor.
```

3) Modify settings.json for your claude

in file .claude/settings.json add the below

```
{
  "env": {
    "CLAUDE_CODE_ENABLE_TELEMETRY": "1",
    "OTEL_METRICS_EXPORTER": "otlp",
    "OTEL_LOGS_EXPORTER": "otlp",    
    "OTEL_EXPORTER_OTLP_PROTOCOL": "http/protobuf",
    "OTEL_EXPORTER_OTLP_ENDPOINT": "[Add DCR endpoint here]",
    "OTEL_RESOURCE_ATTRIBUTES": "asset.name=sample-app",

    "CLAUDE_CODE_OTEL_HEADERS_HELPER_DEBOUNCE_MS": "900000",
    "otelHeadersHelper": "generate-otel-headers.sh"
  }
}


```

- OTEL_EXPORTER_OTLP_ENDPOINT = the endpoint for DCR from app insights

- otelHeadersHelper = References the bash script to do the authentication header.  Note you need to be logged in with az cli