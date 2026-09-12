
## Aim

I am not moving on to look at telemetry for agents hosted in foundry agent service.

I will build an agent using the grok model
I will call it using C#
We will look at what gets logged out of the box

## Before we look at the code

I setup foundry and APIM and App Insights
Although I am calling foundry directly and not via APIM in this case
Ill talk about the APIM bits in the next sample

## 1. Create an Agent

I created an agent in Foundry.  I used a prompt agent rather than a code agent

These are the images:
10.foundry.agent.service.via.agent.sdk\Documentation\foundry-agent.png
10.foundry.agent.service.via.agent.sdk\Documentation\foundry-agent-details.png

## 2. Setup sample code

I then setup the sample code

- The request is sent to https://<your foundry>>.services.ai.azure.com/api/projects/<your project>

- Authentication in my case is using Entra Azure CLI credential but in real world id use azure default credential and if you deploy it will pick up managed identity

## 3. Sending custom headers

I added code to send the custom headers by working with the HTTPClient directly
They get sent but because the requests do not get logged they dont appear in app insights

## What gets logged

- Dependencies get logged in app insights by foundry

- Traces and Requests do not get logged

## Example Logs

### Dependencies calling Foundry to Trigger the agent

This pic shows it 
10.foundry.agent.service.via.agent.sdk\Documentation\appinsights-dependencies.png

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
    duration,
    itemId
| order by timestamp desc
```

## Outcome

What I can do
I am able to call the agent in foundry agent service
I am calling foundry directly and not via APIM in this case
I am able to get the logs setup and showing the agent calls for tokens

Whats missing

- Any custom headers so i can see who did what
- I could probably do some client side logging to mitigate this
or 
- I will now look at APIM to centralize this
