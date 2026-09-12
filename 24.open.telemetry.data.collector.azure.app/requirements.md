Id like to do a presentation here like our other presentations

In this case Id like to talk about how we deploy the data collector as a container on azure container apps and then we can avoid deploying the windows service on developer machines.  This allows us to centralize the configuration.

We can then write in the same data collector approach as previous to app insights.

We can take this as an alternative architecture to whats discussed in this folder
20.open.telemetry.data.collector

Can we build a presentation to talk about this in this folder.

## Architecture

- Log Analytics is deployed
- App Insights is deployed pointing to log analytics
- Container Apps Environment hosts container app
- OTEL Data Collector is deployed in a container in the container app
- OTEL data collector is pointing to App Insights
- Client VS code is pointing to the container app for the OTEL data collector endpoint

As the user uses VS code the telemetry streams into app insights via the otel data collector