namespace V10Sharp.TerraFX;

public static class Extensions
{
    /// <summary>Warning! This function safe only for consts strings.</summary>
    public static unsafe char* ToPWChar(this string input)
    {
        //TODO: rewrite for use Marshal.StringToHGlobal + Marshal.FreeHGlobal
        ArgumentException.ThrowIfNullOrEmpty(input);
        fixed (char* pch = &input.GetPinnableReference())
            return pch;
    }

    /// <summary>Warning! This function safe only for consts strings.</summary>
    public static unsafe sbyte* ToPChar(this ReadOnlySpan<byte> input)
    {
        //TODO: rewrite for use Marshal.StringToHGlobalAnsi + Marshal.FreeHGlobal
        fixed (byte* pch = &input[0])
            return (sbyte*)pch;
    }
    
}
