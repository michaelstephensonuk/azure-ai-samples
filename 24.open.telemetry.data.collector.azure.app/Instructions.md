## Setup

### 1. Deploy Log Analytics, Application Insights and a Container Apps Environment

```
az group create -n rg-otel-collector -l uksouth

az monitor log-analytics workspace create \
  -g rg-otel-collector -n law-otel-collector

az monitor app-insights component create \
  -g rg-otel-collector -a appi-otel-collector -l uksouth \
  --workspace law-otel-collector

az containerapp env create \
  -g rg-otel-collector -n cae-otel-collector -l uksouth
```

Notes:
- Application Insights is created as workspace-based, pointing at the Log Analytics workspace above
- Grab the connection string for later: `az monitor app-insights component show -g rg-otel-collector -a appi-otel-collector --query connectionString -o tsv`

### 2. Package the OTel Collector Contrib image with our config

`config.yaml` in this folder is the same collector config as [sample 20](../20.open.telemetry.data.collector), with the exporter's connection string read from an environment variable instead of hard-coded, so it can be supplied by a Container Apps secret:

```
exporters:
  azuremonitor:
    connection_string: "${env:APPINSIGHTS_CONNECTION_STRING}"
```

Dockerfile:

```
FROM otel/opentelemetry-collector-contrib:0.157.0
COPY config.yaml /etc/otelcol-contrib/config.yaml
```

Build and push to an Azure Container Registry:

```
az acr create -g rg-otel-collector -n acrotelcollector --sku Basic
az acr build -r acrotelcollector -t otel-collector:0.157.0 .
```

### 3. Deploy the collector as a Container App

```
az containerapp create \
  -g rg-otel-collector -n ca-otel-collector \
  --environment cae-otel-collector \
  --image acrotelcollector.azurecr.io/otel-collector:0.157.0 \
  --registry-server acrotelcollector.azurecr.io \
  --target-port 4318 \
  --ingress external \
  --secrets appinsights-connection-string="<CONNECTION-STRING-FROM-STEP-1>" \
  --env-vars APPINSIGHTS_CONNECTION_STRING=secretref:appinsights-connection-string \
  --min-replicas 1 --max-replicas 1
```

Notes:
- `--target-port 4318` exposes the OTLP/HTTP receiver; Container Apps ingress terminates TLS, so clients call the collector over HTTPS on port 443
- OTLP/gRPC (4317) can also be exposed, but needs the ingress transport set to `http2` — stick to OTLP/HTTP unless a client specifically needs gRPC
- `--min-replicas 1` keeps a warm instance running so telemetry isn't dropped while the app scales from zero
- Rotating the connection string is now a single `az containerapp secret set` — no per-machine changes required

### 4. Configure VS Code AI extensions

Same GitHub Copilot / Claude Code OpenTelemetry settings as [sample 20](../20.open.telemetry.data.collector), but pointing at the container app's public HTTPS endpoint instead of `localhost`:

```
https://ca-otel-collector.<random-suffix>.uksouth.azurecontainerapps.io
```

Every developer in the team uses this same URL — there is nothing to install locally.

### Verify

- Tail the collector's logs: `az containerapp logs show -g rg-otel-collector -n ca-otel-collector --follow`
- Trigger a Copilot or Claude Code action in VS Code and confirm events appear in Application Insights → Transaction Search within ~30 seconds
- Data lands in the same `dependencies`, `traces` and `customMetrics` tables as the Windows service approach — existing KQL queries and dashboards work unchanged

## Troubleshoot

- If nothing arrives, confirm the container app's ingress target port (4318) matches what VS Code is sending to, and that the URL uses `https://`, not `http://`
- Check `az containerapp revision list` to confirm the latest revision is running and healthy
- Confirm the `APPINSIGHTS_CONNECTION_STRING` secret is set and referenced correctly in the container app's env vars
