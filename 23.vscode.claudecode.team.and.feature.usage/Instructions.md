

## Summary of Developer Features Built by Day

In this query, we have added the below attribute in the .claude\settings.json file.

```
"OTEL_RESOURCE_ATTRIBUTES": "department=dev,feature=cool-feature2"
```

An example of this would be

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
    "OTEL_METRICS_INCLUDE_VERSION": "true",

    "OTEL_RESOURCE_ATTRIBUTES": "department=dev,feature=cool-feature2"
  }
}

```

Each time the developer starts working on a new feature they will update the attribute to set the 
name of the feature they are working on

This means that each time they use claude code on a feature it will include the telemetry to Azure App Insights about which feature the user is developing

```

traces
| where message == "claude_code.api_request"  
| extend feature_ = tostring(customDimensions.feature)
| extend cost_usd_ = todecimal(customDimensions.cost_usd)
| extend input_tokens_ = tolong(customDimensions.input_tokens)
| extend output_tokens_ = tolong(customDimensions.output_tokens)
| extend user_email_ = tostring(customDimensions.["user.email"])
| extend cache_creation_tokens_ = tolong(customDimensions.cache_creation_tokens)
| extend cache_read_tokens_ = tolong(customDimensions.cache_read_tokens)
| summarize 
    CostUSD=sum(cost_usd_), 
    Requests=count(), 
    InputTokens=sum(input_tokens_), 
    OutputTokens=sum(output_tokens_), 
    CacheCreationTokens_=sum(cache_creation_tokens_), 
    CacheReadTokens_=sum(cache_read_tokens_) 
    by 
    Feature=feature_, 
    Email=user_email_


```

I can then see the tokens per developer per feature in the below image

23.vscode.claudecode.team.and.feature.usage\01.AppInsights.TokensPerFeature.png