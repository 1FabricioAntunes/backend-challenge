using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using TransactionProcessor.Application.Models;
using TransactionProcessor.Application.Services;
using Xunit;

namespace TransactionProcessor.Domain.UnitTests;

public class CNABParserTests
{
    private readonly CNABParser _parser = new();

    [Fact]
    public async Task ParseAsync_ValidLine_ParsesAllFields()
    {
        var line = BuildLine(
            type: 1,
            date: "20240101",
            amountCents: "0000012345",
            cpf: "12345678901",
            card: "123456789012",
            time: "103000",
            owner: "OWNER NAME",
            store: "STORE MAIN");

        using var stream = new MemoryStream(Encoding.ASCII.GetBytes(line + "\n"));

        var result = await _parser.ParseAsync(stream);

        result.IsValid.Should().BeTrue();
        result.ValidLines.Should().ContainSingle();

        var record = result.ValidLines.Single();
        record.Type.Should().Be(1);
        record.Date.Should().Be(new DateTime(2024, 1, 1));
        record.Amount.Should().Be(12345m);
        record.CPF.Should().Be("12345678901");
        record.Card.Should().Be("123456789012");
        record.Time.Should().Be(new TimeSpan(10, 30, 0));
        record.StoreOwner.Should().Be("OWNER NAME");
        record.StoreName.Should().Be("STORE MAIN");
    }

    [Fact]
    public async Task ParseAsync_InvalidLength_ReturnsError()
    {
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("SHORT\n"));

        var result = await _parser.ParseAsync(stream);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("Invalid length"));
    }

    [Fact]
    public async Task ParseAsync_InvalidDate_ReturnsError()
    {
        var line = BuildLine(
            type: 1,
            date: "20241340",
            amountCents: "0000000100",
            cpf: "12345678901",
            card: "123456789012",
            time: "101010",
            owner: "OWNER",
            store: "STORE");

        using var stream = new MemoryStream(Encoding.ASCII.GetBytes(line + "\n"));

        var result = await _parser.ParseAsync(stream);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("Invalid date format"));
    }

    [Fact]
    public async Task ParseAsync_AmountParsesAsDecimalCents()
    {
        var line = BuildLine(
            type: 2,
            date: "20231231",
            amountCents: "0000004321",
            cpf: "12345678901",
            card: "123456789012",
            time: "235959",
            owner: "OWNER",
            store: "STORE");

        using var stream = new MemoryStream(Encoding.ASCII.GetBytes(line + "\n"));

        var result = await _parser.ParseAsync(stream);

        result.IsValid.Should().BeTrue();
        var record = result.ValidLines.Single();
        record.Amount.Should().Be(4321m);
    }

    private static string BuildLine(int type, string date, string amountCents, string cpf, string card, string time, string owner, string store)
    {
        var builder = new StringBuilder();
        builder.Append(type.ToString()); // 1
        builder.Append(date); // 8
        builder.Append(amountCents); // 10
        builder.Append(cpf); // 11
        builder.Append(card); // 12
        builder.Append(time); // 6
        builder.Append(owner.PadRight(14)); // 14
        builder.Append(store.PadRight(19)); // 19

        var line = builder.ToString();
        line.Length.Should().Be(80, "CNAB lines must be exactly 80 characters for test setup");
        return line;
    }
}
