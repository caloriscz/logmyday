using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;

namespace LogMyDay.Shared.Serialization;

/// <summary>
/// Provides flexible parsing and formatting for <see cref="TimeSpan"/> values.
/// Accepts multiple common formats ("c", "hh:mm", "hh:mm:ss", ISO 8601 duration).
/// </summary>
public sealed class FlexibleTimeSpanConverter : JsonConverter<TimeSpan?>
{
    private static readonly string[] SupportedFormats =
    [
        "c",
        @"hh\\:mm",
        @"hh\\:mm\\:ss",
        @"hh\\:mm\\:ss\.FFFFFFF"
    ];

    public override TimeSpan? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"Unexpected token parsing TimeSpan. Expected String, got {reader.TokenType}.");
        }

        var stringValue = reader.GetString();
        if (string.IsNullOrWhiteSpace(stringValue))
        {
            return null;
        }

        if (TimeSpan.TryParseExact(stringValue, SupportedFormats, CultureInfo.InvariantCulture, TimeSpanStyles.None, out var exact))
        {
            return exact;
        }

        if (TimeSpan.TryParse(stringValue, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        try
        {
            return XmlConvert.ToTimeSpan(stringValue);
        }
        catch (FormatException)
        {
            throw new JsonException($"Unable to parse TimeSpan value '{stringValue}'.");
        }
    }

    public override void Write(Utf8JsonWriter writer, TimeSpan? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
        {
            writer.WriteStringValue(value.Value.ToString("c", CultureInfo.InvariantCulture));
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}
