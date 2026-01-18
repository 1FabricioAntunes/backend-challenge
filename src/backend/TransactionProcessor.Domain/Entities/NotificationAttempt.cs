using System.ComponentModel.DataAnnotations;

namespace TransactionProcessor.Domain.Entities;

/// <summary>
/// Tracks notification delivery attempts for idempotency and retry logic.
/// </summary>
public class NotificationAttempt
{
    /// <summary>
    /// Primary key identifier for the notification attempt.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Related file identifier (FK to Files).
    /// </summary>
    public Guid FileId { get; set; }

    /// <summary>
    /// Type of notification: "ProcessingCompleted" or "ProcessingFailed".
    /// </summary>
    [MaxLength(50)]
    public string NotificationType { get; set; } = string.Empty;

    /// <summary>
    /// Recipient address (email) or webhook URL.
    /// </summary>
    [MaxLength(500)]
    public string Recipient { get; set; } = string.Empty;

    /// <summary>
    /// Current status: "Sent", "Failed", or "Retrying".
    /// </summary>
    [MaxLength(50)]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Number of attempts performed.
    /// </summary>
    public int AttemptCount { get; set; }

    /// <summary>
    /// Timestamp of the most recent attempt (UTC).
    /// </summary>
    public DateTime LastAttemptAt { get; set; }

    /// <summary>
    /// Error message from the last failed attempt (if any).
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Timestamp when the notification was successfully sent (UTC), if applicable.
    /// </summary>
    public DateTime? SentAt { get; set; }
}
