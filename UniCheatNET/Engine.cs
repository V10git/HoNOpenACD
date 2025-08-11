using System.Diagnostics;
using static V10Sharp.ExtConsole.Ansi;
using V10Sharp.ExtProcess.Windows;
using System.Text.Json;
using V10Sharp.ExtJson;
using System.Reflection;
using System.Diagnostics.CodeAnalysis;

namespace UniCheat;

public sealed class Engine
{
    /// <summary>The exception that is thrown when critical error occurs and requires immediately stop all scripts execution.</summary>
    public class FatalError(string? message) : Exception(message);

    //TODO: public static string Version => typeof(Engine).GetAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
    public const string Version = "1.0-alpha";

    private readonly List<BaseScript> _scripts = new List<BaseScript>();
    private readonly string _processName;
    private Process? _process = null;

    /// <summary>Gets the attached process.</summary>
    /// <value>The attached process.</value>
    public Process? Process => _process;

    /// <summary>Has any error occurred?</summary>
    public static bool AnyError;

    /// <summary>Initializes a new instance of the <see cref="Engine" /> class.</summary>
    /// <param name="processName">Name of the process to attach.</param>
    /// <param name="scripts">The scripts list.</param>
    public Engine(string processName, List<BaseScript> scripts)
    {
        _processName = processName;
        _scripts = scripts;
    }

    /// <summary>Initializes a new instance of the <see cref="Engine" /> class.</summary>
    /// <param name="processName">Name of the process to attach.</param>
    public Engine(string processName)
    {
        _processName = processName;
    }

    /// <summary>Reads the configuration.</summary>
    /// <typeparam name="T">Application config class.<br /><strong>Should be <c>public</c> class</strong>.</typeparam>
    /// <param name="filename">The config filename.</param>
    public void ReadConfig<T>(string? filename = null) where T : new()
    {
        Config.Active = Config.BuildDefault<T>();

        if (!string.IsNullOrEmpty(filename))
            Config.Filename = filename;

        // If config file not exists, then build default and save
        if (!File.Exists(Config.Filename))
        {
            AnsiPrint(@Warning, $"Config file {@Name(Config.Filename)} not found. Creating default config.");
            BuildDefaultConfig();
            Config.Read();
        }

        // Reading config from file
        try
        {
            var configFilename = Path.GetRelativePath(Path.GetDirectoryName(Environment.ProcessPath)!, Config.Filename);
            Console.WriteLine($"Reading config {@Name(configFilename)}");
            Config.Write();
        }
        catch (Exception e)
        {
            // If any error occurs when reading config then build default config without save
            Console.WriteLine(@Except(e));
            AnsiPrint(@Warning, $"Config file {@Name(Config.Filename)} is broken. Using default config.");
            BuildDefaultConfig();
        }

        // Adding external scripts
        if (Config.UC.AllowExternalScripts)
        {
            foreach ((var scriptName, var scriptSettings) in Config.UC.Scripts)
            {
                // not enabled - skip
                if (!scriptSettings.Enable)
                    continue;

                // internal - skip
                if (scriptSettings.External == null)
                    continue;

                LoadExternalScript(scriptName, scriptSettings.External);
            }
        }
    }

    private void BuildDefaultConfig()
    {
        foreach (var script in _scripts)
            try
            {
                var scriptDefault = script.BuildDefaultConfig();
                if (scriptDefault != null)
                    Config.Scripts[script.Name] = scriptDefault;
                Config.UC.AddScriptDefaults(script.Name, script.EnableByDefault);
            }
            catch (FatalError) { throw; }
            catch (Exception e)
            {
                 ShowError($"Exception due trying to get default config for script {@Name(script.Name)}\n{Except(e)}");
#if DEBUG||RESEARCH
                throw;
#endif
            }
    }

    /// <summary>Load script from assembly and create instance.</summary>
    /// <param name="assemblyFilename">The assembly dll.</param>
    /// <param name="fullClassName">Full name of the class.</param>
    /// <param name="catchAll">if set to <c>true</c> all exceptions catched silently.</param>
    /// <param name="showErrors">if set to <c>true</c> for show errors.</param>
    /// <returns><c>true</c> if success; otherwise, <c>false</c></returns>
    [UnconditionalSuppressMessage("AssemblyLoadTrimming", "IL2026")]
    [UnconditionalSuppressMessage("AssemblyLoadTrimming", "IL2058")]
    [RequiresUnreferencedCode("External UniCheat scripts not work with full trimming or AOT compile")]
    [RequiresDynamicCode("External UniCheat scripts not work with full trimming or AOT compile")]
    public bool AddScriptAssembly(string assemblyFilename, string fullClassName, bool catchAll=true, bool showErrors=true)
    {
        try
        {
            var assembly = Assembly.LoadFile(Path.GetFullPath(assemblyFilename));
            var script = assembly.CreateInstance(fullClassName) as BaseScript;
            if (script == null)
            {
                if (catchAll && showErrors)
                    ShowError($"Cant create script {@Id(fullClassName)} instance.");
                return false;
            }
            _scripts.Add(script);
            return true;
        }
        catch (Exception e)
        {
            if (catchAll)
            {
                if (showErrors)
                    ShowError($"Cant create script {@Id(fullClassName)} instance.\n{@Except(e)}");
                return false;
            }
            throw;
        }
    }

    [UnconditionalSuppressMessage("AssemblyLoadTrimming", "IL2026")]
    private void LoadExternalScript(string scriptName, Config.UCConfig.Script.CExternal external)
    {
        if (string.IsNullOrEmpty(external.Filename))
        {
            ShowError($"External script {@Name(scriptName)} {nameof(external.Filename)} empty");
            return;
        }

        if (string.IsNullOrEmpty(external.FullClassName))
        {
            ShowError($"External script {@Name(scriptName)} {nameof(external.FullClassName)} empty");
            return;
        }

        try
        {
            Console.WriteLine($"Loading external script {@Name(scriptName)} from {@Name(external.Filename)}");
            if (!AddScriptAssembly(external.Filename, external.FullClassName, false))
            {
                ShowError($"Cant create external script {@Name(scriptName)} class {@Id(external.FullClassName)} instance.");
                return;
            }

            // Prevent loading scripts with another name
            if (_scripts[^1].Name != scriptName)
            {
                ShowError($"External script {@Name(scriptName)} name dont match script instance name {@Name(_scripts[^1].Name)}. Script will be skipped.");
                _scripts.RemoveAt(_scripts.Count - 1);
            }
        }
        catch (PlatformNotSupportedException)
        {
            ShowError($"This build not support for external scripts. Script {@Name(scriptName)} not loaded.");
        }
        catch (Exception e)
        {
            ShowError($"Exception due trying to load external script {@Name(scriptName)}\n{@Except(e)}");
#if DEBUG
            throw;
#endif 
        }
    }

    /// <summary>Configures all scripts.</summary>
    public void ConfigureScripts()
    {
        foreach (var script in _scripts)
            try
            {
                // no config in UC - creating default
                if (!Config.UC.Scripts.ContainsKey(script.Name))
                {
                    Config.UC.AddScriptDefaults(script.Name, script.EnableByDefault);
                    Config.Read();
                }

                // not enabled - skip
                if (!Config.UC.Scripts[script.Name].Enable)
                    continue;

                // no config - skip
                if (script.ConfigType == null)
                    continue;

                // no script config - creating default
                if (!Config.Scripts.ContainsKey(script.Name))
                {
                    Config.Scripts[script.Name] = script.BuildDefaultConfig();
                    Config.Read();
                }

                try
                {
                    // Deserializing script config json to ConfigType
                    var config = Config.Scripts[script.Name];
                    if (config is JsonElement)
                        config = Config.Scripts[script.Name] = ((JsonElement)config).ToObject(script.ConfigType, Config.SerializerOptions);                        

                    // Setups script configuration
                    script.Setup(config);
                }
                catch (FatalError) { throw; }
                catch (Exception e)
                {
                    // If any error occurred, we will try to use the default script configuration as last chance to configure script
                    var lastChance = false;
                    try
                    {
                        script.Setup(script.BuildDefaultConfig());
                        AnsiPrint(@Warning, $"Warning: Config for script {@Name(script.Name)} restored to {@Id("DEFAULT")} after error \"{@Bad(e.Message)}\"");
                        lastChance = true;
                    }
                    catch { }
                    // re-throw if failed last chance
                    if (!lastChance)
                        throw;
                }
            }
            catch (FatalError) { throw; }                
            catch (Exception e)
            {
                ShowError($"Exception due calling Setup for script {@Name(script.Name)}\n{Except(e)}");
#if DEBUG || RESEARCH
                throw;
#endif
            }
    }

    /// <summary>Waits the process and attach if success.</summary>
    /// <param name="waiter">Callback for waiting.</param>
    /// <param name="waitInit">if set to <c>true</c> wait full process initialization.</param>
    /// <param name="waitInitTimeout">full process initialization waiting timeout in milliseconds.</param>
    /// <returns><c>true</c> if success; otherwise, <c>false</c></returns>
    public bool WaitProcess(Func<bool>? waiter = null, bool waitInit=true, int waitInitTimeout=5000)
    {
        if (ProcessHelpers.TryGetProcess(_processName, out _process))
        {
            var ret = true;
            if (waitInit)
                if (!_process!.WaitForInputIdle(waitInitTimeout))
                {
                    AnsiPrint(@Warning, $"Process {@Name(_processName)} PID {@Id(_process!.Id)} is stuck. Terminate it or restart PC.");
                    if (Config.UC.AutoCloseStuckProcesses)
                    {
                        ret = false;
                        _process.Kill();
                        Thread.Sleep(100);
                        AnsiPrint(@Warning, $"Process {@Name(_processName)} PID {@Id(_process!.Id)} killed.");
                    }
                }
            if (ret)
            {
                AnsiPrint(@Good, $"Process {@Name(_processName)} found ( {@Id(_process!.Id)} )");
                return true;
            }
        }

        Console.Write($"Waiting for process {@Name(_processName + ".exe")}...");
        if (!ProcessHelpers.WaitProcess(_processName, out _process, waiter))
        {
            Console.WriteLine(@Bad("timeout. Process not found."));
            AnyError = true;
            return false;
        }
        if (waitInit)
            _process!.WaitForInputIdle(waitInitTimeout);
        Console.WriteLine($"{@Good("found")} ( {@Id(_process!.Id)} )");
        return true;
    }

    /// <summary>Run all scripts.</summary>
    /// <param name="waiter">Callback for waiting.</param>
    /// <exception cref="System.ArgumentNullException"></exception>
    public void RunScripts(Func<bool>? waiter = null!)
    {
        ArgumentNullException.ThrowIfNull(_process);
        foreach (var script in _scripts)
        {
            // not enabled - skip
            if (!Config.UC.Scripts[script.Name].Enable)
            {
                Console.WriteLine($"Script {@Name(script.Name)} is {@FGL.Gray("disabled")}");
                continue;
            }

            try
            {
                script.Attach(_process);

                try
                {
                    try
                    {
                        script.Enable(waiter ?? (() => true));
                    }
                    catch
                    {
                        if (_process.HasExited)
                        {
                            ShowError($"Process {@Name(_process.ProcessName)} has crashed or exited.");
                            return;
                        }
                        throw;
                    }
                }
                catch (FatalError) { throw; }
                catch (Exception e) when (e is InvalidOperationException || e is KeyNotFoundException)
                {
                    if (script.ConfigType == null)
                        throw;

                    // If any error occurred, we will try to use the default script configuration
                    ShowError($"on enabling {@Name(script.Name)} script\n{@FG.Yellow(e)}");
                    Console.WriteLine($"Trying to enable {@Name(script.Name)} with {@Id("DEFAULT")} config...");
                    script.Setup(script.BuildDefaultConfig());
                    script.Enable(waiter ?? (() => true));
#if DEBUG || RESEARCH
                    throw;
#endif
                }
            }
            catch (FatalError) { throw; }
            catch (System.ComponentModel.Win32Exception e)
            {
                if (e.NativeErrorCode == 5) //access denied
                    ShowError("Unable to obtain necessary operating system privileges. Please restart application as administrator.");
                ShowError($"on trying enable {@Name(script.Name)} script\n{@FG.Yellow(e)}");
            }
            catch (Exception e)
            {
                ShowError($"on trying enable {@Name(script.Name)} script\n{@FG.Yellow(e)}");
#if DEBUG || RESEARCH
                throw;
#endif
            }
            Console.WriteLine($"Script {@Name(script.Name)} is {@Flag(script.Enabled, "enabled", "failed")}");
            if (!script.Enabled)
                AnyError = true;
        }
    }

    public Dll? GetModule(string name) => 
        GetModule(_process!, name);

    public bool TryGetModule(string name, out Dll dll) =>
        TryGetModule(_process!, name, out dll);

    public bool WaitModule(string module, out Dll dll, Func<string, bool>? waiter = null, bool silentSkipWait = true) => 
        WaitModule(_process!, module, out dll, waiter, silentSkipWait);

    /// <summary>Waits the process modules and return it if success.</summary>
    /// <param name="modules">The modules names array.</param>
    /// <param name="waiter">Callback for waiting.</param>
    /// <param name="silentSkipWait">if set to <c>true</c> console message for already loaded modules not shows.</param>
    /// <returns>Requested modules array</returns>
    public Dll[] WaitModules(string[] modules, Func<string, bool>? waiter = null, bool silentSkipWait = false)
    {
        var dllList = new List<Dll>();
        for (int i = 0; i < modules.Length; i++)
        {
            if (!WaitModule(_process!, modules[i], out var dll, waiter, silentSkipWait))
                return dllList.ToArray();
            dllList.Add(dll);
        }
        return dllList.ToArray();
    }

    /// <summary>Waits the process modules and return it if success.</summary>
    /// <param name="modules">The modules names array.</param>
    /// <param name="dlls">Requested modules array.</param>
    /// <param name="waiter">Callback for waiting.</param>
    /// <param name="silentSkipWait">if set to <c>true</c> console message for already loaded modules not shows.</param>
    /// <returns>
    ///   <c>true</c> if success; otherwise, <c>false</c>.</returns>
    public bool WaitModules(string[] modules, out Dll[] dlls, Func<string, bool>? waiter = null, bool silentSkipWait = false) =>
        (dlls = WaitModules(modules, waiter, silentSkipWait)).Length == modules.Length;

    /// <summary>Waits the process modules and return it if success.</summary>
    /// <param name="modules">The modules names array.</param>
    /// <param name="waiter">Callback for waiting.</param>
    /// <param name="silentSkipWait">if set to <c>true</c> console message for already loaded modules not shows.</param>
    /// <returns>Requested modules array</returns>
    public Dll[] WaitModules(string[] modules, Func<bool>? waiter = null, bool silentSkipWait = false) => 
        WaitModules(modules, waiter == null ? null : (s) => waiter(), silentSkipWait);

    /// <summary>Waits the process modules and return it if success.</summary>
    /// <param name="modules">The modules names array.</param>
    /// <param name="dlls">Requested modules array.</param>
    /// <param name="waiter">Callback for waiting.</param>
    /// <param name="silentSkipWait">if set to <c>true</c> console message for already loaded modules not shows.</param>
    /// <returns>
    ///   <c>true</c> if success; otherwise, <c>false</c>.</returns>
    public bool WaitModules(string[] modules, out Dll[] dlls, Func<bool>? waiter = null, bool silentSkipWait = false) =>
        (dlls = WaitModules(modules, waiter == null ? null : (s) => waiter(), silentSkipWait)).Length == modules.Length;


    /// <summary>
    /// Shows the error in console without full ANSI support
    /// </summary>
    /// <param name="s">error text or object</param>
    public static void ShowError(object s)
    {
        AnyError = true;
        Console.WriteLine(@Bad("Error: " + s));
    }

    /// <summary>
    /// Shows the error in console with full ANSI support
    /// </summary>
    /// <param name="s">composite format string</param>
    public static void ShowError(FormattableString s)
    {
        AnyError = true;
        var color = @Bad();
        if (IsAnsiEnabled)
            Console.WriteLine(color + "Error: " + AnsiFormat(color, s) + @RST);
        else
            Console.WriteLine("Error: " + s);
    }

    /// <summary>Waits the process module and return it if success.</summary>
    /// <param name="process">The process.</param>
    /// <param name="moduleName">Name of the module.</param>
    /// <param name="dll">Requested module result.</param>
    /// <param name="waiter">Callback for waiting.</param>
    /// <param name="silentSkipWait">if set to <c>true</c> console message for already loaded modules not shows.</param>
    /// <returns><c>true</c> if success; otherwise, <c>false</c>.</returns>
    /// <exception cref="System.ArgumentNullException"></exception>
    /// <seealso cref="UniCheat.Dll" />
    public static bool WaitModule(Process process, string moduleName, out Dll dll, Func<string, bool>? waiter = null, bool silentSkipWait = true)
    {
        ArgumentNullException.ThrowIfNull(process);
        if (TryGetModule(process, moduleName, out dll))
        {
            if (!silentSkipWait)
                AnsiPrint(@Good, $"Module {@Name(moduleName)} found at {@Id(dll.Handle)}");
            return true;
        }

        Console.Write($"Waiting for module {@Name(moduleName)}...");
        while (!TryGetModule(process, moduleName, out dll))
        {
            if ((waiter != null && !waiter(moduleName)) || process.HasExited)
            {
                Console.WriteLine(Bad("failed"));
                AnyError = true;
                return false;
            }
            process.Refresh();
        }
        AnsiPrint(@Good, $"found at {@Id(dll.Handle)}");
        return true;
    }

    /// <summary>
    /// Tries the get process module.
    /// </summary>
    /// <param name="process">The process.</param>
    /// <param name="moduleName">Name of the module.</param>
    /// <param name="dll">Requested module result.</param>
    /// <returns><c>true</c> if success; otherwise, <c>false</c>.</returns>
    /// <exception cref="System.ArgumentNullException"></exception>
    /// <seealso cref="UniCheat.Dll" />
    public static bool TryGetModule(Process process, string moduleName, out Dll dll)
    {
        ArgumentNullException.ThrowIfNull(process);
        dll = new Dll(process, moduleName, process.GetModule(moduleName));
        return dll.Module != null;
    }

    /// <summary>Gets the process module.</summary>
    /// <param name="process">The process.</param>
    /// <param name="moduleName">Name of the module.</param>
    /// <returns>Requested module if success or null.</returns>
    /// <exception cref="System.ArgumentNullException"></exception>
    /// <seealso cref="UniCheat.Dll" />
    public static Dll? GetModule(Process process, string moduleName)
    {
        ArgumentNullException.ThrowIfNull(process);
        var module = process.GetModule(moduleName);
        if (module == null)
            return null;
        return new Dll(process, moduleName, module);
    }
}
