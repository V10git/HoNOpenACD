using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using static TerraFX.Interop.Windows.Windows;
using static V10Sharp.Helpers;
using static V10Sharp.ExtConsole.Ansi;
using static V10Sharp.ExtConsole.ConsoleHelpers;
using static V10Sharp.ExtProcess.Windows.ProcessHelpers;
using UniCheat;
using static UniCheat.Engine;
using TerraFX.Interop.Windows;


namespace HoNOpenACD;

public static class Program
{
    private const string HEADER = @"
 _   _                                   __   _   _                        _   _     
| | | | ___ _ __ ___   ___  ___    ___  / _| | \ | | _____      _____ _ __| |_| |__  
| |_| |/ _ \ '__/ _ \ / _ \/ __|  / _ \| |_  |  \| |/ _ \ \ /\ / / _ \ '__| __| '_ \ 
|  _  |  __/ | | (_) |  __/\__ \ | (_) |  _| | |\  |  __/\ V  V /  __/ |  | |_| | | |
|_| |_|\___|_|  \___/ \___||___/  \___/|_|   |_| \_|\___| \_/\_/ \___|_|   \__|_| |_|
                                                                                     
  ___                                                       _    ____ ____  
 / _ \ _ __   ___ _ __    ___  ___  _   _ _ __ ___ ___     / \  / ___|  _ \ 
| | | | '_ \ / _ \ '_ \  / __|/ _ \| | | | '__/ __/ _ \   / _ \| |   | | | |
| |_| | |_) |  __/ | | | \__ \ (_) | |_| | | | (_|  __/  / ___ \ |___| |_| |
 \___/| .__/ \___|_| |_| |___/\___/ \__,_|_|  \___\___| /_/   \_\____|____/ 
      |_|";
    private const ushort VERSION_MAX_LEN = 20;


    public static void Main(string[] args)
    {
        // Enable ANSI console, print logo header and version infos
        AnsiInitialize();
        DrawHeader();
        Console.WriteLine($"{@Name("HoN Open ACD")} {@Id("v" + Version)} [ {@Name("UniCheat.Net")} {@Id("v" + Engine.Version)} ] by V10");

        // Create UniCheat Engine with scripts
        var engine = new Engine(Consts.PROCESS_NAME, new([
#if RESEARCH
            Research.GetScript(),  // ResearchScript should be first
#endif
            new CameraDistance()
        ]));

        engine.ReadConfig<ApplicationConfig>();
        engine.ConfigureScripts();

        // Check for SE_DEBUG privileges (not always required)
        var needAdmin = !EnableDebugPrivileges();

        if (engine.WaitProcess(Animate) && engine.WaitModules(Consts.REQUIRED_MODULES, out var tmp, Animate))
            engine.RunScripts(Animate);

        // Wait for user input if any error happened
        if (AnyError)
        {
            // Show additional hint without admin rights
            if (needAdmin)
                ShowError("Unable to obtain necessary operating system privileges. Please restart application as administrator.");

            WaitAnyKey();
        }
        else
        {
            Console.WriteLine(@Good("All done"));

            // Wait for user input if configured
            if (AppConfig.WaitKey)
                WaitAnyKey(DrawHeader, 2000);
        }
    }

    public class ApplicationConfig
    {
        public bool WaitKey { get; set; } = true;
    }

    public static ApplicationConfig AppConfig => (ApplicationConfig)Config.App;
    public static string Version
    {
        get
        {
            var a = Assembly.GetExecutingAssembly();
            var vStr = a.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
            return string.IsNullOrEmpty(vStr)
                ? string.Empty
                : vStr.Substring(0, Math.Min(VERSION_MAX_LEN, vStr.Length));
        }
    }
    private static readonly Random random = new Random();
    private static ValueTuple<int, int>? initialCursorPos = null;
    private static readonly int[] rainbowColors = CalcRainbow();
    private static int rainbowColorIndex = 8;
    private static string[] headerLines = HEADER.Split("\r\n")[1..];

    private static bool DrawHeader()
    {
        var oldCurVis = Console.CursorVisible;
        Console.CursorVisible = false;

        var first = initialCursorPos == null;
        if (!first)
        {
            if (!IsAnsiEnabled)
                return true;
            SaveCursorPos();
            SetCursorPos(initialCursorPos!.Value);
        }
        else
            initialCursorPos = GetCursorPos();

        var colorStream = RoundRobin(rainbowColors, rainbowColorIndex).GetEnumerator();
        colorStream.MoveNext();
        rainbowColorIndex--;
        if (rainbowColorIndex < 0)
            rainbowColorIndex = rainbowColors.Length - 1;

        foreach (var ln in headerLines)
        {
            Console.WriteLine(@Rgb(colorStream.Current, ln));
            colorStream.MoveNext();
        }
        if (!first)
            RestoreCursorPos();
        Console.CursorVisible = oldCurVis;
        return true;
    }

    private static ConsoleColor GetRandomConsoleColor()
    {
        var consoleColors = Enum.GetValues<ConsoleColor>();
        return (ConsoleColor)consoleColors.GetValue(random.Next(1, consoleColors.Length))!;
    }

    public static bool Animate()
    {
        Console.ForegroundColor = GetRandomConsoleColor();

        var curPos = Console.GetCursorPosition();
        if (Console.BufferWidth - 1 == curPos.Left)
            Console.WriteLine(".");
        else
            Console.Write(".");
        DrawHeader();
        Thread.Sleep(20);
        return true;
    }

    private static int[] CalcRainbow()
    {
        var lst = new List<int>();
        for (int i = 0; i < 255; i += 5)
        {
            lst.Add(ColorHLSToRGB(i, 127, 127));
        }
        return lst.ToArray();
    }

    public static void WaitAnyKey(Func<bool>? worker = null, int msTimeout = -1)
    {
        var codes = RoundRobin(new byte[] { 7, 0 }).GetEnumerator();
        byte NextCode()
        {
            codes.MoveNext();
            return codes.Current;
        }

        var oldCurVis = Console.CursorVisible;
        Console.CursorVisible = false;
        var timeout = DateTime.Now.AddMilliseconds(msTimeout);
        Console.Write(@AnsiSeq(NextCode(), "Press any key to exit...") + "\r");
        while (!Console.KeyAvailable)
        {
            if (worker != null)
                Console.Write(@AnsiSeq(NextCode(), "Press any key to exit...") + "\r");
            WaitKeySleep(300, 50, worker);
            if (msTimeout > 0 && DateTime.Now > timeout)
                break;
        }
        if (Console.KeyAvailable)
            Console.ReadKey(true);
        if (IsAnsiEnabled)
            Console.WriteLine(@RST);
        Console.CursorVisible = oldCurVis;
    }

    [DllImport("shlwapi.dll")]
    private static extern int ColorHLSToRGB(int H, int L, int S);

}

#if (AOTRELEASE||JSON_SOURCE_GENERATION)
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Config.Root<Program.ApplicationConfig>))]
[JsonSerializable(typeof(CameraDistance.ScriptConfig))]
internal partial class SourceGenerationContext : JsonSerializerContext
{
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void ModuleInitializer() =>
           Config.SerializerOptions = SourceGenerationContext.Default.Options;
}
#endif

