using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace V10Sharp.ExtJson;

public static partial class JsonExtensions
{
    [UnconditionalSuppressMessage("AssemblyLoadTrimming", "IL2026")]
    public static T ToObject<T>(this JsonElement element, JsonSerializerOptions options = null!)
    {
        var bufferWriter = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(bufferWriter))
        {
            element.WriteTo(writer);
            writer.Flush();
        }
        return JsonSerializer.Deserialize<T>(bufferWriter.WrittenSpan, options)!;
    }

    public static T ToObject<T>(this JsonDocument document, JsonSerializerOptions options = null!)
    {
        ArgumentNullException.ThrowIfNull(document, nameof(document));
        return document.RootElement.ToObject<T>(options);
    }

    [UnconditionalSuppressMessage("AssemblyLoadTrimming", "IL2026")]
    public static object ToObject(this JsonElement element, Type returnType, JsonSerializerOptions options = null!)
    {
        var bufferWriter = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(bufferWriter))
        {
            element.WriteTo(writer);
            writer.Flush();
        }
        return JsonSerializer.Deserialize(bufferWriter.WrittenSpan, returnType, options)!;
    }

    public static object ToObject(this JsonDocument document, Type returnType, JsonSerializerOptions options = null!)
    {
        ArgumentNullException.ThrowIfNull(document, nameof(document));
        return document.RootElement.ToObject(returnType, options);
    }
}