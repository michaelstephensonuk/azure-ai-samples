

## Requests

### Requests showing Sum of Tokens by Correlation Requests in the same agent execution

```
requests  
| extend total_tokens_ = toint(parse_json(tostring(parse_json(tostring(customDimensions.["Response-Body"])).usage)).total_tokens)
| where total_tokens_ > 0
| extend API_Name_ = tostring(customDimensions.["API Name"])
| extend Subscription_Name_ = tostring(customDimensions.["Subscription Name"])
| summarize sum(total_tokens_), count() by API_Name_, Subscription_Name_, operation_Id
| order by sum_total_tokens_ desc 

```

### Tokens Use by hour and API / Subscription

```
requests  
| extend total_tokens_ = toint(parse_json(tostring(parse_json(tostring(customDimensions.["Response-Body"])).usage)).total_tokens)
| where total_tokens_ > 0
| extend API_Name_ = tostring(customDimensions.["API Name"])
| extend Subscription_Name_ = tostring(customDimensions.["Subscription Name"])
| summarize sum(total_tokens_) by bin(timestamp, 1h), API_Name_, Subscription_Name_
```

### Pick a given Agent Execution and see each request and the tokens used

```
requests  
| where operation_Id == "a8df2df359e26155cd0ea20d26eb59bd"
| extend total_tokens_ = tostring(parse_json(tostring(parse_json(tostring(customDimensions.["Response-Body"])).usage)).total_tokens)
```