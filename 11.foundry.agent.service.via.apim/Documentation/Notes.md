
## Aim

Following the previous sample I now want to look at making the call to Foundry via APIM
This will allow me to do request logging centrally server side

Ill use the same C# code as before but we will change it to talk via APIM
I will also change the authentication

Console App --> APIM = Subscription Key
APIM --> Foundry = Managed Identity

To do this we will need to modify the code

The managed identity for APIM will have the "Foundry Agent Consumer" RBAC role which I have already setup

## 1. How I linked APIM to Foundry

1. Create an App Insights
    - I used OTEL support with a managed workspace
2. Create an APIM (i used consumption)
3. In foundry go to the AI Gateway and link it to the APIM

Pic
11.foundry.agent.service.via.apim\Documentation\foundry-apim-gateway-link.png

## To convert to APIM

- Change URL
- Change code

### Using Entra Auth

- You can add a jwt validation policy in APIM and then use managed identity of APIM downstream

- This is easier

### If you want to us API Key

- When linking APIM and Foundry it setup the API for me
- It changed the default from ocp-apim-subscription-key to api-key for that api
- If your using the agent project sdk then you will need to handle the auth being different
    - Remove the AzureDefaultCredential()
    - Add API key as a header via the ApiKeyAuthenticationPolicy or a custom header on the http client
    - Add a custom nullable token provider as you need one but 

## Fixes in APIM

There were a couple of bits in APIM which the foundry link didnt seem to setup

1) In the policy for the API i added a set of the backend.  The backend was already setup by foundry

```
<policies>
    <!-- Throttle, authorize, validate, cache, or transform the requests -->
    <inbound>
        <base />
        <set-backend-service backend-id="ms-blog-ai-samples-foundry" />
    </inbound>
    <!-- Control if and how the requests are forwarded to services  -->
    <backend>
        <base />
    </backend>
    <!-- Customize the responses -->
    <outbound>
        <base />
    </outbound>
    <!-- Handle exceptions and customize error responses  -->
    <on-error>
        <base />
    </on-error>
</policies>
```

2. App Insights Settings

I needed to add the headers and payload logging

PIC
11.foundry.agent.service.via.apim\Documentation\apim-app-insights.png

We need to think about what a standard set of headers we want to log might look like

## Running the sample

Ill now show a quick demo

## Summarizing the Logging

#### I want to make sure my requests are logged

- Turn on the App Insights for the logging in the API in APIM

## I want to log custom headers

- Add the raw http client 
- Add default headers at the transport level
- Make sure APIM is logging the headers
- You will not see the headers in foundry
- You will see the headers in the dependencies log but only on the post to foundry NOT
on the subsequent agent and tool telemetry
- you will see the headers in the requests log

## Correlation

In order to support custom headers you are going to need to link your requests to your dependencies.

You will need to log message bodies because the agent tools log out of process of the request.  You will get the call to foundry but the downstream agent bits need 

gen_ai.response.id on the dependency mapped to response body id

## Link Requests and Dependencies
- You can link a request to a header on the operation id
- To search both `requests` and `dependencies` for the same response id:

```kql
let ai_response_id = "resp_0df0e24f0a7a5836006aa58d84fe988193b76b0ed6aea5f0dd";
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

## Just Requests

The below query would get my requests to APIM for my agents

```
requests
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
    agent_version_ = tostring(parse_json(tostring(parse_json(tostring(customDimensions.["Request-Body"])).agent_reference)).version)
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
    itemId
| order by timestamp desc
```

## Dependencies calling Foundry to Trigger the agent

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

I was able to get the whole scenario working which is great

Its a bit more involved than id ideally like it to be

I am not sure everyone will like the body logging to get the id to correlate
Maybe we can use policy to extract the response id and promote as a header as a standard pattern.

I think the standard set of headers to log might be challenging for different projects

I think that you may end up with multiple API's for different user scenarios like presentation API's so each team can customize but also have some standards.  

I think its good we can centralize 

I think there will be some thinking by teams about distributed correlation across processes.  When the agent runs downstream i can correlate with the response id but it would make sense to flow standard correlation headers if possible eg traceParent and x-ms-correlation-id and so on