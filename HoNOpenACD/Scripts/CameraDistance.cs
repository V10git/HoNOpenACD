using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Iced.Intel;
using static Iced.Intel.AssemblerRegisters;
using V10Sharp.ExtProcess.Windows;
using V10Sharp.Iced;
using static V10Sharp.ExtConsole.Ansi;
using UniCheat;
using static HoNOpenACD.Consts;


namespace HoNOpenACD;

internal unsafe class CameraDistance : BaseScript
{
    private const int MIN_CODE_SIZE = 512;
    private const float DEFAULT_MIN_CAMERA_DISTANCE = 600f;
    private const float DEFAULT_MAX_CAMERA_DISTANCE = 2100f;
    private const float CAMERA_DISTANCE_CHANGE_STEP = 180f;

    public new class ScriptConfig
    {
        public float MaxCameraDistance { get; set; } = 3000f;
        public string ExecuteAfterInject { get; set; } = "echo ^009ACD ^900Loaded";
    }


    private new ScriptConfig Config => (ScriptConfig)base.Config;
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    public override Type ConfigType => typeof(ScriptConfig);

    private Label lbZoomIn, lbZoomOut, lbSetupCamera;
    private IntPtr orgZoomIn, orgZoomOut, orgSetupCamera;

    public override bool Check(Func<string,bool>? waiter = null)
    {
        if (base.Check())
            return true;

        if (Config.MaxCameraDistance < DEFAULT_MAX_CAMERA_DISTANCE)
        {
            Engine.ShowError($"Camera distance in config {@Value(Config.MaxCameraDistance)} lower than default {@Good(DEFAULT_MAX_CAMERA_DISTANCE)}");
            return false;
        }

        if (!WaitModule(EXPORTS.GS_DLL, out var gsDll, waiter, true))
            return false;
               
        if (!gsDll.TryGetExport(EXPORTS.GS.CPlayer__ZoomIn, out orgZoomIn) ||
            !gsDll.TryGetExport(EXPORTS.GS.CPlayer__ZoomOut, out orgZoomOut) ||
            !gsDll.TryGetExport(EXPORTS.GS.CPlayer__SetupCamera, out orgSetupCamera))
        {
            return false;
        }
        
        return Checked = true;
    }

    protected override void Prepare()
    {
        V10Sharp.Helpers.Repeat(asm.int3, 4);
        var fMinCamera_Default = asm.Variable(DEFAULT_MIN_CAMERA_DISTANCE);
        var fMaxCamera_Default = asm.Variable(DEFAULT_MAX_CAMERA_DISTANCE);
        var fCameraStep = asm.Variable(CAMERA_DISTANCE_CHANGE_STEP);
        var fMaxCamera = asm.Variable(Config.MaxCameraDistance);
        var fActualCameraDistance = asm.Variable(DEFAULT_MAX_CAMERA_DISTANCE);
        V10Sharp.Helpers.Repeat(asm.int3, 4);

        // CheckSyncWithCPlayer()
        var CheckSyncWithCPlayer = asm.Func();
        asm.movss(xmm0, __[fActualCameraDistance]);
        asm.comiss(xmm0, __[fMaxCamera_Default]);
        asm.ja(asm.@F); // ja CheckSyncWithCPlayer_Exit
        asm.movss(__[rcx + OFFSETS.CPlayer.fRenderCameraDistance], xmm0);
        // CheckSyncWithCPlayer_Exit:
        asm.AnonymousLabel();
        asm.ret();
        V10Sharp.Helpers.Repeat(asm.int3, 4);

        // New ZoomOut()
        lbZoomOut = asm.Func();
        asm.movss(xmm0, __[fActualCameraDistance]);
        asm.addss(xmm0, __[fCameraStep]);
        asm.comiss(xmm0, __[fMaxCamera]);
        asm.jna(asm.@F); // ja NewZoomOut_Exit
        asm.movss(xmm0, __[fMaxCamera]);
        asm.AnonymousLabel(); // NewZoomOut_Exit:
        asm.movss(__[fActualCameraDistance], xmm0);
        asm.call(CheckSyncWithCPlayer);
        asm.ret();
        V10Sharp.Helpers.Repeat(asm.int3, 4);

        // New ZoomIn()
        lbZoomIn = asm.Func();
        asm.movss(xmm0, __[fActualCameraDistance]);
        asm.subss(xmm0, __[fCameraStep]);
        asm.comiss(xmm0, __[fMinCamera_Default]);
        asm.ja(asm.@F); // ja NewZoomIn_Exit
        asm.movss(xmm0, __[fMinCamera_Default]);
        asm.AnonymousLabel(); // NewZoomIn_Exit:
        asm.movss(__[fActualCameraDistance], xmm0);
        asm.call(CheckSyncWithCPlayer);
        asm.ret();
        V10Sharp.Helpers.Repeat(asm.int3, 4);

        // New SetupCamera()
        asm.LabelHere(out var SCReturnToCaller);
        asm.dq(0);
        asm.LabelHere(out var BackupRenderCameraDistance);
        asm.dq(0);
        asm.LabelHere(out var BackupCPlayer);
        asm.dq(0);
        V10Sharp.Helpers.Repeat(asm.int3, 4);

        lbSetupCamera = asm.Func();
        // restore SetupCamera code
        asm.mov(r11, rsp);
        asm.mov(__qword_ptr[r11 + 8], rbx);
        asm.mov(__qword_ptr[r11 + 0x10], rbp);
        asm.mov(__qword_ptr[r11 + 0x18], rsi);
        asm.mov(__qword_ptr[r11 + 0x20], rdi);
        // backup stack
        asm.pop(__qword_ptr[SCReturnToCaller]);
        asm.push(__qword_ptr[rcx + OFFSETS.CPlayer.fRenderCameraDistance]);
        asm.pop(__qword_ptr[BackupRenderCameraDistance]);
        asm.push(rcx);
        asm.pop(__qword_ptr[BackupCPlayer]);

        // replace camera distance
        asm.push(__qword_ptr[fActualCameraDistance]);
        asm.pop(__qword_ptr[rcx + OFFSETS.CPlayer.fRenderCameraDistance]);

        // call to original func
        asm.call((ulong)orgSetupCamera + 0x13);

        // restore stack
        asm.push(__qword_ptr[BackupCPlayer]);
        asm.pop(rcx);
        asm.push(__qword_ptr[BackupRenderCameraDistance]);
        asm.pop(__qword_ptr[rcx + OFFSETS.CPlayer.fRenderCameraDistance]);
        asm.push(__qword_ptr[SCReturnToCaller]);
        asm.ret();
        V10Sharp.Helpers.Repeat(asm.int3, 4);
        // End Asm
    }

    protected override bool Inject()
    {
        var baseptr = Process.Alloc<byte>(MIN_CODE_SIZE);
        var compiled = asm.Compile(baseptr);
        Process.WriteMemory((void*)baseptr, compiled);
        AnsiPrint(@Good, $"{Name} injected at {@Id(baseptr)}");

        Process.ApplyPatch((void*)orgZoomOut,     a => { a.jmp(compiled[lbZoomOut]);     });
        Process.ApplyPatch((void*)orgZoomIn,      a => { a.jmp(compiled[lbZoomIn]);      });
        Process.ApplyPatch((void*)orgSetupCamera, a => { a.jmp(compiled[lbSetupCamera]); });

#if !BUILD_REBORN
        // fix kongor patch of GetRegionAlphaFlowmap
        var pGetRegionAlphaFlowmap = Process.GetModuleExport(EXPORTS.K2_DLL, EXPORTS.K2.CWaterMap__GetRegionAlphaFlowmap);
        if (pGetRegionAlphaFlowmap != IntPtr.Zero)
        {
            Process.ApplyPatch((void*)(pGetRegionAlphaFlowmap + 0xE92A), a =>
            {
                a.db([0x76, 0x1D, 0x48, 0x8B, 0x84, 0x24, 0xA0, 0x62, 0x00, 0x00, 0x6B, 
                      0x80, 0xE0, 0x00, 0x00, 0x00, 0xFF, 0x48, 0x8B, 0x8C, 0x24, 0xA0, 
                      0x62, 0x00, 0x00, 0x89, 0x81, 0xE0, 0x00, 0x00, 0x00]);
            });
        }
        else
        {
            Engine.ShowError($"{@Id("GetRegionAlphaFlowmap")} patch failed.");
            return false;
        }
#endif

        // execute command in console
        if (!string.IsNullOrEmpty(Config.ExecuteAfterInject))
        {
            var g_pConsole = Process.GetModuleExport(EXPORTS.K2_DLL, EXPORTS.K2.g_pConsole);
            var command = new HoN_wcstring(Config.ExecuteAfterInject);
            //RCall(EXPORTS.K2_DLL, EXPORTS.K2.CConsole__Execute, g_pConsole, command, null);
        }
        return true;
    }
}
