

## Dependencies

### List of Calls to AI

```
dependencies 
| where type == "Other"
| extend gen_ai_provider_name_ = tostring(customDimensions.["gen_ai.provider.name"])
| extend gen_ai_request_model_ = tostring(customDimensions.["gen_ai.request.model"])
| extend gen_ai_usage_cache_read_input_tokens_ = tostring(customDimensions.["gen_ai.usage.cache_read.input_tokens"])
| extend gen_ai_usage_input_tokens_ = tostring(customDimensions.["gen_ai.usage.input_tokens"])
| extend gen_ai_usage_output_tokens_ = tostring(customDimensions.["gen_ai.usage.output_tokens"])
| extend gen_ai_usage_reasoning_output_tokens_ = tostring(customDimensions.["gen_ai.usage.reasoning.output_tokens"])
| extend openai_api_type_ = tostring(customDimensions.["openai.api.type"])
| project timestamp, operation_Id, name, gen_ai_provider_name_, gen_ai_request_model_, gen_ai_usage_cache_read_input_tokens_, gen_ai_usage_input_tokens_, gen_ai_usage_output_tokens_, gen_ai_usage_reasoning_output_tokens_, openai_api_type_
| order by timestamp desc 


```

## Custom Metric Info

### Input and Output tokens used by hour

```
customMetrics
| where name == "gen_ai.client.token.usage"
| extend tokenType = tostring(customDimensions["gen_ai.token.type"])
| summarize totalTokens = sum(value) by tokenType, bin(timestamp, 1h)
| order by timestamp desc

```


### Tokens by Application used by hour

```
customMetrics
| where name == "gen_ai.client.token.usage"
| summarize totalTokens = sum(value) by cloud_RoleName, bin(timestamp, 1h)
| order by timestamp desc

```

