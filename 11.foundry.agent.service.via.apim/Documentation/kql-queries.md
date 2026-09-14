
## Look up APIM requests and extend key properties

```
requests
| where customDimensions.["API Name"] == "ms-blog-ai-samples-foundry"
| extend
    Request_x_correlation_id_ = tostring(customDimensions["Request-x-correlation-id"]),
    Request_x_customer_id_ = tostring(customDimensions["Request-x-customer-id"]),
    Request_x_username_ = tostring(customDimensions["Request-x-username"]),  
    ResponseBody = parse_json(tostring(customDimensions["Response-Body"]))
| extend
    total_tokens_ = tolong(ResponseBody.usage.total_tokens),
    output_tokens_ = tolong(ResponseBody.usage.output_tokens),
    input_tokens_ = tolong(ResponseBody.usage.input_tokens),
    API_Name_ = tostring(customDimensions.["API Name"]),
    Operation_Name_ = tostring(customDimensions.["Operation Name"]),
    agent_name_ = tostring(parse_json(tostring(parse_json(tostring(customDimensions.["Request-Body"])).agent_reference)).name),
    agent_version_ = tostring(parse_json(tostring(parse_json(tostring(customDimensions.["Request-Body"])).agent_reference)).version),
    ai_response_id_ = tostring(parse_json(tostring(customDimensions.["Response-Body"])).id)
| project
    timestamp, 
    API_Name_,
    Operation_Name_,
    agent_name_,
    agent_version_,
    Request_x_correlation_id_,
    Request_x_customer_id_,
    Request_x_username_,    
    total_tokens_,
    output_tokens_,
    input_tokens_,
    success,
    duration,
    ai_response_id_
| order by timestamp desc
```

## Lookup all requests and dependencies Related to a single request

```
let ai_response_id = "resp_0c13e81329dec99c006aa7d03195388195a0038944d43db33c";
union withsource=SourceTable requests, dependencies
| where parse_json(tostring(customDimensions["Response-Body"])).id == ai_response_id
    or customDimensions["gen_ai.response.id"] == ai_response_id
| extend
    Request_x_correlation_id_ = tostring(customDimensions["Request-x-correlation-id"]),
    Request_x_customer_id_ = tostring(customDimensions["Request-x-customer-id"]),
    Request_x_username_ = tostring(customDimensions["Request-x-username"]),
    gen_ai_request_model_ = tostring(customDimensions["gen_ai.request.model"]),
    gen_ai_provider_name_ = tostring(customDimensions["gen_ai.provider.name"]),
    gen_ai_agent_name_ = tostring(customDimensions["gen_ai.agent.name"]),
    gen_ai_usage_cache_read_input_tokens_ = tolong(customDimensions["gen_ai.usage.cache_read.input_tokens"]),
    gen_ai_usage_cached_tokens_ = tolong(customDimensions["gen_ai.usage.cached_tokens"]),
    gen_ai_usage_input_tokens_ = tolong(customDimensions["gen_ai.usage.input_tokens"]),
    gen_ai_usage_output_tokens_ = tolong(customDimensions["gen_ai.usage.output_tokens"]),
    microsoft_foundry_reasoning_tokens_ = tolong(customDimensions["microsoft.foundry.reasoning.tokens"]),
    ResponseBody = parse_json(tostring(customDimensions["Response-Body"]))
| extend
    total_tokens_ = tolong(ResponseBody.usage.total_tokens),
    output_tokens_ = tolong(ResponseBody.usage.output_tokens),
    input_tokens_ = tolong(ResponseBody.usage.input_tokens)
| project
    timestamp,
    SourceTable,
    Request_x_correlation_id_,
    Request_x_customer_id_,
    Request_x_username_,
    gen_ai_request_model_,
    gen_ai_provider_name_,
    gen_ai_agent_name_,
    gen_ai_usage_cache_read_input_tokens_,
    gen_ai_usage_cached_tokens_,
    gen_ai_usage_input_tokens_,
    gen_ai_usage_output_tokens_,
    microsoft_foundry_reasoning_tokens_,
    total_tokens_,
    output_tokens_,
    input_tokens_
| order by timestamp desc
```