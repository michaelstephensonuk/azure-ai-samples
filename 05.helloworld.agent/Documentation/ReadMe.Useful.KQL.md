

## genAIContent Log

### Get a trace of log events related to a specific execution of an agent for messages

```
genAIContent 
| where cloud_RoleName <> "[Your Host Name]"
| where serviceName == "[Your Agent Name]"
| where operation_Id == "[Your operation ID]"
| order by timestamp desc 
| extend gen_ai_usage_input_tokens_ = tostring(customDimensions.["gen_ai.usage.input_tokens"])
| extend gen_ai_usage_output_tokens_ = tostring(customDimensions.["gen_ai.usage.output_tokens"])
| extend gen_ai_usage_reasoning_output_tokens_ = tostring(customDimensions.["gen_ai.usage.reasoning.output_tokens"])
| extend gen_ai_usage_cache_read_input_tokens_ = tostring(customDimensions.["gen_ai.usage.cache_read.input_tokens"])
```

## Dependencies


### Get Agent Executions

Note this query uses the custom dependency we added in the code to simulate what the right
telemetry usage that would be used by agents in foundry so it shows up in App Insights
agent preview dashboard ok

```
dependencies 
| where type == "Other"
| where customDimensions.["gen_ai.operation.name"] == "invoke_agent"
| extend ai_input_tokens_ = tostring(customDimensions.["ai.input_tokens"])
| extend ai_output_tokens_ = tostring(customDimensions.["ai.output_tokens"])
| extend gen_ai_agent_name_ = tostring(customDimensions.["gen_ai.agent.name"])
| extend gen_ai_operation_name_ = tostring(customDimensions.["gen_ai.operation.name"])
| project timestamp, gen_ai_agent_name_, gen_ai_operation_name_, ai_input_tokens_, ai_output_tokens_, operation_Id, name, cloud_RoleName, success, duration
| order by timestamp desc 


```

### Get Tool Executions

```
dependencies 
| where type == "InProc"
| where customDimensions.["gen_ai.operation.name"] == "execute_tool"
| extend gen_ai_tool_name_ = tostring(customDimensions.["gen_ai.tool.name"])
| extend gen_ai_tool_type_ = tostring(customDimensions.["gen_ai.tool.type"])
| extend microsoft_gen_ai_main_agent_name_ = tostring(customDimensions.["microsoft.gen_ai.main_agent.name"])
| extend gen_ai_tool_call_result_ = tostring(parse_json(tostring(customDimensions.["gen_ai.tool.call.result"])))
| project timestamp, name, success, duration, gen_ai_tool_name_, gen_ai_tool_type_, microsoft_gen_ai_main_agent_name_, gen_ai_tool_call_result_, operation_Id


```

### Get tool executions for my specific request

```
dependencies 
| where type == "InProc"
| where customDimensions.["gen_ai.operation.name"] == "execute_tool"
| extend gen_ai_tool_name_ = tostring(customDimensions.["gen_ai.tool.name"])
| extend gen_ai_tool_type_ = tostring(customDimensions.["gen_ai.tool.type"])
| extend microsoft_gen_ai_main_agent_name_ = tostring(customDimensions.["microsoft.gen_ai.main_agent.name"])
| extend gen_ai_tool_call_result_ = tostring(parse_json(tostring(customDimensions.["gen_ai.tool.call.result"])))
| project timestamp, name, success, duration, gen_ai_tool_name_, gen_ai_tool_type_, microsoft_gen_ai_main_agent_name_, gen_ai_tool_call_result_, operation_Id
| where operation_Id == "0d9bb17249974b4a518a0088c477b237"
```


### See how the agent tool calls increase the number of tokens each time

Subsequent calls to the model will exponentially increase the number of tokens being returned.

```
dependencies
| where operation_Id == "a6031436df0a998c229fb8c7ffbb96ac"
| extend gen_ai_usage_input_tokens_ = tostring(customDimensions.["gen_ai.usage.input_tokens"])
| extend gen_ai_usage_output_tokens_ = tostring(customDimensions.["gen_ai.usage.output_tokens"])
| extend inputMessages = tostring(parse_json(tostring(customDimensions.["gen_ai.input.messages"])))
| extend ParsedArray = parse_json(inputMessages)
| extend ItemCount = array_length(ParsedArray)
| project timestamp, operation_Id, name, type, gen_ai_usage_input_tokens_, gen_ai_usage_output_tokens_, success, duration, ItemCount
```