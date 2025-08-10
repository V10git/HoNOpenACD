using System.Text.Json;
using System.Text.Json.Serialization;


namespace V10Sharp.ExtProcess.Patterns;

public class JsonPattern
{
    private class HexConverter<T> : JsonConverter<T> where T: unmanaged
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            object result;
            bool negative = false;
            if (reader.TokenType == JsonTokenType.String)
            {
                var value = reader.GetString();
                if (string.IsNullOrEmpty(value))
                    return default;

                negative = value[0] == '-';
                if (negative)
                    value = value[1..];

                try
                {
                    result = Convert.ChangeType(Convert.ToInt64(value) * (negative ? -1 : 1), typeof(T));
                }
                catch
                {
                    result = Convert.ChangeType(Convert.ToInt64(value, 16) * (negative ?  -1 : 1), typeof(T));
                }
            }
            else
            {
                result = Convert.ChangeType(reader.GetInt64(), typeof(T));
            }
            return (T)result;
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
        
    }

    public enum DereferenceType
    {
        None = 0,
        Relative = 1,
        Direct = 2
    }

    public required string Module { get; set; }

    public string? Symbol { get; set; } = null;
    [JsonConverter(typeof(HexConverter<int>))]
    public int SymbolLen { get; set; } = 0;

    public required string Pattern { get; set; }
    [JsonConverter(typeof(HexConverter<int>))]
    public int Offset { get; set; } = 0;

    public DereferenceType DerefType { get; set; } = DereferenceType.None;
    [JsonConverter(typeof(HexConverter<ulong>))]
    public ulong DerefRelativeSize { get; set; } = 0;
    [JsonConverter(typeof(HexConverter<int>))]
    public int DerefOffset { get; set; } = 0;

    public DereferenceType Deref2Type { get; set; } = DereferenceType.None;
    [JsonConverter(typeof(HexConverter<ulong>))]
    public ulong Deref2RelativeSize { get; set; } = 0;
}


