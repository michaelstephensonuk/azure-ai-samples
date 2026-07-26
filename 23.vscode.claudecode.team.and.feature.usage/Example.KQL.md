


## Cost Per Developer / Per Feature

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

## Cost Per User

```

let tool_usage = traces
| where message startswith "claude_code."
| where customDimensions["event.name"] == "tool_decision"
| summarize totalToolCalls = count() by userId = tostring(customDimensions["user.email"]);
let prompts = traces
| where message startswith "claude_code."
| where customDimensions["event.name"] == "user_prompt"
| summarize totalPrompts = count() by userId = tostring(customDimensions["user.email"]);
let api_reqs = traces
| where message startswith "claude_code."
| where customDimensions["event.name"] == "api_request"
| summarize totalApiRequests = count(), totalCost = sum(todouble(customDimensions["cost_usd"])), totalTokens = sum(todouble(customDimensions["input_tokens"]) + todouble(customDimensions["output_tokens"])), totalActiveTimeSec = sum(todouble(customDimensions["duration_ms"])) / 1000.0, totalSessions = dcount(tostring(customDimensions["session.id"])) by userId = tostring(customDimensions["user.email"]);
let api_errs = traces
| where message startswith "claude_code."
| where customDimensions["event.name"] == "api_error"
| summarize totalApiErrors = count() by userId = tostring(customDimensions["user.email"]);
let loc = customMetrics
| where name == "claude_code.lines_of_code.count"
| extend changeType = tostring(customDimensions["type"])
| summarize linesAdded = sumif(value, changeType == "added"), linesRemoved = sumif(value, changeType == "removed") by userId = tostring(customDimensions["user.email"]);
let users = union
    (tool_usage | project userId),
    (prompts | project userId),
    (api_reqs | project userId),
    (api_errs | project userId),
    (loc | project userId)
| distinct userId;
users
| join kind=leftouter tool_usage on userId
| join kind=leftouter prompts on userId
| join kind=leftouter api_reqs on userId
| join kind=leftouter api_errs on userId
| join kind=leftouter loc on userId
| project User=userId, Sessions=totalSessions, Prompts=totalPrompts, ToolCalls=totalToolCalls, ApiRequests=totalApiRequests, ApiErrors=totalApiErrors, Cost=round(totalCost,2), Tokens=totalTokens, ActiveHrs=round(totalActiveTimeSec/3600.0,2), LinesAdded=linesAdded, LinesRemoved=linesRemoved

```