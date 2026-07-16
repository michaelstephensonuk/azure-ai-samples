
## Requests Table

### Get Token usage by hour per API and subscription

```
requests  
| extend total_tokens_ = toint(parse_json(tostring(parse_json(tostring(customDimensions.["Response-Body"])).usage)).total_tokens)
| where total_tokens_ > 0
| extend API_Name_ = tostring(customDimensions.["API Name"])
| extend Subscription_Name_ = tostring(customDimensions.["Subscription Name"])
| summarize sum(total_tokens_) by bin(timestamp, 1h), API_Name_, Subscription_Name_


```