using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UniCheat;

/// <summary>
/// JSON converter for <see cref="IntPtr" /> that serializes as a hexadecimal string
/// and deserializes from both hex strings (e.g. <c>"0x180AFC348"</c>) and numeric values.
/// </summary>
/// <remarks>
/// This converter is AOT-safe — it uses no reflection and only primitive parsing APIs
/// (<see cref="ulong.Parse(ReadOnlySpan{char}, NumberStyles, IFormatProvider)" /> and
/// <see cref="long.Parse(string, IFormatProvider)" />), making it compatible with
/// <c>PublishAot</c> and <c>PublishTrimmed</c>.
/// </remarks>
public sealed class IntPtrJsonConverter : JsonConverter<IntPtr>
{
    /// <inheritdoc />
    public override IntPtr Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => ParseString(reader.GetString()),
            JsonTokenType.Number => new IntPtr(reader.GetInt64()),
            _ => IntPtr.Zero,
        };
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, IntPtr value, JsonSerializerOptions options)
    {
        writer.WriteStringValue($"0x{(ulong)value.ToInt64():X}");
    }

    private static IntPtr ParseString(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return IntPtr.Zero;

        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return new IntPtr(unchecked((long)ulong.Parse(
                value.AsSpan(2),
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture)));
        }

        return new IntPtr(long.Parse(value, CultureInfo.InvariantCulture));
    }
}