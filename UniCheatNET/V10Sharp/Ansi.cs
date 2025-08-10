using Microsoft.CSharp.RuntimeBinder;
using System.Diagnostics.CodeAnalysis;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using static V10Sharp.ExtConsole.Ansi.AnsiConsts;


namespace V10Sharp.ExtConsole;

public static class Ansi
{
    public class AnsiConsts
    {
        public const char ESCAPE_CHAR = '\u001b';
        public const string ESCAPE = "\u001b[";
        public const string ESCAPE_256 = "\u001b[38;5;";
        public const string ESCAPE_RGB = "\u001b[38;2;";

        public const string CODE_COMMON_SUFFIX = "m";

        public const string CODE_COLOR_RESET = ESCAPE + "0" + CODE_COMMON_SUFFIX;

        public const string CODE_BOLD_ON = ESCAPE + "2" + CODE_COMMON_SUFFIX;
        public const string CODE_BOLD_OFF = ESCAPE + "22" + CODE_COMMON_SUFFIX;
        public const string CODE_ITALIC_ON = ESCAPE + "3" + CODE_COMMON_SUFFIX;
        public const string CODE_ITALIC_OFF = ESCAPE + "23" + CODE_COMMON_SUFFIX;
        public const string CODE_UNDERLINE_ON = ESCAPE + "4" + CODE_COMMON_SUFFIX;
        public const string CODE_UNDERLINE_OFF = ESCAPE + "24" + CODE_COMMON_SUFFIX;

        public static string COLOR_ID = Colors.FGL.WHITE;
        public static string COLOR_VALUE = Colors.FGL.WHITE;
        public static string COLOR_NAME = Colors.FG.CYAN;

        public static string COLOR_BAD = Colors.FG.RED;
        public static string COLOR_GOOD = Colors.FG.GREEN;

        public static string COLOR_TRUE = Colors.FG.GREEN;
        public static string COLOR_FALSE = Colors.FG.RED;

        public static string COLOR_WARNING =  ESCAPE_RGB + "255;140;0" + CODE_COMMON_SUFFIX;
        public static string COLOR_EXCEPTION = Colors.FG.YELLOW;

        public class Colors
        {
            public static class FG
            {
                public const string BLACK = ESCAPE + "30" + CODE_COMMON_SUFFIX;
                public const string RED = ESCAPE + "31" + CODE_COMMON_SUFFIX;
                public const string GREEN = ESCAPE + "32" + CODE_COMMON_SUFFIX;
                public const string YELLOW = ESCAPE + "33" + CODE_COMMON_SUFFIX;
                public const string BLUE = ESCAPE + "34" + CODE_COMMON_SUFFIX;
                public const string MAGENTA = ESCAPE + "35" + CODE_COMMON_SUFFIX;
                public const string CYAN = ESCAPE + "36" + CODE_COMMON_SUFFIX;
                public const string WHITE = ESCAPE + "37" + CODE_COMMON_SUFFIX;
            }

            public static class FGL
            {
                public const string GRAY = ESCAPE + "90" + CODE_COMMON_SUFFIX;
                public const string RED = ESCAPE + "91" + CODE_COMMON_SUFFIX;
                public const string GREEN = ESCAPE + "92" + CODE_COMMON_SUFFIX;
                public const string YELLOW = ESCAPE + "93" + CODE_COMMON_SUFFIX;
                public const string BLUE = ESCAPE + "94" + CODE_COMMON_SUFFIX;
                public const string MAGENTA = ESCAPE + "95" + CODE_COMMON_SUFFIX;
                public const string CYAN = ESCAPE + "96" + CODE_COMMON_SUFFIX;
                public const string WHITE = ESCAPE + "97" + CODE_COMMON_SUFFIX;
            }

            public static class BG
            {
                public const string BLACK = ESCAPE + "40" + CODE_COMMON_SUFFIX;
                public const string RED = ESCAPE + "41" + CODE_COMMON_SUFFIX;
                public const string GREEN = ESCAPE + "42" + CODE_COMMON_SUFFIX;
                public const string YELLOW = ESCAPE + "43" + CODE_COMMON_SUFFIX;
                public const string BLUE = ESCAPE + "44" + CODE_COMMON_SUFFIX;
                public const string MAGENTA = ESCAPE + "45" + CODE_COMMON_SUFFIX;
                public const string CYAN = ESCAPE + "46" + CODE_COMMON_SUFFIX;
                public const string WHITE = ESCAPE + "47" + CODE_COMMON_SUFFIX;
            }

            public static class BGL
            {
                public const string GRAY = ESCAPE + "100" + CODE_COMMON_SUFFIX;
                public const string RED = ESCAPE + "101" + CODE_COMMON_SUFFIX;
                public const string GREEN = ESCAPE + "102" + CODE_COMMON_SUFFIX;
                public const string YELLOW = ESCAPE + "103" + CODE_COMMON_SUFFIX;
                public const string BLUE = ESCAPE + "104" + CODE_COMMON_SUFFIX;
                public const string MAGENTA = ESCAPE + "105" + CODE_COMMON_SUFFIX;
                public const string CYAN = ESCAPE + "106" + CODE_COMMON_SUFFIX;
                public const string WHITE = ESCAPE + "107" + CODE_COMMON_SUFFIX;
            }
        }
    }

    public const string @RST = CODE_COLOR_RESET;
    public const string @NR = "";  // No Reset - for remove color reset at end
    public const string @PREVCOL = "PREVCOL";

    public const string @BLDOFF = CODE_BOLD_OFF;
    public const string @ITLOFF = CODE_ITALIC_OFF;
    public const string @UNLOFF = CODE_UNDERLINE_OFF;

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    public static Type AnsiColorsIdClass = typeof(AnsiConsts);

    public static class @FG
    {
        public static string Black(object s, string r = @RST) =>
            Render(Colors.FG.BLACK, s, r);
        public static string Red(object s, string r = @RST) =>
            Render(Colors.FG.RED, s, r);
        public static string Green(object s, string r = @RST) =>
            Render(Colors.FG.GREEN, s, r);
        public static string Yellow(object s, string r = @RST) =>
            Render(Colors.FG.YELLOW, s, r);
        public static string Blue(object s, string r = @RST) =>
            Render(Colors.FG.BLUE, s, r);
        public static string Magenta(object s, string r = @RST) =>
            Render(Colors.FG.MAGENTA, s, r);
        public static string Cyan(object s, string r = @RST) =>
            Render(Colors.FG.CYAN, s, r);
        public static string White(object s, string r = @RST) =>
            Render(Colors.FG.WHITE, s, r);
    }

    public static class @FGL
    {
        public static string Gray(object s, string r = @RST) =>
            Render(Colors.FGL.GRAY, s, r);
        public static string Red(object s, string r = @RST) =>
            Render(Colors.FGL.RED, s, r);
        public static string Green(object s, string r = @RST) =>
            Render(Colors.FGL.GREEN, s, r);
        public static string Yellow(object s, string r = @RST) =>
            Render(Colors.FGL.YELLOW, s, r);
        public static string Blue(object s, string r = @RST) =>
            Render(Colors.FGL.BLUE, s, r);
        public static string Magenta(object s, string r = @RST) =>
            Render(Colors.FGL.MAGENTA, s, r);
        public static string Cyan(object s, string r = @RST) =>
            Render(Colors.FGL.CYAN, s, r);
        public static string White(object s, string r = @RST) =>
            Render(Colors.FGL.WHITE, s, r);
    }

    public static class @BG
    {
        public static string Black(object s, string r = @RST) =>
            Render(Colors.BG.BLACK, s, r);
        public static string Red(object s, string r = @RST) =>
            Render(Colors.BG.RED, s, r);
        public static string Green(object s, string r = @RST) =>
            Render(Colors.BG.GREEN, s, r);
        public static string Yellow(object s, string r = @RST) =>
            Render(Colors.BG.YELLOW, s, r);
        public static string Blue(object s, string r = @RST) =>
            Render(Colors.BG.BLUE, s, r);
        public static string Magenta(object s, string r = @RST) =>
            Render(Colors.BG.MAGENTA, s, r);
        public static string Cyan(object s, string r = @RST) =>
            Render(Colors.BG.CYAN, s, r);
        public static string White(object s, string r = @RST) =>
            Render(Colors.BG.WHITE, s, r);
    }

    public static class @BGL
    {
        public static string Gray(object s, string r = @RST) =>
            Render(Colors.BGL.GRAY, s, r);
        public static string Red(object s, string r = @RST) =>
            Render(Colors.BGL.RED, s, r);
        public static string Green(object s, string r = @RST) =>
            Render(Colors.BGL.GREEN, s, r);
        public static string Yellow(object s, string r = @RST) =>
            Render(Colors.BGL.YELLOW, s, r);
        public static string Blue(object s, string r = @RST) =>
            Render(Colors.BGL.BLUE, s, r);
        public static string Magenta(object s, string r = @RST) =>
            Render(Colors.BGL.MAGENTA, s, r);
        public static string Cyan(object s, string r = @RST) =>
            Render(Colors.BGL.CYAN, s, r);
        public static string White(object s, string r = @RST) =>
            Render(Colors.BGL.WHITE, s, r);
    }

    public static string AnsiEsc(byte code) => 
        IsAnsiEnabled ? ESCAPE + code + CODE_COMMON_SUFFIX : "";
    public static string AnsiSeq(string code, object s, string r = @RST) =>
        Render(code, s, r);
    public static string AnsiSeq(byte code, object s, string r = @RST) =>
        Render(ESCAPE + code + CODE_COMMON_SUFFIX, s, r);

    public static string Rgb(int rgb, object s, string r = @RST)
    {
        var code = $"{(rgb & 0xFF0000) >> 16};{(rgb & 0xFF00) >> 8};{rgb & 0xFF}";
        return Render(ESCAPE_RGB + code + CODE_COMMON_SUFFIX, s, r);
    }
    public static string Rgb(int rgb, FormattableString s, string r = @RST)
    {
        var code = $"{(rgb & 0xFF0000) >> 16};{(rgb & 0xFF00) >> 8};{rgb & 0xFF}";
        return AnsiFormat(ESCAPE_RGB + code + CODE_COMMON_SUFFIX, s, r);
    }

    public static string @Flag(bool value, string @true = "Yes", string @false = "No") =>
        Render(GetColorIdCode(value ? "COLOR_TRUE" : "COLOR_FALSE"), value ? @true : @false, @RST);

    public static string @Bld(object? s = default, string r = @BLDOFF) =>
        Render(CODE_BOLD_ON, s, r);
    public static string @Itl(object? s = default, string r = @ITLOFF) =>
        Render(CODE_ITALIC_ON, s, r);
    public static string @Unl(object? s = default, string r = @UNLOFF) =>
        Render(CODE_UNDERLINE_ON, s, r);

    public static string @Name(object? s = default, string r = @RST) =>
        Render(GetColorIdCode("COLOR_NAME"), s, r);


    public static string @Id(string? s = default, string r = @RST) =>
        Render(GetColorIdCode("COLOR_ID"), s, r);


    public static string @Id(int s, string r = @RST) =>
        Render(GetColorIdCode("COLOR_ID"), s, r);

    public static string @Id(IntPtr s, string r = @RST) =>
        Render(GetColorIdCode("COLOR_ID"), "0x" + s.ToString("X"), r);


    public static string @Value<T>(T s, string r = @RST) =>
        Render(GetColorIdCode("COLOR_VALUE"), s, r);


    public static string @Bad(object? s = default, string r = @RST) =>
        Render(GetColorIdCode("COLOR_BAD"), s, r);

    public static string @Good(object? s = default, string r = @RST) =>
        Render(GetColorIdCode("COLOR_GOOD"), s, r);


    public static string @Error(object? s = default, string r = @RST) =>
        Render(GetColorIdCode("COLOR_BAD"), s, r);

    public static string @Warning(object? s = default, string r = @RST) =>
        Render(GetColorIdCode("COLOR_WARNING"), s, r);

    public static string @Except(object? s = default, string r = @RST) =>
        Render(GetColorIdCode("COLOR_EXCEPTION"), s, r);

    public static bool IsAnsiEnabled { get; private set; } = false;

    public unsafe static bool AnsiInitialize()
    {
        if (IsAnsiEnabled)
            return true;

        var hOut = GetStdHandle(STD.STD_OUTPUT_HANDLE);
        int oldMode = 0;
        if (!GetConsoleMode(hOut, (uint*)&oldMode))
            return false;

        oldMode = oldMode | ENABLE.ENABLE_VIRTUAL_TERMINAL_PROCESSING;
        if (!SetConsoleMode(hOut, *(uint*)&oldMode))
            return false;
        IsAnsiEnabled = true;
        return true;
    }

    public static void AnsiDisable() => IsAnsiEnabled = false;

    private static (int, int) savedCursorPos = (0, 0);
    public static void SaveCursorPos() => savedCursorPos = GetCursorPos();
    public static void RestoreCursorPos() => SetCursorPos(savedCursorPos);

    public static (int, int) GetCursorPos() => 
        Console.GetCursorPosition();
    public static void SetCursorPos((int, int) pos) => 
        Console.SetCursorPosition(pos.Item1, pos.Item2);

    public static void AnsiWrite(FormattableString text, string reset = @RST)
    {
        if (!IsAnsiEnabled)
        {
            Console.Write(text);
            return;
        }

        if (text.ArgumentCount > 1)
            Console.Write(AnsiFormat(@RST, text) + reset);
        else
            Console.Write(text + reset);
    }

    public static void AnsiWrite(byte code, FormattableString text, string reset = @RST)
    {
        if (!IsAnsiEnabled)
        {
            Console.Write(text);
            return;
        }

        var color = ESCAPE + code + CODE_COMMON_SUFFIX;
        Console.Write(AnsiFormat(color, text) + reset);
    }

    public static void AnsiWrite(Func<object, string, string> colorfunc, FormattableString text, string reset = @RST)
    {
        if (!IsAnsiEnabled)
        {
            Console.Write(text);
            return;
        }

        var color = colorfunc(null!, "");
        Console.Write(AnsiFormat(color, text) + reset);
    }

    public static void AnsiPrint(FormattableString text, string reset = @RST)
    {
        if (!IsAnsiEnabled)
        {
            Console.WriteLine(text);
            return;
        }

        if (text.ArgumentCount > 1)
            Console.WriteLine(AnsiFormat(@RST, text) + reset);
        else
            Console.WriteLine(text + reset);
    }

    public static void AnsiPrint(object text, string reset = @RST)
    {
        if (!IsAnsiEnabled)
        {
            Console.WriteLine(text);
            return;
        }

        Console.WriteLine(text + reset);
    }

    public static void AnsiPrint(byte code, FormattableString text, string reset = @RST)
    {
        if (!IsAnsiEnabled)
        {
            Console.WriteLine(text);
            return;
        }

        var color = ESCAPE + code + CODE_COMMON_SUFFIX;
        Console.WriteLine(AnsiFormat(color, text) + reset);
    }

    public static void AnsiPrint(Func<object, string, string> colorfunc, FormattableString text, string reset = @RST)
    {
        if (!IsAnsiEnabled)
        {
            Console.WriteLine(text);
            return;
        }

        var color = colorfunc(null!, "");
        Console.WriteLine(AnsiFormat(color, text) + reset);
    }

    public static string AnsiFormat(FormattableString text)
    {
        if (!IsAnsiEnabled)
            return text.ToString();
        return AnsiFormat(@RST, text);
    }

    public static string AnsiFormat(string color, FormattableString text, string reset)
    {
        if (!IsAnsiEnabled)
            return text.ToString();
        return AnsiFormat(color, text) + reset;
    }

    public static string AnsiFormat(string color, FormattableString text)
    {
        if (!IsAnsiEnabled)
            return text.ToString();
        var args = text.GetArguments();
        var activeColor = color;
        bool origColorIsRst = activeColor == @RST;
        bool colorIsRst = origColorIsRst;
        try
        {
            for (int i = 0; i < args.Length; i++)
            {
                string arg = (string)args[i]!;
                if (arg.EndsWith(@RST))
                {
                    if (!colorIsRst)
                        args[i] = arg[..^@RST.Length] + activeColor;
                }
                else
                {
                    if (!arg.Contains(@RST))
                    {
                        var roff = arg.LastIndexOf(ESCAPE);
                        if (roff >= 0)
                        {
                            activeColor = arg[roff..(arg.IndexOf("m", roff) + 1)];
                            colorIsRst = activeColor == @RST;
                        }
                    }
                    else if (!colorIsRst)
                    {
                        var roff = arg.LastIndexOf(@RST);
                        args[i] = arg[..roff] + activeColor + arg[(roff + @RST.Length)..];
                    }
                }

            }
        }
        catch (Exception)
        {
#if DEBUG
            throw;
#endif
        }
        if (origColorIsRst)
            return text.ToString();
        return color + text.ToString();

    }

    private static string Render(string code, object? text, string reset)
    {
        if (!IsAnsiEnabled)
            return $"{text}";
        
        try
        {
            if (text == null && reset == @RST) // empty call == just permanent color change (for reset need manually add @RST to output)
            {
                return code;
            }
        } catch (RuntimeBinderException) {
            // skip, text != null this way, for mutable types like JsonElement witch cant be null
        }
        return code + text + reset;
    }

    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(AnsiConsts))]
    private static string GetColorIdCode(string colorId) => (string)AnsiColorsIdClass.GetField(colorId)!.GetValue(null)!;
}
