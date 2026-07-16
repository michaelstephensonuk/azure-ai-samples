

## genAIContent Log

### List of Logged Content in last 1 hour

```
genAIContent 
| where timestamp > ago(1h)
| extend gen_ai_usage_reasoning_output_tokens_ = tostring(customDimensions.["gen_ai.usage.reasoning.output_tokens"])
| extend gen_ai_usage_output_tokens_ = tostring(customDimensions.["gen_ai.usage.output_tokens"])
| extend gen_ai_usage_input_tokens_ = tostring(customDimensions.["gen_ai.usage.input_tokens"])
| extend gen_ai_usage_cache_read_input_tokens_ = tostring(customDimensions.["gen_ai.usage.cache_read.input_tokens"])
| extend gen_ai_response_model_ = tostring(customDimensions.["gen_ai.response.model"])
| extend gen_ai_response_id_ = tostring(customDimensions.["gen_ai.response.id"])
| extend gen_ai_provider_name_ = tostring(customDimensions.["gen_ai.provider.name"])
| extend gen_ai_operation_name_ = tostring(customDimensions.["gen_ai.operation.name"])
| extend output_content_ = tostring(parse_json(tostring(parse_json(outputMessages)[0].parts))[0].content)
| extend input_content_ = tostring(parse_json(tostring(parse_json(inputMessages)[0].parts))[0].content)
| project timestamp, operation_Id, id, operation_ParentId, modelName, cloud_RoleName, itemType, serviceName, gen_ai_usage_reasoning_output_tokens_, gen_ai_usage_output_tokens_, gen_ai_usage_input_tokens_, gen_ai_usage_cache_read_input_tokens_, gen_ai_response_model_, gen_ai_response_id_, gen_ai_provider_name_, gen_ai_operation_name_, output_content_, input_content_


```

## Dependencies

### Logged Dependencies including input and output content

```
dependencies 
| where timestamp > ago(1h)
| extend gen_ai_usage_reasoning_output_tokens_ = tostring(customDimensions.["gen_ai.usage.reasoning.output_tokens"])
| extend gen_ai_usage_output_tokens_ = tostring(customDimensions.["gen_ai.usage.output_tokens"])
| extend gen_ai_usage_input_tokens_ = tostring(customDimensions.["gen_ai.usage.input_tokens"])
| extend gen_ai_usage_cache_read_input_tokens_ = tostring(customDimensions.["gen_ai.usage.cache_read.input_tokens"])
| extend gen_ai_response_model_ = tostring(customDimensions.["gen_ai.response.model"])
| extend gen_ai_response_id_ = tostring(customDimensions.["gen_ai.response.id"])
| extend gen_ai_provider_name_ = tostring(customDimensions.["gen_ai.provider.name"])
| extend gen_ai_operation_name_ = tostring(customDimensions.["gen_ai.operation.name"])
| extend input_content_ = tostring(parse_json(tostring(parse_json(tostring(customDimensions.["gen_ai.input.messages"]))[0].parts))[0].content)
| extend output_content_ = tostring(parse_json(tostring(parse_json(tostring(customDimensions.["gen_ai.output.messages"]))[0].parts))[0].content)
| extend gen_ai_request_model_ = tostring(customDimensions.["gen_ai.request.model"])
| extend openai_api_type_ = tostring(customDimensions.["openai.api.type"])
| project timestamp, operation_Id, id, operation_ParentId, cloud_RoleName, itemType, gen_ai_usage_reasoning_output_tokens_, gen_ai_usage_output_tokens_, gen_ai_usage_input_tokens_, gen_ai_usage_cache_read_input_tokens_, gen_ai_response_model_, gen_ai_response_id_, gen_ai_provider_name_, gen_ai_operation_name_, output_content_, input_content_, success, duration, name, gen_ai_request_model_, openai_api_type_


```


### Tokens by Application used by hour

```
customMetrics
| where name == "gen_ai.client.token.usage"
| summarize totalTokens = sum(value) by cloud_RoleName, bin(timestamp, 1h)
| order by timestamp desc

```

