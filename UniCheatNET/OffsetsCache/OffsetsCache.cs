using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using static V10Sharp.ExtConsole.Ansi;

namespace UniCheat;

/// <summary>
/// Manages loading and saving of cached offsets to/from a JSON file,
/// and stores offset data directly as key (string) to value (<see cref="IntPtr" />) pairs.
/// </summary>
/// <remarks>
/// This class combines the functionality of an offsets data container and a JSON
/// cache manager. It is designed for AOT compilation and single-file deployments —
/// all JSON serialization is performed through <see cref="OffsetsJsonContext" />,
/// which uses compile-time source generation instead of runtime reflection.
///
/// <para>
/// A typical usage pattern is:
/// <code>
/// var cache = new OffsetsCache("offsets.json");
/// cache.Load();
///
/// if (!cache.TryGetOffset("g_camDistanceMax", out var cachedOffset))
/// {
///     // Scan for the pattern and obtain the offset
///     var offset = HoN_CVar&lt;float&gt;.CreateFromPattern(...);
///     cache.SetOffset("g_camDistanceMax", offset);
/// }
///
/// cache.Save();
/// </code>
/// </para>
/// </remarks>
public class OffsetsCache
{
    private readonly string _filename = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath)!, "ocache.json");
    private Dictionary<string, IntPtr> _offsets = new();
    public bool IsChanged { get; private set; }

    /// <summary>
    /// Gets the filename of the offsets cache file.
    /// </summary>
    public string Filename => _filename;

    /// <summary>
    /// Gets the number of stored offsets.
    /// </summary>
    public int Count => _offsets.Count;

    /// <summary>
    /// Gets a value indicating whether the stored offsets collection is empty.
    /// </summary>
    public bool IsEmpty => _offsets.Count == 0;

    /// <summary>
    /// Gets or sets the <see cref="IntPtr" /> value for the specified key.
    /// Returns <see cref="IntPtr.Zero" /> if the key does not exist.
    /// </summary>
    /// <param name="key">The name of the offset.</param>
    /// <returns>
    /// The <see cref="IntPtr" /> value, or <see cref="IntPtr.Zero" /> if the key is not found.
    /// </returns>
    public IntPtr this[string key]
    {
        get => _offsets.TryGetValue(key, out var value) ? value : IntPtr.Zero;
        set {
            if (this[key] != value)
            {
                _offsets[key] = value;
                IsChanged = true;
            }
        }
    }

    /// <summary>
    /// Creates a new <see cref="OffsetsCache" /> instance with the specified filename.
    /// </summary>
    /// <param name="filename">The path to the JSON offsets cache file.</param>
    public OffsetsCache(string filename)
    {
        _filename = filename;
    }

    /// <summary>
    /// Creates a new <see cref="OffsetsCache" /> instance with default filename.
    /// </summary>
    public OffsetsCache()
    {
    }

    /// <summary>
    /// Determines whether the stored offsets contain the specified key.
    /// </summary>
    /// <param name="key">The key to check.</param>
    /// <returns><c>true</c> if the key exists; otherwise, <c>false</c>.</returns>
    public bool Contains(string key) => _offsets.ContainsKey(key);

    /// <summary>
    /// Tries to get a cached offset by key.
    /// </summary>
    /// <param name="key">The name of the offset.</param>
    /// <param name="offset">
    /// When this method returns, contains the cached <see cref="IntPtr" /> if found
    /// and non-zero; otherwise, <see cref="IntPtr.Zero" />.
    /// </param>
    /// <returns>
    /// <c>true</c> if the offset was found and non-zero; otherwise, <c>false</c>.
    /// </returns>
    public bool TryGetOffset(string key, out IntPtr offset)
    {
        offset = _offsets.TryGetValue(key, out var v) ? v : IntPtr.Zero;
        return offset != IntPtr.Zero;
    }

    /// <summary>
    /// Loads offsets from the JSON file.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the file was loaded successfully;
    /// <c>false</c> if the file does not exist or could not be parsed.
    /// </returns>
    [UnconditionalSuppressMessage("AssemblyLoadTrimming", "IL2026")]
    public bool Load()
    {
        if (!File.Exists(_filename))
            return false;

        try
        {
            var json = File.ReadAllText(_filename);
            var loaded = JsonSerializer.Deserialize<Dictionary<string, IntPtr>>(json, OffsetsJsonContext.Default.Options);
            _offsets = loaded ?? new Dictionary<string, IntPtr>();

            if (_offsets.Count > 0)
            {
                var relFilename = Path.GetRelativePath(Path.GetDirectoryName(Environment.ProcessPath)!, _filename);
                AnsiPrint($"Readed {@Id(_offsets.Count)} cached offsets from {@Name(relFilename)}");
            }
            return true;
        }
        catch (Exception e)
        {
            AnsiPrint(@Warning($"Cant read offsets cache from {@Name(_filename)}\n{@Except(e)}"));
            return false;
        }
    }

    /// <summary>
    /// Saves offsets to the JSON file.
    /// </summary>
    /// <returns>
    /// <c>true</c> if offsets collection is empty \not changed or the file was saved successfully;
    /// <c>false</c> otherwise.
    /// </returns>
    [UnconditionalSuppressMessage("AssemblyLoadTrimming", "IL2026")]
    public bool Save()
    {
        if (IsEmpty || !IsChanged) return true;

        try
        {
            var relFilename = Path.GetRelativePath(Path.GetDirectoryName(Environment.ProcessPath)!, _filename);
            AnsiPrint($"Saving offsets cache to {@Name(relFilename)}");
            //string json;
            //var copy = new Dictionary<string, IntPtr>(_offsets);
            //json = JsonSerializer.Serialize(copy, OffsetsJsonContext.Default.Options);
            string json = JsonSerializer.Serialize(_offsets, OffsetsJsonContext.Default.Options);
            File.WriteAllText(_filename, json);
            return true;
        }
        catch (Exception e)
        {
            Engine.ShowError($"Cant save offsets cache to {@Name(_filename)}\n{@Except(e)}");
            return false;
        }
    }

    /// <summary>
    /// Returns an enumerator that iterates through the stored offsets.
    /// </summary>
    /// <returns>An enumerator for the offsets.</returns>
    public IEnumerator<KeyValuePair<string, IntPtr>> GetEnumerator() => _offsets.GetEnumerator();
}

/// <summary>
/// AOT-compatible JSON serialization context for <see cref="Dictionary{String, IntPtr}" />.
/// </summary>
/// <remarks>
/// This context uses compile-time source generation instead of runtime reflection,
/// making it compatible with <c>PublishAot</c>, <c>PublishTrimmed</c>, and
/// single-file deployments. The <see cref="IntPtrJsonConverter" /> is registered
/// via the <see cref="JsonSourceGenerationOptionsAttribute.Converters" /> property
/// to handle <see cref="IntPtr" /> serialization as hex strings.
///
/// <para>
/// When consuming this context, use <see cref="OffsetsJsonContext.Default.Options" />
/// as the <see cref="JsonSerializerOptions" /> argument to
/// <see cref="JsonSerializer.Serialize{T}(T, JsonSerializerOptions)" /> and
/// <see cref="JsonSerializer.Deserialize{T}(string, JsonSerializerOptions)" />.
/// </para>
/// </remarks>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    Converters = new[] { typeof(IntPtrJsonConverter) })]
[JsonSerializable(typeof(Dictionary<string, IntPtr>))]
public partial class OffsetsJsonContext : JsonSerializerContext
{
}
