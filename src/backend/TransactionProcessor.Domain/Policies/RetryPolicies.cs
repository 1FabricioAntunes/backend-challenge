using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace TransactionProcessor.Domain.Policies;

/// <summary>
/// Static class containing resilience policies for notification service operations.
/// 
/// These policies implement retry and circuit breaker patterns to handle transient failures
/// in notification delivery, ensuring robust handling of network issues and service disruptions.
/// 
/// Policy Strategy:
/// 1. Exponential Backoff Retry: Handles transient failures with increasing delays
/// 2. Circuit Breaker: Prevents cascading failures by breaking after repeated failures
/// 
/// Usage Context:
/// These policies are designed for notification service operations where:
/// - Transient network failures may occur (timeouts, temporary unavailability)
/// - Repeated failures should trigger a circuit break to prevent resource exhaustion
/// - Graceful degradation is preferred over aggressive retry loops
/// 
/// Design Rationale:
/// - Exponential backoff reduces load on failing services during recovery
/// - Circuit breaker prevents thundering herd problem (multiple clients retrying simultaneously)
/// - Combined use provides defense-in-depth resilience strategy
/// 
/// Reference: technical-decisions.md § 6 Notification Service Resilience
/// Reference: docs/async-processing.md § Notification Delivery & Retry Strategy
/// 
/// Polly Documentation: https://github.com/App-vNext/Polly
/// </summary>
public static class RetryPolicies
{
    /// <summary>
    /// Exponential backoff retry policy for transient failures.
    /// 
    /// Configuration:
    /// - Max Retries: 3 attempts
    /// - Retry Delays: 2s, 4s, 8s (2^attempt seconds)
    /// - Retry On: All exceptions (can be customized by caller)
    /// - Total Max Duration: 2 + 4 + 8 = 14 seconds
    /// 
    /// Behavior:
    /// 1. First attempt: fails
    /// 2. Wait 2 seconds (2^1)
    /// 3. Retry attempt 1: fails
    /// 4. Wait 4 seconds (2^2)
    /// 5. Retry attempt 2: fails
    /// 6. Wait 8 seconds (2^3)
    /// 7. Retry attempt 3: fails
    /// 8. Throw exception (no more retries)
    /// 
    /// Use Cases:
    /// - Email delivery timeouts (SMTP server slow to respond)
    /// - Network connection issues (temporary network congestion)
    /// - Transient service unavailability (service starting up)
    /// - Database connection pools temporarily exhausted
    /// 
    /// Example Usage:
    /// 
    /// var policy = RetryPolicies.GetExponentialBackoffRetryPolicy();
    /// await policy.ExecuteAsync(async () =>
    /// {
    ///     await emailService.SendAsync(notification.Email);
    /// });
    /// 
    /// Example Outcomes:
    /// 
    /// Scenario 1: Success on First Try
    ///   Email send succeeds immediately
    ///   Result: Email sent, no retries needed
    /// 
    /// Scenario 2: Transient Failure, Success on First Retry
    ///   First attempt times out (wait 2s)
    ///   Second attempt succeeds
    ///   Result: Email sent after 2 second delay
    /// 
    /// Scenario 3: Sustained Failure
    ///   All attempts fail (wait 2s, 4s, 8s between attempts)
    ///   After 3 retries, exception is thrown
    ///   Result: Notification sent to DLQ for manual review
    /// 
    /// Performance Impact:
    /// - Best case: Immediate execution (no delays)
    /// - Worst case: ~14 second total duration (2 + 4 + 8 seconds waiting)
    /// - Memory: Minimal overhead (stateless policy)
    /// 
    /// </summary>
    /// <returns>
    /// IAsyncPolicy that executes with exponential backoff retry on all exceptions.
    /// Use ExecuteAsync() to invoke the policy with your notification operation.
    /// </returns>
    /// <remarks>
    /// Important: This policy is not wrapped with a circuit breaker.
    /// For better resilience, consider using GetNotificationResiliencePolicy()
    /// which combines exponential backoff with circuit breaker protection.
    /// 
    /// The retry delays follow the formula: delay = 2^attempt seconds
    /// - Attempt 1 failure: wait 2 seconds
    /// - Attempt 2 failure: wait 4 seconds
    /// - Attempt 3 failure: wait 8 seconds
    /// 
    /// Thread Safety: Polly policies are thread-safe and can be reused
    /// across multiple concurrent operations.
    /// 
    /// See: GetNotificationResiliencePolicy for combined retry + circuit breaker
    /// </remarks>
    public static IAsyncPolicy GetExponentialBackoffRetryPolicy()
    {
        // Exponential backoff: 2^attempt seconds delay
        // Attempt 1: 2^1 = 2 seconds
        // Attempt 2: 2^2 = 4 seconds
        // Attempt 3: 2^3 = 8 seconds
        return Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    // Optional: Log retry attempt
                    var correlationId = context.ContainsKey("CorrelationId")
                        ? context["CorrelationId"]
                        : "unknown";

                    System.Diagnostics.Debug.WriteLine(
                        $"[{correlationId}] Retry {retryCount} after {timespan.TotalSeconds}s delay. " +
                        $"Exception: {outcome.Exception?.Message}");
                });
    }

    /// <summary>
    /// Circuit breaker policy for cascading failure prevention.
    /// 
    /// Configuration:
    /// - Failure Threshold: 5 consecutive failures
    /// - Break Duration: 30 seconds
    /// - Behavior: Fails fast without attempting operation when circuit is open
    /// 
    /// Circuit States:
    /// 1. Closed (Normal Operation):
    ///    - Requests proceed normally
    ///    - Failures are tracked
    ///    - After 5 consecutive failures, transitions to Open
    /// 
    /// 2. Open (Failing):
    ///    - Requests fail immediately without execution
    ///    - No calls made to failing service
    ///    - After 30 seconds, transitions to Half-Open for recovery test
    /// 
    /// 3. Half-Open (Testing Recovery):
    ///    - Single request is allowed to test if service recovered
    ///    - If succeeds: transitions back to Closed
    ///    - If fails: transitions back to Open with another 30-second wait
    /// 
    /// Use Cases:
    /// - Prevent overwhelming failing email service with requests
    /// - Avoid connection pool exhaustion during service outages
    /// - Reduce latency by failing fast instead of waiting for timeouts
    /// - Protect downstream systems from cascading failures
    /// 
    /// Example Scenario:
    /// 
    /// Time 0s: SMTP service goes down
    /// Time 1s: Notification 1 fails (failures: 1/5)
    /// Time 2s: Notification 2 fails (failures: 2/5)
    /// Time 3s: Notification 3 fails (failures: 3/5)
    /// Time 4s: Notification 4 fails (failures: 4/5)
    /// Time 5s: Notification 5 fails (failures: 5/5) → Circuit Opens
    /// Time 6s: Notification 6 attempted → BrokenCircuitException (no execution)
    /// Time 10s: Notification 7 attempted → BrokenCircuitException (no execution)
    /// Time 35s: SMTP service recovered → Circuit Half-Opens
    /// Time 36s: Notification 8 attempted → Succeeds → Circuit Closes
    /// Time 37s: Notification 9 attempted → Succeeds (normal operation)
    /// 
    /// Benefits:
    /// - Reduces resource waste (no calls to failing service)
    /// - Improves system responsiveness (fast failures instead of timeouts)
    /// - Allows failing service time to recover (30-second break)
    /// - Prevents cascading failures across the system
    /// 
    /// </summary>
    /// <returns>
    /// IAsyncPolicy that breaks circuit after 5 consecutive failures for 30 seconds.
    /// Throws BrokenCircuitException when circuit is open.
    /// </returns>
    /// <remarks>
    /// Important: This policy fails fast without retrying.
    /// For comprehensive resilience, combine with exponential backoff retry.
    /// Use GetNotificationResiliencePolicy() for both retry and circuit breaker.
    /// 
    /// Configuration Details:
    /// - Failure Threshold: Configurable via handledEventsAllowedBeforeBreaking parameter
    ///   Currently set to 5 consecutive failures
    /// - Break Duration: Configurable via durationOfBreak parameter
    ///   Currently set to 30 seconds
    /// - Failure Criteria: All exceptions (can be filtered by caller)
    /// 
    /// Circuit Breaker Exception:
    /// When circuit is Open, invoking the policy throws BrokenCircuitException:
    /// 
    /// try
    /// {
    ///     await policy.ExecuteAsync(operation);
    /// }
    /// catch (BrokenCircuitException ex)
    /// {
    ///     // Circuit is open, service is unavailable
    ///     // Send notification to DLQ for later retry
    /// }
    /// 
    /// Thread Safety: Polly policies are thread-safe.
    /// The circuit state is shared across all threads using the same policy instance.
    /// 
    /// See: GetNotificationResiliencePolicy for combined retry + circuit breaker
    /// </remarks>
    public static IAsyncPolicy GetCircuitBreakerPolicy()
    {
        // Break circuit after 5 consecutive failures for 30 seconds
        // During break period, all requests fail immediately with BrokenCircuitException
        return Policy
            .Handle<Exception>()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (outcome, breakDuration, context) =>
                {
                    // Optional: Log circuit break event
                    var correlationId = context.ContainsKey("CorrelationId")
                        ? context["CorrelationId"]
                        : "unknown";

                    System.Diagnostics.Debug.WriteLine(
                        $"[{correlationId}] Circuit breaker opened. " +
                        $"Breaking for {breakDuration.TotalSeconds}s. " +
                        $"Reason: {outcome.Exception?.Message}");
                },
                onReset: (context) =>
                {
                    // Optional: Log circuit reset event
                    var correlationId = context.ContainsKey("CorrelationId")
                        ? context["CorrelationId"]
                        : "unknown";

                    System.Diagnostics.Debug.WriteLine(
                        $"[{correlationId}] Circuit breaker reset. Service recovered.");
                });
    }

    /// <summary>
    /// Combined resilience policy with exponential backoff retry AND circuit breaker.
    /// 
    /// This is the recommended policy for notification service operations.
    /// It provides comprehensive protection against transient and sustained failures.
    /// 
    /// Policy Composition (in order):
    /// 1. Circuit Breaker (outermost):
    ///    - Breaks circuit after 5 consecutive failures
    ///    - Fails fast for 30 seconds when circuit is open
    /// 
    /// 2. Exponential Backoff Retry (innermost):
    ///    - Retries 3 times with increasing delays (2s, 4s, 8s)
    ///    - Applied only when circuit is closed
    /// 
    /// Combined Behavior:
    /// 
    /// Scenario 1: Transient Failure (Circuit Closed)
    ///   Request fails
    ///   → Retry with 2s delay
    ///   → Succeeds
    ///   Result: Message delivered after ~2 second delay
    /// 
    /// Scenario 2: Sustained Failure (Circuit Closed, All Retries Exhausted)
    ///   Request fails (attempt 1)
    ///   → Retry with 2s delay (attempt 2)
    ///   → Fails
    ///   → Retry with 4s delay (attempt 3)
    ///   → Fails
    ///   → Retry with 8s delay (attempt 4)
    ///   → Fails
    ///   Failures count: 4 of 5 before circuit breaks
    ///   Result: Exception thrown, notification sent to DLQ
    /// 
    /// Scenario 3: Cascading Failure (Circuit Open)
    ///   Circuit breaker opens after 5 failures
    ///   Next request fails immediately with BrokenCircuitException
    ///   No retries attempted (circuit is open)
    ///   Result: Fast failure, notification sent to DLQ
    ///   (After 30 seconds, circuit half-opens for recovery test)
    /// 
    /// Resilience Flow:
    /// 
    ///                    ┌─────────────────────────────┐
    ///                    │   Notification Service      │
    ///                    │  Invokes Policy.ExecuteAsync│
    ///                    └──────────────┬──────────────┘
    ///                                   │
    ///                    ┌──────────────▼──────────────┐
    ///                    │   Circuit Breaker Wrapper   │
    ///                    │  (5 failures → 30s break)   │
    ///                    └──────────────┬──────────────┘
    ///                                   │
    ///                     ┌─────────────▼─────────────┐
    ///                     │ Exponential Backoff Retry  │
    ///                     │  (3 retries: 2s,4s,8s)    │
    ///                     └─────────────┬─────────────┘
    ///                                   │
    ///                    ┌──────────────▼──────────────┐
    ///                    │  Email Service / Webhook    │
    ///                    │     (Actual Operation)      │
    ///                    └─────────────────────────────┘
    /// 
    /// Usage Example:
    /// 
    /// var policy = RetryPolicies.GetNotificationResiliencePolicy();
    /// 
    /// try
    /// {
    ///     await policy.ExecuteAsync(async () =>
    ///     {
    ///         await emailService.SendAsync(notification.Email);
    ///     });
    ///     // Notification sent successfully
    /// }
    /// catch (BrokenCircuitException)
    /// {
    ///     // Service is temporarily down, circuit is open
    ///     // Send to notification DLQ for later retry
    ///     await notificationDlqService.PublishAsync(notification);
    /// }
    /// catch (Exception ex)
    ///     // Other failures (business logic errors, etc.)
    ///     // Send to notification DLQ for investigation
    ///     logger.LogError($"Notification failed: {ex.Message}");
    ///     await notificationDlqService.PublishAsync(notification);
    /// }
    /// 
    /// </summary>
    /// <returns>
    /// IAsyncPolicy that combines circuit breaker and exponential backoff retry.
    /// Provides comprehensive resilience for notification operations.
    /// </returns>
    /// <remarks>
    /// Policy Composition Order:
    /// The circuit breaker is placed OUTSIDE the retry policy.
    /// This ensures:
    /// - Circuit breaker counts all failure attempts (including retries)
    /// - When circuit is open, retries are never attempted (fail fast)
    /// - More aggressive circuit break when service is truly failing
    /// 
    /// Recommended Usage:
    /// This is the standard policy for all notification service operations.
    /// It provides optimal balance between resilience and fast failure.
    /// 
    /// Performance Characteristics:
    /// - Best case: Immediate (no failures)
    /// - Transient failure: ~2-14 seconds (depending on retry count)
    /// - Sustained failure (circuit open): ~1 millisecond (fast fail)
    /// 
    /// Monitoring:
    /// The policy provides callback hooks for logging:
    /// - OnRetry: Logs retry attempts
    /// - OnBreak: Logs when circuit opens
    /// - OnReset: Logs when circuit recovers
    /// 
    /// Use these to track notification health and service availability.
    /// 
    /// See: technical-decisions.md § 6 for complete resilience strategy
    /// </remarks>
    public static IAsyncPolicy GetNotificationResiliencePolicy()
    {
        // Build policy: Circuit Breaker wrapping Exponential Backoff Retry
        // This order ensures circuit breaker protects against retry storms
        var retryPolicy = GetExponentialBackoffRetryPolicy();
        var circuitBreakerPolicy = GetCircuitBreakerPolicy();

        // Combine policies: circuit breaker (outer) + retry (inner)
        return Policy.WrapAsync(circuitBreakerPolicy, retryPolicy);
    }
}
