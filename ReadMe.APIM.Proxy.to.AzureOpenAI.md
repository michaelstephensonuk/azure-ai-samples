

1) Create Language Model API

2) Add settings to point to AI endpoint

3) Let APIM build the proxy API

4) Modify the inbound policy for all API's

- Make sure to remove any inbound api key or subscription key so they arent sent beyond APIM.  Note that the APIM backend will set the API Key for Open AI

- Dynamically handle the re-write to the correct model

```
<policies>
    <inbound>
        <base />

        <set-header 
            name="api-key" 
            exists-action="delete" />

        <set-header 
            name="Ocp-Apim-Subscription-Key" 
            exists-action="delete" />
        
        <set-backend-service 
            id="apim-generated-policy"                     
            backend-id="sample-hello-world-azure-open-ai-openai-endpoint" />
        
        <!-- Dynamically prepend the Azure OpenAI deployment path to whatever
         operation path came in (e.g. /chat/completions, /embeddings, etc.)
         context.Api.Path  = the APIM API base path  e.g. /my-openai-api
         context.Request.Url.Path = full incoming path e.g. /my-openai-api/chat/completions
         suffix (after stripping API base) = /chat/completions
         result = /openai/deployments/gpt-4o/chat/completions               
        -->
        <rewrite-uri template="@{
        var apiBase   = context.Api.Path;
        var fullPath  = context.Request.Url.Path;
        var suffix    = fullPath.StartsWith(apiBase)
                            ? fullPath.Substring(apiBase.Length)
                            : fullPath;
        return "/openai/deployments/{{AzureOpenAI-DeploymentName}}/" + suffix;
    }" />

        <set-query-parameter name="api-version" exists-action="override">
            <value>2025-01-01-preview</value>
        </set-query-parameter>
    
        <llm-emit-token-metric>
            <dimension name="Subscription ID" />
            <dimension name="Product ID" />
        </llm-emit-token-metric>
    
        <llm-token-limit 
            remaining-quota-tokens-header-name="remaining-tokens" remaining-tokens-header-name="remaining-tokens" 
            tokens-per-minute="10000" 
            token-quota="1000000" 
            token-quota-period="Hourly" 
            counter-key="@(context.Subscription.Id)" estimate-prompt-tokens="true" tokens-consumed-header-name="consumed-tokens" />
    </inbound>
    <backend>
        <base />
    </backend>
    <outbound>
        <base />
    </outbound>
    <on-error>
        <base />
    </on-error>
</policies>
```