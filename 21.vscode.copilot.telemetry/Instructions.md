

## Configure at workspace Level

Add these settings to the following file:

.vscode\settings.json

```
{
  "github.copilot.chat.otel.enabled": true,
  "github.copilot.chat.otel.exporterType": "otlp-http",
  "github.copilot.chat.otel.otlpEndpoint": "http://localhost:4318",
  "github.copilot.chat.otel.captureContent": true
}
```


## Configure for all VS Code Solutions

Add these settings to your **User Settings** so they apply to every workspace on this machine.

### Option A — VS Code UI

1. Open VS Code
2. Press `Ctrl + Shift + P` and run **Preferences: Open User Settings**
3. Search for each setting name below and set the value

### Option B — Edit settings.json directly

1. Press `Ctrl + Shift + P` and run **Preferences: Open User Settings (JSON)**
2. This opens `%APPDATA%\Code\User\settings.json`
3. Add the following entries:

```json
{
  "github.copilot.chat.otel.enabled": true,
  "github.copilot.chat.otel.exporterType": "otlp-http",
  "github.copilot.chat.otel.otlpEndpoint": "http://localhost:4318",
  "github.copilot.chat.otel.captureContent": true
}
```

> **Note:** User settings are stored at `%APPDATA%\Code\User\settings.json` on Windows. Settings here apply globally across all workspaces and projects on this machine, unless a workspace-level `.vscode\settings.json` overrides them.

---

## Configure via Environment Variables

GitHub Copilot also reads a set of `COPILOT_OTEL_*` environment variables. These are useful for:

- **Machine-wide configuration** without editing any JSON file
- **Shared/managed machines** where settings.json is not practical to deploy
- **CI or scripted environments** where you want to inject values at launch time

Set these as Windows **System environment variables** (so they apply to all users) or **User environment variables** (current user only):

| Environment Variable | Equivalent Setting | Example Value |
|---------------------|-------------------|---------------|
| `COPILOT_OTEL_ENABLED` | `github.copilot.chat.otel.enabled` | `true` |
| `COPILOT_OTEL_EXPORTER_TYPE` | `github.copilot.chat.otel.exporterType` | `otlp-http` |
| `COPILOT_OTEL_OTLP_ENDPOINT` | `github.copilot.chat.otel.otlpEndpoint` | `http://localhost:4318` |
| `COPILOT_OTEL_CAPTURE_CONTENT` | `github.copilot.chat.otel.captureContent` | `true` |

> **Note:** The env var names above follow the `COPILOT_OTEL_ENABLED` pattern that is confirmed. The others are the expected equivalents based on the same naming convention. If a variable does not take effect, fall back to the `settings.json` approach above.

### Set via PowerShell (current user)

```powershell
[System.Environment]::SetEnvironmentVariable("COPILOT_OTEL_ENABLED",        "true",        "User")
[System.Environment]::SetEnvironmentVariable("COPILOT_OTEL_EXPORTER_TYPE",  "otlp-http",   "User")
[System.Environment]::SetEnvironmentVariable("COPILOT_OTEL_OTLP_ENDPOINT",  "http://localhost:4318", "User")
[System.Environment]::SetEnvironmentVariable("COPILOT_OTEL_CAPTURE_CONTENT","true",        "User")
```

### Set via PowerShell (all users — requires admin)

```powershell
[System.Environment]::SetEnvironmentVariable("COPILOT_OTEL_ENABLED",        "true",        "Machine")
[System.Environment]::SetEnvironmentVariable("COPILOT_OTEL_EXPORTER_TYPE",  "otlp-http",   "Machine")
[System.Environment]::SetEnvironmentVariable("COPILOT_OTEL_OTLP_ENDPOINT",  "http://localhost:4318", "Machine")
[System.Environment]::SetEnvironmentVariable("COPILOT_OTEL_CAPTURE_CONTENT","true",        "Machine")
```

> **Important:** Restart VS Code after setting environment variables — they are only read at process startup.

---

## Configure via Environment Variables

- COPILOT_OTEL_ENABLED


## What each setting means

| Setting | Type | Description |
|---------|------|-------------|
| `github.copilot.chat.otel.enabled` | `boolean` | Turns on OpenTelemetry export from GitHub Copilot. When `true`, Copilot emits traces and metrics for every AI interaction. Set to `false` to disable without removing the other settings. |
| `github.copilot.chat.otel.exporterType` | `string` | The transport protocol used to send telemetry to the collector. `otlp-http` sends data over HTTP using the OTLP protocol (port 4318). Use `otlp-grpc` if you prefer gRPC (port 4317). `otlp-http` is the most compatible option. |
| `github.copilot.chat.otel.otlpEndpoint` | `string` | The URL of the OpenTelemetry collector endpoint. `http://localhost:4318` targets the OTel Collector Contrib Windows service running on your local machine (see sample 20). Change this if your collector is on a different host or port. |
| `github.copilot.chat.otel.captureContent` | `boolean` | When `true`, Copilot includes the actual prompt and response text in the telemetry spans (as `gen_ai.prompt` and `gen_ai.completion` attributes). This is useful for debugging and content analysis but means sensitive prompt content will appear in Application Insights. Set to `false` if you only need token counts and latency without capturing message content. |


## More Info & Useful Links

https://github.com/microsoft/vscode-copilot-chat/blob/main/docs/monitoring/agent_monitoring.md