using Microsoft.Extensions.Logging;
using Moq;

namespace TransactionProcessor.Tests.Integration.Services;

/// <summary>
/// Helper extensions for verifying logger calls in tests.
/// Provides fluent API for asserting logging behavior.
/// </summary>
public static class MockLoggerExtensions
{
    /// <summary>
    /// Verify that logger was called at least once.
    /// </summary>
    public static void VerifyLoggingOccurred<T>(
        this Mock<ILogger<T>> loggerMock,
        LogLevel logLevel = LogLevel.Information) where T : class
    {
        loggerMock.Verify(
            x => x.Log(
                logLevel,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce,
            $"Expected at least one log call with level {logLevel}");
    }

    /// <summary>
    /// Verify that a specific log message was written.
    /// </summary>
    public static void VerifyLogContains<T>(
        this Mock<ILogger<T>> loggerMock,
        string expectedMessage,
        LogLevel logLevel = LogLevel.Information) where T : class
    {
        loggerMock.Verify(
            x => x.Log(
                logLevel,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce,
            $"Expected logger to contain message: {expectedMessage}");
    }

    /// <summary>
    /// Verify that logger was called at a specific level exactly N times.
    /// </summary>
    public static void VerifyLoggingCalled<T>(
        this Mock<ILogger<T>> loggerMock,
        LogLevel logLevel,
        Times times) where T : class
    {
        loggerMock.Verify(
            x => x.Log(
                logLevel,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times,
            "Expected logger call with specified level");
    }
}
