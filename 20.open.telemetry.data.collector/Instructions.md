




## Setup

### Install the Open Telemetry Data Collector

Run the below command to download and install the collector

```
msiexec /i "https://github.com/open-telemetry/opentelemetry-collector-releases/releases/download/v0.157.0/otelcol-contrib_0.157.0_windows_x64.msi"

```

Notes:
- We are using the contrib version as it also includes the azure monitor exporter out of the box

- Get it from:
https://opentelemetry.io/docs/collector/install/binary/windows/

- This includes the azure monitor data collector from this url 
https://opentelemetry.io/docs/collector/components/exporter/


### Modify the YAML for the data collector to add azure monitor exporter

1. GOTO this location --> C:\Program Files\OpenTelemetry Collector Contrib\
C:\Program Files\OpenTelemetry Collector

2. Find file --> config.yaml

3. Add this bit to the config for the collectors
```
exporters:
  azuremonitor:
    connection_string: "InstrumentationKey=<YOUR-KEY>;IngestionEndpoint=https://<region>.in.applicationinsights.azure.com/;LiveEndpoint=https://<region>.livediagnostics.monitor.azure.com/"
```

4. Add the link from pipelines to the azure monitor exporter

```
 pipelines:

    traces:
      receivers: [otlp, jaeger, zipkin]
      processors: [batch]
      exporters: [debug, azuremonitor]

    metrics:
      receivers: [otlp, prometheus]
      processors: [batch]
      exporters: [debug, azuremonitor]

    logs:
      receivers: [otlp]
      processors: [batch]
      exporters: [debug, azuremonitor]
```

Notes:

- You might want to remove the debug exporter as this adds lots of events to your event viewer

The full YAML looks like this

```
# To limit exposure to denial of service attacks, change the host in endpoints below from 0.0.0.0 to a specific network interface.
# See https://github.com/open-telemetry/opentelemetry-collector/blob/main/docs/security-best-practices.md#safeguards-against-denial-of-service-attacks

extensions:
  health_check:
  pprof:
    endpoint: 0.0.0.0:1777
  zpages:
    endpoint: 0.0.0.0:55679

receivers:
  otlp:
    protocols:
      grpc:
        endpoint: 0.0.0.0:4317
      http:
        endpoint: 0.0.0.0:4318

  # Collect own metrics
  prometheus:
    config:
      scrape_configs:
      - job_name: 'otel-collector'
        scrape_interval: 10s
        static_configs:
        - targets: ['127.0.0.1:8888']

  jaeger:
    protocols:
      grpc:
        endpoint: 0.0.0.0:14250
      thrift_binary:
        endpoint: 0.0.0.0:6832
      thrift_compact:
        endpoint: 0.0.0.0:6831
      thrift_http:
        endpoint: 0.0.0.0:14268

  zipkin:
    endpoint: 0.0.0.0:9411

processors:
  batch:

exporters:
  #debug:
  #  verbosity: detailed
  azuremonitor:
    connection_string: "InstrumentationKey=63a82e54-1d6b-41b9-9844-7d01b0e7bca9;IngestionEndpoint=https://uksouth-1.in.applicationinsights.azure.com/;LiveEndpoint=https://uksouth.livediagnostics.monitor.azure.com/"
    maxbatchsize: 1024
    maxbatchinterval: 10s
    spaneventsenabled: false

service:
  telemetry:
    metrics:
      readers:
        - pull:
            exporter:
              prometheus:
                host: '127.0.0.1'
                port: 8888
                without_scope_info: true
                without_type_suffix: true
                without_units: true
                with_resource_constant_labels:
                  included: [ ]

  pipelines:

    traces:
      receivers: [otlp, jaeger, zipkin]
      processors: [batch]
      exporters: [azuremonitor]

    metrics:
      receivers: [otlp, prometheus]
      processors: [batch]
      exporters: [azuremonitor]

    logs:
      receivers: [otlp]
      processors: [batch]
      exporters: [azuremonitor]

  extensions: [health_check, pprof, zpages]
```


### Restart the open telemetry data collector Service

If you go to services.msc and then restart the open telemetry data collector windows service


### Verify Azure Monitor Exporter

## Troubleshoot

This shows you which exporters are registered

http://127.0.0.1:8888/metrics

If you open that url your looking for the azure monitor exporter to be mentioned