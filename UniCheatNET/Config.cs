using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using V10Sharp.ExtConsole;


namespace UniCheat;

public static class Config
{
    public class UCConfig
    {
        public class Script
        {
            public class CExternal
            {
                public string? Filename { get; set; }
                public string? FullClassName { get; set; }
            }

            public bool Enable { get; set; } = false;

            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public CExternal? External { get; set; } = null;
        }

        public bool AllowExternalScripts { get; set; } = false;
        public bool ReadOnlyConfig { get; set; } = false;
        public Dictionary<string, Script> Scripts { get; set; } = new Dictionary<string, Script>();

        internal void AddScriptDefaults(string name, bool enable)
        {
            if (!Scripts.ContainsKey(name))
                Scripts[name] = new Script() { Enable = enable };
        }
    }

    public class Root<T> where T : new()
    {
        public UCConfig UniCheat { get; set; } = new UCConfig();
        public T Application { get; set; } = new T();
        public Dictionary<string, object> Scripts { get; set; } = new Dictionary<string, object>();
    }

    public static string Filename = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath)!, "config.json");
    public static Func<string, string> FileReader = File.ReadAllText;
    public static Action<string, string> FileWriter = File.WriteAllText;
    public static JsonSerializerOptions SerializerOptions = new JsonSerializerOptions { WriteIndented = true };

    public static object Active = null!;
    public static UCConfig UC { get => GetProp<UCConfig>(nameof(Root<object>.UniCheat)); }
    public static object App { get => GetProp<object>(nameof(Root<object>.Application)); }
    public static Dictionary<string, object> Scripts { get => GetProp<Dictionary<string, object>>(nameof(Root<object>.Scripts)); }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    private static Type activeType = null!;

    public static object BuildDefault<T>() where T : new()
    {
        activeType = typeof(Root<T>);
        return new Root<T>();
    }

    private static T GetProp<T>(string prop) => 
        (T)activeType.GetProperty(prop)!.GetValue(Active)!;

    [UnconditionalSuppressMessage("AssemblyLoadTrimming", "IL2026")]
    public static void Read()
    {
        if (UC.ReadOnlyConfig)
        {
            return;
        }

        string json = JsonSerializer.Serialize(Active, SerializerOptions);
        try
        {
            FileWriter(Filename, json);
        }
        catch (Exception e)
        {
            Engine.ShowError($"Cant save config to {Ansi.@Name(Filename)}\n{@Ansi.@Except(e)}");
        }
    }

    [UnconditionalSuppressMessage("AssemblyLoadTrimming", "IL2026")]
    public static void Write()
    {
        string json = FileReader(Filename);
        Active = JsonSerializer.Deserialize(json, activeType, SerializerOptions)!;
    }
}
