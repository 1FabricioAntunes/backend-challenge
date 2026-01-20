using System;

namespace TransactionProcessor.Domain.UnitTests.Helpers;

/// <summary>
/// Base class for all unit tests providing common setup and utilities.
/// </summary>
public abstract class TestBase
{
    /// <summary>
    /// Creates a new Guid for testing purposes.
    /// </summary>
    protected static Guid NewGuid() => Guid.NewGuid();

    /// <summary>
    /// Creates a deterministic Guid from a seed for reproducible tests.
    /// </summary>
    protected static Guid DeterministicGuid(int seed)
    {
        var bytes = new byte[16];
        BitConverter.GetBytes(seed).CopyTo(bytes, 0);
        return new Guid(bytes);
    }

    /// <summary>
    /// Gets the current UTC date for testing.
    /// </summary>
    protected static DateTime UtcNow => DateTime.UtcNow;

    /// <summary>
    /// Gets a specific test date (2024-01-15).
    /// </summary>
    protected static DateTime TestDate => new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);

    /// <summary>
    /// Gets a specific test DateOnly (2024-01-15).
    /// </summary>
    protected static DateOnly TestDateOnly => new DateOnly(2024, 1, 15);

    /// <summary>
    /// Gets a specific test TimeOnly (10:30:00).
    /// </summary>
    protected static TimeOnly TestTimeOnly => new TimeOnly(10, 30, 0);
}
