

## Option 1 - Workspace Level

1. Create a claude settings file if you dont have one at the following path:
.claude\settings.json

2. Add the below settings to set the values which will be sent to Open Telemetry

```

{
  "env": {
    "CLAUDE_CODE_ENABLE_TELEMETRY": "1",
    "OTEL_METRICS_EXPORTER": "otlp",
    "OTEL_LOGS_EXPORTER": "otlp",
    "OTEL_EXPORTER_OTLP_PROTOCOL": "http/protobuf",
    "OTEL_EXPORTER_OTLP_ENDPOINT": "http://localhost:4318",
    "OTEL_LOG_USER_PROMPTS": "1",
    "OTEL_LOG_TOOL_DETAILS": "1",
    "OTEL_METRICS_INCLUDE_VERSION": "true"    
  }
}

```

3. Start using Claude Code and then check App Insights


## Option 2 - Environment Variables

Set the required environment variables at the user level using PowerShell, then relaunch your terminal / Claude Code so the new values are picked up.

```powershell
# Claude Code OpenTelemetry environment variables
# Run once in PowerShell, then restart your terminal or Claude Code session.

param(
    [string]$OtlpEndpoint = "http://localhost:4318"
)

$vars = @{
    "CLAUDE_CODE_ENABLE_TELEMETRY" = "1"
    "OTEL_METRICS_EXPORTER"        = "otlp"
    "OTEL_LOGS_EXPORTER"           = "otlp"
    "OTEL_EXPORTER_OTLP_PROTOCOL"  = "http/protobuf"
    "OTEL_EXPORTER_OTLP_ENDPOINT"  = $OtlpEndpoint
    "OTEL_LOG_USER_PROMPTS"        = "1"
    "OTEL_LOG_TOOL_DETAILS"        = "1"
    "OTEL_METRICS_INCLUDE_VERSION" = "true"
}

foreach ($key in $vars.Keys) {
    [System.Environment]::SetEnvironmentVariable($key, $vars[$key], "User")
    Write-Host "Set $key = $($vars[$key])" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Environment variables set. Restart Claude Code to apply." -ForegroundColor Cyan
```

To also set `OTEL_RESOURCE_ATTRIBUTES` with feature/work item context (useful for filtering in App Insights):

```powershell
$FeatureName = Read-Host "Feature name (e.g. my-feature-x)"
$WorkItemId  = Read-Host "Work item ID (e.g. 12345)"

$attributes = "service.name=$FeatureName,work.item.id=$WorkItemId,deployment.environment=dev,department=engineering,team.id=platform,cost_center=eng-123"

[System.Environment]::SetEnvironmentVariable("OTEL_RESOURCE_ATTRIBUTES", $attributes, "User")

Write-Host "OTEL_RESOURCE_ATTRIBUTES set to:" -ForegroundColor Cyan
Write-Host "  $attributes" -ForegroundColor Yellow
```