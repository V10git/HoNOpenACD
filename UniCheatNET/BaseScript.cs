using Iced.Intel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using V10Sharp.ExtProcess.Patterns;
using static V10Sharp.ExtProcess.Patterns.PatternScanner;


namespace UniCheat;

/// <summary>Base class for scripts.</summary>
public class BaseScript
{
    /// <summary>Base script config type.</summary>
    public class ScriptConfig : Dictionary<string, JsonElement>;

    /// <summary>The exception that is thrown when a script not attached to process.</summary>
    public class NotAttachedException : InvalidOperationException
    {
        public NotAttachedException() : base("You should attach script to process before try to enable it.") { }
    }

    /// <summary>
    /// The script name.
    /// Default: This class name.
    /// </summary>
    public readonly string Name;

    /// <summary>Gets a value indicating whether this <see cref="BaseScript" /> is enabled.</summary>
    /// <value><c>true</c> if enabled; otherwise, <c>false</c>.</value>
    public bool Enabled { get; private set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether this <see cref="BaseScript" /> is checked.
    /// </summary>
    /// <value><c>true</c> if checked; otherwise, <c>false</c>.</value>
    public bool Checked { get; set; } = false;

    /// <summary>Gets a value indicating whether this <see cref="BaseScript" /> is attached.</summary>
    /// <value><c>true</c> if attached; otherwise, <c>false</c>.</value>
    public bool Attached { get => Process != null && Process.Id > 0; }

    /// <summary>The x64 assembler generator.</summary>
    protected readonly Assembler asm = new Assembler(64);

    /// <summary>Gets the attached process.</summary>
    /// <value>The attached process.</value>
    protected Process Process { get; private set; } = null!;

    /// <summary>The script configuration.</summary>
    protected object Config = null!;

    /// <summary>
    /// Gets or sets the type of the script configuration.
    /// Used internally by config loader.
    /// Can be overriden by script class.
    /// </summary>
    /// <value>The type of the configuration.</value>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    public virtual Type ConfigType { get; protected set; } = null!;

    /// <summary>
    /// Gets or sets a value indicating whether script enabled by default in config.
    /// Can be overriden by script class
    /// </summary>
    /// <value><c>true</c> if script enabled by default in config; otherwise, <c>false</c>.</value>
    public virtual bool EnableByDefault { get; protected set; } = true;

    /// <summary>Initializes a new instance of the <see cref="BaseScript" /> class.</summary>
    /// <param name="name">The script name. If <c>null</c> or empty class name will be used</param>
    public BaseScript(string? name = null)
    {
        Name = name ?? GetType().Name;
    }

    /// <summary>Setups the this script configuration.</summary>
    /// <param name="config">The script configuration.</param>
    public virtual void Setup(object config) => Config = config;

    /// <summary>Builds the default script configuration.</summary>
    /// <returns>Default script configuration.</returns>
    public virtual object BuildDefaultConfig() => ConfigType?.GetConstructors()[0].Invoke(null)!;

    /// <summary>Attaches this script to specified process.</summary>
    /// <param name="process">The process.</param>
    /// <returns><c>true</c> if success; otherwise, <c>false</c>.</returns>
    public virtual bool Attach(Process process)
    {
        Process = process;
        return true;
    }

    /// <summary>Checks this script ready to enable.</summary>
    /// <param name="waiter">Callback for waiting.</param>
    /// <returns><c>true</c> if success; otherwise, <c>false</c>.</returns>
    /// <exception cref="UniCheat.BaseScript.NotAttachedException"></exception>
    public virtual bool Check(Func<string, bool>? waiter = null)
    {
        if (Checked)
            return true;

        if (!Attached)
            throw new NotAttachedException();

        return Checked;
    }

    /// <summary>Checks this script ready to enable.</summary>
    /// <param name="waiter">Callback for waiting.</param>
    /// <returns><c>true</c> if success; otherwise, <c>false</c>.</returns>
    /// <exception cref="UniCheat.BaseScript.NotAttachedException"></exception>
    public bool Check(Func<bool> waiter) => Check((s) => waiter());


    /// <summary>Enables this script.</summary>
    /// <param name="waiter">Callback for waiting.</param>
    /// <returns><c>true</c> if success; otherwise, <c>false</c>.</returns>
    /// <exception cref="UniCheat.BaseScript.NotAttachedException"></exception>
    public virtual bool Enable(Func<string, bool>? waiter = null)
    {
        if (!Attached)
            throw new NotAttachedException();

        if (!Check(waiter))
            return false;

        Prepare();
        if (!Inject())
            return false;

        return Enabled = true;
    }

    /// <summary>Enables this script.</summary>
    /// <param name="waiter">Callback for waiting.</param>
    /// <returns><c>true</c> if success; otherwise, <c>false</c>.</returns>
    /// <exception cref="UniCheat.BaseScript.NotAttachedException"></exception>
    public bool Enable(Func<bool> waiter) => Enable((s) => waiter());


    /// <summary>Disables this script.</summary>
    /// <returns><c>true</c> if success; otherwise, <c>false</c>.</returns>
    /// <exception cref="System.NotImplementedException"></exception>
    public virtual bool Disable() { throw new NotImplementedException(); }

    /// <summary>Prepares this script to inject.</summary>
    /// <exception cref="UniCheat.BaseScript.NotAttachedException"></exception>
    protected virtual void Prepare()
    {
        if (!Attached)
            throw new NotAttachedException();
    }

    /// <summary>Injects this script to process memory.</summary>
    /// <returns><c>true</c> if success; otherwise, <c>false</c>.</returns>
    protected virtual bool Inject() => false;

    public Dll? GetModule(string name) =>
        Engine.GetModule(Process, name);

    public bool WaitModule(string module, out Dll dll, Func<string, bool>? waiter = null, bool silentSkipWait = true) =>
        Engine.WaitModule(Process, module, out dll, waiter, silentSkipWait);

    public bool TryGetModule(string name, out Dll dll) =>
        Engine.TryGetModule(Process, name, out dll);

    public IntPtr RCall(string dll, string function, params object?[]? args) =>
        RCFunction.Call(Process, dll, function, args);

    public IntPtr RCall(IntPtr hModule, string function, params object?[]? args) =>
        RCFunction.Call(Process, hModule, function, args);

    public IntPtr RCall(IntPtr pFunction, params object?[]? args) =>
        RCFunction.Call(Process, pFunction, args);

    public PatternScanner CreateScanner(MemCache? memCache = null) =>
        new PatternScanner(Process, true, memCache ?? Process.GetMemCache());

    public Dictionary<string, IntPtr> FindJsonPatterns(string json, MemCache? memCache = null) =>
        PatternScanner.FindJsonPatterns(Process, json, memCache ?? Process.GetMemCache());

    public Dictionary<string, IntPtr> FindPatternsFromFile(string filename, MemCache? memCache = null) =>
        PatternScanner.FindJsonPatterns(Process, File.ReadAllText(filename), memCache ?? Process.GetMemCache());

}
