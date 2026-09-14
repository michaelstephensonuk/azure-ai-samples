

## Look up agent usage

```
dependencies
| where type == "AI"
| where data == "chat"
| where customDimensions.["microsoft.foundry"] == "True"
| where customDimensions.["gen_ai.agent.name"] <> ""
| extend 
    gen_ai_request_model_ = tostring(customDimensions.["gen_ai.request.model"]),
    gen_ai_provider_name_ = tostring(customDimensions.["gen_ai.provider.name"]),
    gen_ai_agent_name_ = tostring(customDimensions.["gen_ai.agent.name"]),
    gen_ai_usage_cache_read_input_tokens_ = tostring(customDimensions.["gen_ai.usage.cache_read.input_tokens"]),
    gen_ai_usage_cached_tokens_ = tostring(customDimensions.["gen_ai.usage.cached_tokens"]),
    gen_ai_usage_input_tokens_ = tostring(customDimensions.["gen_ai.usage.input_tokens"]),
    gen_ai_usage_output_tokens_ = tostring(customDimensions.["gen_ai.usage.output_tokens"]),
    microsoft_foundry_reasoning_tokens_ = tostring(customDimensions.["microsoft.foundry.reasoning.tokens"])
| project
    timestamp,
    gen_ai_agent_name_,
    gen_ai_provider_name_,
    gen_ai_request_model_,
    gen_ai_usage_cache_read_input_tokens_,
    gen_ai_usage_cached_tokens_,
    gen_ai_usage_input_tokens_,
    gen_ai_usage_output_tokens_,
    microsoft_foundry_reasoning_tokens_,
    name,
    success,
    duration
| order by timestamp desc

```
