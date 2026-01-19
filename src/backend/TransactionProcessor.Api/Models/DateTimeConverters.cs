using System.Text.Json;
using System.Text.Json.Serialization;

namespace System.Text.Json.Serialization;

/// <summary>
/// Custom JSON converter for DateTime that serializes to ISO 8601 UTC format
/// Format: yyyy-MM-ddTHH:mm:ss.fffZ (with milliseconds and Z suffix for UTC)
/// </summary>
public class Rfc3339DateTimeConverter : JsonConverter<DateTime>
{
    /// <summary>
    /// ISO 8601 format with milliseconds and Z suffix
    /// </summary>
    private const string DateTimeFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";

    /// <summary>
    /// Reads a DateTime from JSON
    /// </summary>
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        
        if (string.IsNullOrEmpty(value))
        {
            throw new JsonException("DateTime value cannot be null or empty");
        }

        // Try to parse as ISO 8601 UTC format
        if (DateTime.TryParseExact(value, "O", null, System.Globalization.DateTimeStyles.RoundtripKind, out var dateTime))
        {
            // Ensure it's UTC
            if (dateTime.Kind != DateTimeKind.Utc)
            {
                dateTime = dateTime.ToUniversalTime();
            }
            return dateTime;
        }

        // Fallback to general parsing
        if (DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsedDate))
        {
            if (parsedDate.Kind != DateTimeKind.Utc)
            {
                parsedDate = parsedDate.ToUniversalTime();
            }
            return parsedDate;
        }

        throw new JsonException($"Unable to parse '{value}' as DateTime in ISO 8601 format");
    }

    /// <summary>
    /// Writes a DateTime to JSON
    /// </summary>
    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        // Ensure datetime is UTC
        var utcValue = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
        
        // Write in ISO 8601 format with Z suffix
        writer.WriteStringValue(utcValue.ToString(DateTimeFormat));
    }
}

/// <summary>
/// Custom JSON converter for nullable DateTime
/// </summary>
public class NullableDateTimeConverter : JsonConverter<DateTime?>
{
    private readonly Rfc3339DateTimeConverter _dateTimeConverter = new();

    /// <summary>
    /// Reads a nullable DateTime from JSON
    /// </summary>
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        return _dateTimeConverter.Read(ref reader, typeof(DateTime), options);
    }

    /// <summary>
    /// Writes a nullable DateTime to JSON
    /// </summary>
    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
        }
        else
        {
            _dateTimeConverter.Write(writer, value.Value, options);
        }
    }
}
