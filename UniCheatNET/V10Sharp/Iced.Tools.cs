namespace Iced.Intel;

public static class Tools
{
    private const int HEXBYTES_COLUMN_BYTE_LENGTH = 16;

    private sealed class FormatterOutputImpl : FormatterOutput
    {
        public readonly List<(string text, FormatterTextKind kind)> List =
            new List<(string text, FormatterTextKind kind)>();
        public override void Write(string text, FormatterTextKind kind) => List.Add((text, kind));
    }

    public static void DumpAsm(byte[] codeBytes, long length, ulong rip)
    {
        var codeReader = new ByteArrayCodeReader(codeBytes);
        var decoder = Iced.Intel.Decoder.Create(64, codeReader);
        decoder.IP = rip;
        ulong endRip = decoder.IP + (uint)length;

        var instructions = new List<Instruction>();
        while (decoder.IP < endRip)
            instructions.Add(decoder.Decode());

        var formatter = new NasmFormatter();
        formatter.Options.DigitSeparator = "";
        formatter.Options.FirstOperandCharIndex = 10;
        var output = new FormatterOutputImpl();
        foreach (var instr in instructions)
        {
            output.List.Clear();
            // Don't use instr.ToString(), it allocates more, uses masm syntax and default options
            formatter.Format(instr, output);
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(instr.IP.ToString("X16"));
            Console.Write(" ");
            int instrLen = instr.Length;
            int byteBaseIndex = (int)(instr.IP - rip);
            for (int i = 0; i < instrLen; i++)
                Console.Write(codeBytes[byteBaseIndex + i].ToString("X2"));
            int missingBytes = HEXBYTES_COLUMN_BYTE_LENGTH - instrLen;
            for (int i = 0; i < missingBytes; i++)
                Console.Write("  ");
            Console.Write(" ");
            var len = 0;
            foreach (var (text, kind) in output.List)
            {
                Console.ForegroundColor = GetColor(kind);
                Console.Write(text);
                len += text.Length;
            }
            for (int i = 0; i < 40-len; i++)
                Console.Write(" ");
            Console.ForegroundColor = ConsoleColor.DarkYellow;

            for (int i = 0; i < instrLen; i++)
            {
                var ch = (char)codeBytes[byteBaseIndex + i];
                if ((byte)ch >= 32 && (byte)ch < 127)
                    Console.Write(ch);
                else if ((byte) ch == 0)
                {
                    Console.Write('.');
                } else
                    Console.Write(' ');
            }

            Console.WriteLine();
        }
        Console.ResetColor();
    }

    private static ConsoleColor GetColor(FormatterTextKind kind)
    {
        switch (kind)
        {
            case FormatterTextKind.Directive:
            case FormatterTextKind.Keyword:
                return ConsoleColor.Yellow;

            case FormatterTextKind.Prefix:
            case FormatterTextKind.Mnemonic:
                return ConsoleColor.Red;

            case FormatterTextKind.Register:
                return ConsoleColor.Magenta;

            case FormatterTextKind.Number:
                return ConsoleColor.Green;

            default:
                return ConsoleColor.White;
        }
    }
}
