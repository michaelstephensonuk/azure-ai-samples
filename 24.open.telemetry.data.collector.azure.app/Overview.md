
## What is it

- The same idea as [20.open.telemetry.data.collector](../20.open.telemetry.data.collector), but the OpenTelemetry Collector runs once, centrally, as a container in Azure Container Apps instead of as a Windows service on every developer machine
- Send VS Code AI usage (GitHub Copilot, Claude Code) to Application Insights via OpenTelemetry
- Understand token usage across the whole team from one shared endpoint

## How does it work

1. A Log Analytics workspace is deployed
2. Application Insights is deployed pointing at that Log Analytics workspace
3. A Container Apps Environment is deployed to host the collector
4. The OpenTelemetry Collector Contrib image is packaged with a config.yaml and deployed as a container app, with public HTTPS ingress
5. The App Insights connection string is stored as a Container Apps secret and injected into the container as an environment variable — no image rebuild needed to rotate it
6. In VS Code, GitHub Copilot and/or Claude Code settings are configured to send OpenTelemetry to the container app's HTTPS endpoint instead of `localhost`
7. The collector forwards the telemetry to Application Insights using the Azure Monitor exporter
8. Data lands in the `dependencies`, `traces`, `customMetrics` (and `genAIContent`, where emitted) tables, same as the Windows service approach
9. KQL dashboards (Azure Workbooks, Grafana, Turbo360) work exactly the same way on top of this data

## Why

- Remove the need to install and maintain a Windows service on every developer's machine
- Centralize configuration — one place to update the connection string, exporter settings, or collector version
- Every developer in the team points at the same shared endpoint

## Alternative architecture

This is an alternative to [20.open.telemetry.data.collector](../20.open.telemetry.data.collector), which installs the collector as a Windows service on each dev machine. Both approaches write to the same Application Insights tables and support the same downstream KQL/dashboard tooling — the difference is purely in where the collector runs and how its configuration is managed.
