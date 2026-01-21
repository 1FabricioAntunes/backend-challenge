using Prometheus;

namespace TransactionProcessor.Infrastructure.Metrics;

/// <summary>
/// Service for managing Prometheus metrics collection across the application.
/// Provides metrics for file processing, HTTP requests, database queries, and system health.
/// </summary>
public class MetricsService
{
    // ========================================================================
    // COUNTERS - Measure total count of events
    // ========================================================================
    
    /// <summary>
    /// Total number of files processed (labeled by status: success, rejected)
    /// </summary>
    public static readonly Counter FilesProcessedTotal = Prometheus.Metrics.CreateCounter(
            name: "files_processed_total",
            help: "Total number of files processed",
            labelNames: new[] { "status" }  // Values: success, rejected
        );

    /// <summary>
    /// Total number of HTTP requests (labeled by method, endpoint, HTTP status)
    /// </summary>
    public static readonly Counter HttpRequestsTotal = Prometheus.Metrics.CreateCounter(
            name: "http_requests_total",
            help: "Total number of HTTP requests",
            new CounterConfiguration
            {
                LabelNames = new[] { "method", "endpoint", "status_code" }
            });

    /// <summary>
    /// Total number of errors (labeled by error type)
    /// </summary>
    public static readonly Counter ErrorsTotal = Prometheus.Metrics.CreateCounter(
            name: "errors_total",
            help: "Total number of errors",
            new CounterConfiguration
            {
                LabelNames = new[] { "error_type" }  // Values: validation, not_found, internal_error, etc.
            });

    /// <summary>
    /// Total number of database operations (labeled by operation type)
    /// </summary>
    public static readonly Counter DatabaseOperationsTotal = Prometheus.Metrics.CreateCounter(
            name: "database_operations_total",
            help: "Total number of database operations",
            new CounterConfiguration
            {
                LabelNames =  new[] { "operation" }  // Values: select, insert, update, delete
            });

    /// <summary>
    /// Total number of messages processed from SQS (labeled by queue and status)
    /// </summary>
    public static readonly Counter SqsMessagesProcessedTotal = Prometheus.Metrics.CreateCounter(
            name: "sqs_messages_processed_total",
            help: "Total number of SQS messages processed",
            new CounterConfiguration
            {
                LabelNames = new[] { "queue", "status" }  // Status: success, failed
            });

    // ========================================================================
    // HISTOGRAMS - Measure distribution of values (latency, duration)
    // ========================================================================
    
    /// <summary>
    /// File processing duration in seconds (with predefined buckets)
    /// Buckets: 0.5s, 1s, 5s, 10s, 30s, 60s
    /// </summary>
    public static readonly Histogram FileProcessingDurationSeconds = Prometheus.Metrics.CreateHistogram(
            name: "file_processing_duration_seconds",
            help: "Duration of file processing in seconds",
            new HistogramConfiguration
            {
                LabelNames = new[] { "file_type" },  // e.g., cnab
                Buckets = new double[] { 0.5, 1.0, 5.0, 10.0, 30.0, 60.0 }
            });

    /// <summary>
    /// HTTP request duration in seconds
    /// Buckets: 0.01s, 0.05s, 0.1s, 0.5s, 1s, 5s
    /// </summary>
    public static readonly Histogram HttpRequestDurationSeconds = Prometheus.Metrics.CreateHistogram(
            name: "http_request_duration_seconds",
            help: "Duration of HTTP requests in seconds",
            new HistogramConfiguration
            {
                LabelNames = new[] { "method", "endpoint", "status_code" },
                Buckets = new double[] { 0.01, 0.05, 0.1, 0.5, 1.0, 5.0 }
            });

    /// <summary>
    /// Database query duration in seconds
    /// Buckets: 0.001s, 0.005s, 0.01s, 0.05s, 0.1s, 1s
    /// </summary>
    public static readonly Histogram DatabaseQueryDurationSeconds = Prometheus.Metrics.CreateHistogram(
            name: "database_query_duration_seconds",
            help: "Duration of database queries in seconds",
            new HistogramConfiguration
            {
                LabelNames = new[] { "operation", "entity" },
                Buckets = new double[] { 0.001, 0.005, 0.01, 0.05, 0.1, 1.0 }
            });

    /// <summary>
    /// SQS message processing duration in seconds
    /// </summary>
    public static readonly Histogram SqsMessageProcessingDurationSeconds = Prometheus.Metrics.CreateHistogram(
            name: "sqs_message_processing_duration_seconds",
            help: "Duration of SQS message processing in seconds",
            new HistogramConfiguration
            {
                LabelNames = new[] { "queue" },
                Buckets = new double[] { 0.1, 0.5, 1.0, 5.0, 10.0, 30.0 }
            });

    /// <summary>
    /// File parsing duration in seconds
    /// </summary>
    public static readonly Histogram FileParsingDurationSeconds = Prometheus.Metrics.CreateHistogram(
            name: "file_parsing_duration_seconds",
            help: "Duration of file parsing in seconds",
            new HistogramConfiguration
            {
                LabelNames = new[] { "file_type" },
                Buckets = new double[] { 0.01, 0.05, 0.1, 0.5, 1.0 }
            });

    /// <summary>
    /// CNAB validation duration in seconds
    /// </summary>
    public static readonly Histogram ValidationDurationSeconds = Prometheus.Metrics.CreateHistogram(
            name: "validation_duration_seconds",
            help: "Duration of validation operations in seconds",
            new HistogramConfiguration
            {
                LabelNames = new[] { "validation_type" },
                Buckets = new double[] { 0.001, 0.005, 0.01, 0.05, 0.1 }
            });

    /// <summary>
    /// SQS operation duration in seconds (publish, receive, delete, dlq_receive)
    /// </summary>
    public static readonly Histogram SQSOperationDurationSeconds = Prometheus.Metrics.CreateHistogram(
            name: "sqs_operation_duration_seconds",
            help: "Duration of SQS operations in seconds",
            new HistogramConfiguration
            {
                LabelNames = new[] { "operation" },  // Values: publish, receive, delete, dlq_receive
                Buckets = new double[] { 0.01, 0.05, 0.1, 0.5, 1.0, 5.0 }
            });

    // ========================================================================
    // GAUGES - Measure instantaneous values (depth, size, count)
    // ========================================================================
    
    /// <summary>
    /// SQS queue depth (approximate number of messages in queue)
    /// </summary>
    public static readonly Gauge SqsQueueDepth = Prometheus.Metrics.CreateGauge(
            name: "sqs_queue_depth",
            help: "Approximate number of messages in SQS queue",
            new GaugeConfiguration
            {
                LabelNames = new[] { "queue" }
            });

    /// <summary>
    /// DLQ (Dead Letter Queue) depth
    /// </summary>
    public static readonly Gauge DlqDepth = Prometheus.Metrics.CreateGauge(
            name: "dlq_depth",
            help: "Approximate number of messages in Dead Letter Queue",
            new GaugeConfiguration
            {
                LabelNames = new[] { "queue" }
            });

    /// <summary>
    /// Database connection pool size
    /// </summary>
    public static readonly Gauge DatabaseConnectionPoolSize = Prometheus.Metrics.CreateGauge(
            name: "database_connection_pool_size",
            help: "Current size of database connection pool",
            new GaugeConfiguration
            {
                LabelNames = new[] { "pool" }
            });

    /// <summary>
    /// Number of active HTTP requests
    /// </summary>
    public static readonly Gauge ActiveHttpRequests = Prometheus.Metrics.CreateGauge(
            name: "active_http_requests",
            help: "Number of currently active HTTP requests",
            new GaugeConfiguration
            {
                LabelNames = new[] { "method", "endpoint" }
            });

    /// <summary>
    /// Number of files in processing queue
    /// </summary>
    public static readonly Gauge FilesInProcessingQueue = Prometheus.Metrics.CreateGauge(
            name: "files_in_processing_queue",
            help: "Number of files in processing queue"
        );

    /// <summary>
    /// Last file processing timestamp
    /// </summary>
    public static readonly Gauge LastFileProcessedTimestamp = Prometheus.Metrics.CreateGauge(
            name: "last_file_processed_timestamp",
            help: "Unix timestamp of last processed file"
        );

    // ========================================================================
    // RECORDING METHODS - Helper methods for metrics recording
    // ========================================================================

    /// <summary>
    /// Record file processing result (success or rejection)
    /// </summary>
    /// <param name="status">Processing status: 'success' or 'rejected'</param>
    public static void RecordFileProcessed(string status)
    {
        FilesProcessedTotal.WithLabels(status).Inc();
        LastFileProcessedTimestamp.Set(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }

    /// <summary>
    /// Record HTTP request metrics
    /// </summary>
    /// <param name="method">HTTP method (GET, POST, etc.)</param>
    /// <param name="endpoint">API endpoint path</param>
    /// <param name="statusCode">HTTP response status code</param>
    public static void RecordHttpRequest(string method, string endpoint, string statusCode)
    {
        HttpRequestsTotal.WithLabels(method, endpoint, statusCode).Inc();
    }

    /// <summary>
    /// Record error occurrence
    /// </summary>
    /// <param name="errorType">Type of error (validation, not_found, internal_error, etc.)</param>
    public static void RecordError(string errorType)
    {
        ErrorsTotal.WithLabels(errorType).Inc();
    }

    /// <summary>
    /// Record database operation
    /// </summary>
    /// <param name="operation">Type of operation (select, insert, update, delete)</param>
    public static void RecordDatabaseOperation(string operation)
    {
        DatabaseOperationsTotal.WithLabels(operation).Inc();
    }

    /// <summary>
    /// Record SQS message processing
    /// </summary>
    /// <param name="queue">Queue name</param>
    /// <param name="status">Processing status (success or failed)</param>
    public static void RecordSqsMessageProcessed(string queue, string status)
    {
        SqsMessagesProcessedTotal.WithLabels(queue, status).Inc();
    }

    /// <summary>
    /// Update queue depth gauge
    /// </summary>
    /// <param name="queue">Queue name</param>
    /// <param name="depth">Current queue depth</param>
    public static void UpdateQueueDepth(string queue, double depth)
    {
        SqsQueueDepth.WithLabels(queue).Set(depth);
    }

    /// <summary>
    /// Update DLQ depth gauge
    /// </summary>
    /// <param name="queue">Queue name</param>
    /// <param name="depth">Current DLQ depth</param>
    public static void UpdateDlqDepth(string queue, double depth)
    {
        DlqDepth.WithLabels(queue).Set(depth);
    }
}
