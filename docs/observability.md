# Observability

This document describes the observability strategy, including structured logging, metrics, and monitoring tools.

## Overview

Observability is essential to understand system behavior in production. The project implements structured logging, metrics collection, and tracing capabilities to facilitate debugging, monitoring, and performance analysis.

## Structured Logging

### Log Structure

Logs are structured in JSON format to facilitate parsing and analysis:

```json
{
  "timestamp": "2024-01-15T10:30:00Z",
  "level": "Information",
  "message": "File processing started",
  "correlationId": "550e8400-e29b-41d4-a716-446655440000",
  "fileId": "660e8400-e29b-41d4-a716-446655440001",
  "properties": {
    "fileName": "cnab_file.txt",
    "fileSize": 1024
  }
}
```

### Log Levels

- **Trace**: Detailed diagnostic information
- **Debug**: Debugging information
- **Information**: General information (default)
- **Warning**: Warnings (non-critical anomalies)
- **Error**: Errors (operations failed)
- **Fatal**: Critical errors (system may be unstable)

### Correlation IDs

Each request receives a unique correlation ID propagated across all components:

- **Generation**: At the entry point (API or Lambda)
- **Propagation**: Across all subsequent calls
- **Tracing**: Enables tracking a request through the system

### Rich Context

Logs include relevant context:

- **Correlation ID**: For tracking
- **User ID**: User identification (if authenticated)
- **Request ID**: Request identification
- **File ID**: File identification (when applicable)
- **Timing**: Timestamps for performance analysis

### Implementation Example

```csharp
_logger.LogInformation(
    "File processing started",
    new { FileId = fileId, FileName = fileName, FileSize = fileSize }
);
```

## Metrics

### Collected Metrics

#### Processing Metrics

- **Processing Duration**: File processing time
- **Processing Success Rate**: Processing success rate
- **Processing Error Rate**: Processing error rate
- **Files Processed**: Count of processed files
- **Files Rejected**: Count of rejected files

#### System Metrics

- **Request Duration**: HTTP request duration
- **Request Count**: Request count
- **Error Count**: Error count
- **Queue Depth**: SQS queue depth
- **DLQ Depth**: DLQ depth

#### Database Metrics

- **Query Duration**: Query duration
- **Connection Pool Size**: Connection pool size
- **Transaction Count**: Transaction count

### Metrics Format

Metrics follow the Prometheus format:

```
# HELP file_processing_duration_seconds Duration of file processing
# TYPE file_processing_duration_seconds histogram
file_processing_duration_seconds_bucket{le="0.5"} 10
file_processing_duration_seconds_bucket{le="1.0"} 25
file_processing_duration_seconds_bucket{le="+Inf"} 50
file_processing_duration_seconds_sum 45.2
file_processing_duration_seconds_count 50
```

## Prometheus

### Configuration

Prometheus is used for metrics collection and storage:

- **Collection**: Scraping metrics endpoints
- **Storage**: Time-series database
- **Query Language**: PromQL for queries

### Metrics Endpoints

- **Backend API**: `/metrics` (Prometheus format)
- **Lambda**: Metrics via CloudWatch (production)

### Configuration Example

```yaml
# prometheus.yml
scrape_configs:
  - job_name: 'cnab-api'
    scrape_interval: 15s
    static_configs:
      - targets: ['localhost:5001']
```

## Grafana

### Dashboards

Grafana dashboards provide metrics visualization:

#### Processing Dashboard

- Processing success rate
- Processing duration
- Counters for processed/rejected files
- Error rate over time

#### System Dashboard

- Requests per second
- Request duration
- Error rate
- Queue depth

#### Database Dashboard

- Queries per second
- Query duration
- Connection pool size
- Connection error rate

### Query Example

```promql
# Processing success rate (last 5 minutes)
rate(files_processed_total{status="success"}[5m]) / rate(files_processed_total[5m])
```

## Lambda Limitations

### Challenges

- **Logs**: CloudWatch Logs (not Prometheus directly)
- **Metrics**: CloudWatch Metrics (different format)
- **Correlation**: Correlation ID propagation
- **Cost**: CloudWatch can be expensive at high volume

### Solutions

- **Structured Logs**: JSON format for parsing
- **Custom Metrics**: CloudWatch Custom Metrics
- **Sampling**: Reduce log volume when necessary
- **Aggregation**: Aggregate metrics before sending

### In Production

- **CloudWatch Logs Insights**: For log analysis
- **CloudWatch Metrics**: For metrics
- **X-Ray**: For distributed tracing (optional)
- **Cost Optimization**: Configure retention and sampling

## How It Looks in Real Production

### AWS Managed Services

#### Logging

- **CloudWatch Logs**: Centralized storage
- **CloudWatch Logs Insights**: Log analysis
- **Kinesis Firehose**: Log streaming for analysis
- **S3**: Archiving old logs

#### Metrics

- **CloudWatch Metrics**: Native metrics
- **CloudWatch Custom Metrics**: Custom metrics
- **CloudWatch Alarms**: Metric-based alerts
- **CloudWatch Dashboards**: Visualization

#### Tracing

- **AWS X-Ray**: Distributed tracing
- **Service Map**: Dependency visualization
- **Trace Analysis**: Trace analysis

### Prometheus/Grafana Integration

- **CloudWatch Exporter**: Export CloudWatch metrics to Prometheus
- **Managed Prometheus**: AWS Managed Service for Prometheus
- **Managed Grafana**: AWS Managed Service for Grafana

### Advanced Observability

- **APM Tools**: New Relic, Datadog, etc.
- **Log Aggregation**: ELK Stack, Splunk
- **Real User Monitoring**: Frontend RUM

## Alerts

### Configured Alerts

- **High Error Rate**: > 5% errors
- **High Queue Depth**: > 1000 messages
- **High DLQ Depth**: > 10 messages
- **High Processing Duration**: > 5 minutes
- **Low Success Rate**: < 95%

### Notification Channels

- Email
- Slack
- PagerDuty (for critical alerts)

## Best Practices

### Logging

1. **Use structured logging**: JSON format
2. **Include correlation IDs**: For tracking
3. **Log at appropriate levels**: Avoid over/under-logging
4. **Avoid sensitive data**: Do not log passwords, tokens, etc.
5. **Use context**: Add relevant context

### Metrics

1. **Measure what matters**: Business metrics, not only technical
2. **Use appropriate types**: Counter, Gauge, Histogram
3. **Label correctly**: Labels for filtering and aggregation
4. **Avoid high cardinality**: Do not create too many time series

### Monitoring

1. **Set up dashboards**: Proactive visualization
2. **Configure alerts**: Problem detection
3. **Review regularly**: Periodic analysis
4. **Optimize costs**: Retention and sampling

## Conclusion

The observability strategy provides full system visibility, enabling efficient debugging, proactive monitoring, and performance analysis. In production, AWS managed services replace local tools while keeping the same principles and standards.
