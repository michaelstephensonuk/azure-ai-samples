
## What is it

- Send vs code AI usage to app insights via open telemetry
- Understand token usage

## How does it work

1. You install the open telemetry data collector service
2. This installs a windows service on your dev machine
3. The windows service contains a component called the azure monitor data explorer
4. You update the configuration to include your app insights connection string
    4.1. Dont forget to restart the windows service

5. In VS code you can set the github copilot and claude code settings so that when you use AI features it sends open telemetry to the open telemetry data collector over a local http call to the windows service

6. The windows service then sends the telemetry to application insights using the azure monitor exporter

7. You can then use data stored in the dependencies, trace, genAIContent and customMetrics tables to help you understand your data

8. You can then use KQL in dashboards such as azure workbooks, grafana and turbo360 to visualize and monitor your queries

## Why

Monitor, manage and optimize token utilization