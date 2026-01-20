using System;
using FluentAssertions;
using TransactionProcessor.Application.Models;
using TransactionProcessor.Application.Services;
using Xunit;

namespace TransactionProcessor.Domain.UnitTests;

public class CNABValidatorTests
{
    private readonly CNABValidator _validator = new();

    [Fact]
    public void ValidateRecord_ValidRecord_ReturnsValid()
    {
        var record = CreateValidRecord();

        var result = _validator.ValidateRecord(record, lineNumber: 1);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateRecord_InvalidType_ReturnsError()
    {
        var record = CreateValidRecord();
        record.Type = 0;

        var result = _validator.ValidateRecord(record, lineNumber: 2);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("transaction type"));
    }

    [Fact]
    public void ValidateRecord_AmountMustBeGreaterThanZero()
    {
        var record = CreateValidRecord();
        record.Amount = 0;

        var result = _validator.ValidateRecord(record, lineNumber: 3);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("greater than 0"));
    }

    [Fact]
    public void ValidateRecord_DateCannotBeInFarFuture()
    {
        var record = CreateValidRecord();
        record.Date = DateTime.UtcNow.AddYears(2);

        var result = _validator.ValidateRecord(record, lineNumber: 4);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("date cannot be in the future"));
    }

    private static CNABLineData CreateValidRecord()
    {
        return new CNABLineData
        {
            Type = 1,
            Date = DateTime.UtcNow.Date,
            Amount = 100,
            CPF = "12345678901",
            Card = "123456789012",
            Time = new TimeSpan(10, 0, 0),
            StoreOwner = "OWNER",
            StoreName = "STORE"
        };
    }
}
