using System.Text.Json;
using System.Text.Json.Serialization;

namespace VintageStoryModManager.Services;

/// <summary>
/// JSON converter that accepts both string and number types and converts them to string.
/// This handles inconsistent API responses where a field may be either a string or a number.
/// </summary>
public class FlexibleStringConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => ConvertNumberToString(ref reader),
            JsonTokenType.Null => null,
            JsonTokenType.True => "true",
            JsonTokenType.False => "false",
            _ => string.Empty
        };
    }

    private static string ConvertNumberToString(ref Utf8JsonReader reader)
    {
        if (reader.TryGetInt64(out var longValue))
        {
            return longValue.ToString();
        }
        return reader.GetDouble().ToString();
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStringValue(value);
        }
    }
}

/// <summary>
/// JSON converter that accepts both int and string types and converts them to int.
/// This handles inconsistent API responses where a field may be either an int or a string.
/// </summary>
public class FlexibleIntConverter : JsonConverter<int>
{
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Number => reader.GetInt32(),
            JsonTokenType.String => ParseStringToInt(reader.GetString()),
            _ => 0
        };
    }

    private static int ParseStringToInt(string? value)
    {
        if (value != null && int.TryParse(value, out var result))
        {
            return result;
        }
        return 0;
    }

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
    }
}
