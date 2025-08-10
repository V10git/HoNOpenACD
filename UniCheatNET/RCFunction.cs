using Iced.Intel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using V10Sharp.ExtProcess.Windows;
using V10Sharp.Iced;

namespace UniCheat;

public class RCFunction
{
    protected readonly Process process;
    protected readonly object?[] args;
    protected readonly Dictionary<string, object> regs = new();
    protected readonly Stack<object> stack = new();

    protected bool isPrepared = false;
    protected readonly Stack<string> freeRegsLong = new Stack<string>(["r9", "r8", "rdx", "rcx"]);
    protected readonly Stack<string> freeRegsfloat = new Stack<string>(["xmm3", "xmm2", "xmm1", "xmm0"]);
    protected byte freeRegsCount = 4;

    public readonly RCGenerator Generator;
    public CompiledResult? FullResult = null;

    public RCFunction(Process process, IntPtr func, params object?[]? args)
    {
        this.process = process;
        this.args = args ?? [];
        Generator = new RCGenerator(func, regs, stack);
    }

    public RCFunction(Process process, RCGenerator gen, params object?[]? args)
    {
        this.process = process;
        this.args = args ?? [];
        Generator = gen;
        Generator.Stack = stack;
        Generator.Regs = regs;
    }

    [MemberNotNull(nameof(FullResult))]
    public RCFunction Prepare()
    {
#pragma warning disable CS8774 // Member must have a non-null value when exiting.
        if (isPrepared)
            return this;
#pragma warning restore CS8774 // Member must have a non-null value when exiting.

        // x64 calling convention https://learn.microsoft.com/en-us/cpp/build/x64-calling-convention?view=msvc-170
        foreach (var arg in args)
        {
            object? value = arg;
            bool deref = false;
            if (arg != null && arg.GetType().IsArray && arg.GetType().GetElementType() != typeof(byte))
            {
                var arr = arg as Array;
                if (arr != null && arr.Length == 1)
                {
                    deref = true;
                    value = arr.GetValue(0);
                }
            }
                
            value = PrepareParamValue(value);
            if (value != null)
                AddParam(value, deref);
        }

        Generator.IsStackReversed = true;
        Generator.GenerateCode();
        FullResult = Generator.Compile();
        isPrepared = true;
        return this;
    }

    protected virtual dynamic? PrepareParamValue(object? arg)
    {
        if (arg == null)
            return (ulong)IntPtr.Zero;
        dynamic value = arg;
        if (arg is string)
            value = new RCVar((string)arg);
        else if (arg.GetType().IsArray)
            value = new RCVar((byte[])arg);
        else if (arg is IntPtr)
            value = (ulong)(IntPtr)arg;
        return value;
    }

    protected virtual void AddParam(object value, bool bNeedDeref)
    {
        if (freeRegsCount > 0)
        {
            freeRegsCount--;
            string reg = value is float ? freeRegsfloat.Pop() : freeRegsLong.Pop();
            if (bNeedDeref)
                reg = $"[{reg}]";
            regs.Add(reg, value);
        }
        else
            stack.Push(value);
    }

    public virtual RCFunction Call()
    {
        Prepare();
        ArgumentNullException.ThrowIfNull((object)FullResult!);

        if (!TryCall(out var result))
            throw new InvalidOperationException("unexpected error.");
        return this;
    }

    public virtual RCFunction Call(out IntPtr result)
    {
        Prepare();
        ArgumentNullException.ThrowIfNull((object)FullResult!);

        if (!TryCall(out result))
            throw new InvalidOperationException("unexpected error.");
        return this;
    }

    public virtual RCFunction Call(out IntPtr result, out bool success)
    {
        Prepare();
        ArgumentNullException.ThrowIfNull((object)FullResult!);

        success = false;
        if (!TryCall(out result))
            return this;
        success = true;
        return this;
    }

    public virtual bool TryCall(out IntPtr result)
    {
        Prepare();
        if (!isPrepared)
            throw new InvalidOperationException($"cant prepare call {this}");

        result = IntPtr.Zero;
        if (FullResult == null)
            return false;

        var ptr = Alloc();
        try
        {
            Write(ptr);
            Run(ptr);
            Read(ptr);
            result = (IntPtr)FullResult["result"];
        }
        finally
        {
            Free(ptr);
        }
        return true;
    }

    protected virtual IntPtr Alloc()
    {
        var ptr = process.Alloc<byte>(FullResult!.Length);
        foreach (var cv in Generator.Vars.Keys)
            cv.OnAllocated(ptr, FullResult);
        return ptr;
    }

    protected virtual void Write(IntPtr ptr)
    {
        process.WriteMemory(ptr, FullResult!);
    }

    protected unsafe virtual void Run(IntPtr ptr)
    {
        process.RunThread(ptr);
    }

    protected virtual void Read(IntPtr ptr)
    {
        process.ReadMemory(ptr, FullResult!);
    }

    protected virtual void Free(IntPtr ptr)
    {
        foreach (var cv in Generator.Vars.Keys)
            cv.OnBeforeDeallocated(ptr, FullResult!);
        process.Free(ptr);
    }

    public virtual IntPtr GetResult()
    {
        ArgumentNullException.ThrowIfNull((object)FullResult!);
        return (IntPtr)(ulong)FullResult["result"];
    }

    public T GetResult<T>(string label) where T: unmanaged
    {
        ArgumentNullException.ThrowIfNull((object)FullResult!);

        return FullResult.Get<T>(label);
    }

    public T GetResult<T>(Label label) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull((object)FullResult!);

        return FullResult.Get<T>(label);
    }

    public static IntPtr Call(Process process, IntPtr pFunc, params object?[]? args)
    {
        new RCFunction(process, pFunc, args).Call(out var callRes);
        return callRes;
    }

    public static IntPtr Call(Process process, IntPtr hModule, string function, params object?[]? args)
    {
        var pFunc = process.GetModuleExport(hModule, function);
        if (pFunc == IntPtr.Zero)
            new ArgumentException($"cant get {function} address");

        new RCFunction(process, pFunc, args).Call(out var callRes);
        return callRes;
    }

    public static IntPtr Call(Process process, string module, string function, params object?[]? args)
    {
        var hModule = process.GetModuleBase(module);
        if (hModule == IntPtr.Zero)
            new ArgumentException($"cant get {module} handle");

        var pFunc = process.GetModuleExport(hModule, function);
        if (pFunc == IntPtr.Zero)
            new ArgumentException($"cant get {function} address");

        new RCFunction(process, pFunc, args).Call(out var callRes);
        return callRes;
    }
}


