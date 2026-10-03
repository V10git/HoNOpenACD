using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Iced.Intel;
using static Iced.Intel.AssemblerRegisters;
using UniCheat;
using V10Sharp.ExtProcess.Windows;
using V10Sharp.Iced;
using static V10Sharp.ExtConsole.Ansi;
using static HoNOpenACD.Consts;

using CVarPatternList = System.Collections.Generic.IReadOnlyDictionary<string, HoNOpenACD.CVarPattern>;


namespace HoNOpenACD;

internal unsafe class CameraDistance : BaseScript
{
    private const int MIN_CODE_SIZE = 512;
    private const float DEFAULT_MIN_CAMERA_DISTANCE = 600f;
#if BUILD_REBORN
    private const float DEFAULT_MAX_CAMERA_DISTANCE = 3300f;
#else
    private const float DEFAULT_MAX_CAMERA_DISTANCE = 2100f;
#endif
    private const float CAMERA_DISTANCE_CHANGE_STEP = 180f;

    public new class ScriptConfig
    {
        public float MaxCameraDistance { get; set; } = 4500f;
        public string ExecuteAfterInject { get; set; } = "echo ^009ACD ^900Loaded";
    }


    private new ScriptConfig Config => (ScriptConfig)base.Config;
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    public override Type ConfigType => typeof(ScriptConfig);

    private Label lbZoomIn, lbZoomOut, lbSetupCamera;
    private IntPtr orgZoomIn, orgZoomOut, orgSetupCamera;

    private float DefaultMaxCameraDistance = DEFAULT_MAX_CAMERA_DISTANCE;

    private readonly CVarPatternList CVARS_PATTERNS = new Dictionary<string, CVarPattern>()
    {
        // cvar g_camDistanceMax pattern:
        //1800353ff  f30f7f4c2440       movdqu xmmword[rsp + 0x40], xmm1
        //180035405  41b810000000       mov     r8d, 0x10
        //18003540b  488d15366fac00     lea     rdx, [rel data_180afc348] { u"g_camDistanceMax"}
        //180035412  488d4c2430         lea     rcx, [rsp+0x30 { var_28}]
        //180035417  e8b4025d00         call    sub_1806056d0
        //18003541c  90                 nop
        //18003541d  c644242802         mov     byte[rsp + 0x28 { var_30}], 0x2
        //180035422  488b05573ea700     mov     rax, qword[rel DefaultCvar_Cmd]
        //180035429  4889442420         mov qword[rsp + 0x20 { var_38}], rax
        //18003542e  41b903000000       mov     r9d, 0x3
        //180035434  41b808480000       mov     r8d, 0x4808
        //18003543a  488d542430         lea rdx, [rsp + 0x30 { var_28}]
        //18003543f  488d0dca2fbe00     lea     rcx, [rel g_camDistanceMax]
        //180035446  ff150c3ea700       call    qword[rel ICvar::ICvar]
        //{ "g_camDistanceMax", new CVarPattern("f3 0f 7f 4c 24 ? 41 b8 ? 00 00 00 48 8d 15 ? ? ? ? 48 8d 4c 24", 0xC, 0x40) },
        { "g_camDistanceMax", new(EXPORTS.GS_DLL, "f3 0f 7f 4c 24 ? 41 b8 10 00 00 00 48 8d 15 ? ? ? ? 48 8d 4c 24 ? e8", 0xC, 0x40) },

        // cvar scene_fogFar pattern:
        //18023a6ff  f30f7f4c2440       movdqu  xmmword [rsp+0x40 {arg_40}], xmm1
        //18023a705  41b80c000000       mov     r8d, 0xc
        //18023a70b  488d156eb4d001     lea     rdx, [rel data_181f45b80]  {u"scene_fogFar"}
        //18023a712  488d4c2430         lea     rcx, [rsp+0x30 {arg_30}]
        //18023a717  e874c60800         call    sub_1802c6d90
        //18023a71c  90                 nop     
        //18023a71d  c644242800         mov     byte [rsp+0x28 {arg_28}], 0x0
        //18023a722  48c7442420000000…  mov     qword [rsp+0x20 {arg_20}], 0x0
        //18023a72b  41b903000000       mov     r9d, 0x3
        //18023a731  41b808200000       mov     r8d, 0x2008
        //18023a737  488d542430         lea     rdx, [rsp+0x30 {arg_30}]
        //18023a73c  488d0d3df61b02     lea     rcx, [rel data_1823f9d80]
        //18023a743  e8d83b2d00         call    ICvar::ICvar
        { "scene_fogFar", new(EXPORTS.K2_DLL, "f3 0f 7f 4c 24 ? 41 b8 0c 00 00 00 48 8d 15 ? ? ? ? 48 8d 4c 24 ? e8", 0xC, 0x3D) }
    };

    private HoN_CVar<float> CV_scene_fogFar = HoN_CVar<float>.INVALID_CVAR;

    private bool TryGetCvarFromPattern<T>(string name, CVarPattern pattern, out HoN_CVar<T> cvar, Func<string, bool>? waiter = null) where T : unmanaged
    {
        IntPtr cached = OffsetsCache[name];
        var newOffset = HoN_CVar<T>.CreateFromPattern(Process, name, pattern, out cvar, cached, waiter);
        OffsetsCache[name] = newOffset;
        return newOffset != IntPtr.Zero;
    }

    private bool TryGetCvarFromPattern<T>(string name, out HoN_CVar<T> cvar, Func<string, bool>? waiter = null) where T : unmanaged =>
        TryGetCvarFromPattern(name, CVARS_PATTERNS[name], out cvar, waiter);

    public override object BuildDefaultConfig()
    {
        var config = (ScriptConfig)base.BuildDefaultConfig();
        while (true)
        {
            float val = config.MaxCameraDistance;
            AnsiWrite(@FGL.Magenta, $"Enter max camera distance (or press {FG.White("ENTER")} for default [{@Value(val)}]): ");
            
            var s = Console.ReadLine();
            if (s == null || s == string.Empty || float.TryParse(s, out val))
            {
                if (val > DefaultMaxCameraDistance)
                {
                    AnsiPrint($"New max camera distance = {@Value(val)}");
                    config.MaxCameraDistance = val;
                    return config;
                }
            }
            AnsiPrint(@Error($"Invalid value {@Value(s)}. Try again."));
        }
    }

    public override bool Check(Func<string, bool>? waiter = null)
    {
        if (base.Check())
            return true;

        if (Config.MaxCameraDistance < DefaultMaxCameraDistance)
        {
            Engine.ShowError($"Camera distance in config {@Value(Config.MaxCameraDistance)} lower than default {@Good(DefaultMaxCameraDistance)}");
            return false;
        }

        if (!WaitModule(EXPORTS.GS_DLL, out var gsDll, waiter, true))
            return false;

#if BUILD_REBORN
        if (TryGetCvarFromPattern<float>("g_camDistanceMax", out var cv_g_camDistanceMax, waiter) && cv_g_camDistanceMax.IsValid)
        {
            var value = cv_g_camDistanceMax.Value;
            if (value >= 2100.0f && value <= 10000.0f)
            {
                if (value < DefaultMaxCameraDistance)
                {
                    AnsiPrint(@Warning($"Camera default max distance in-game {@Value(value)} is lower than ACD default {@Bad(DefaultMaxCameraDistance)}. This can lead to an account ban."));
                    DefaultMaxCameraDistance = value;
                }
            }
            else
            {
                Engine.ShowError($"Invalid {@Name("g_camDistanceMax")} cvar, required for check default max camera distance. This can lead to an account ban.");
            }
        }
        else
        {
            Engine.ShowError($"Cant find {@Name("g_camDistanceMax")} cvar, required for check default max camera distance. This can lead to an account ban.");
        }

        if (!TryGetCvarFromPattern("scene_fogFar", out CV_scene_fogFar, waiter) || !CV_scene_fogFar.IsValid)
        {
            AnsiPrint(@Warning($"Cant find {@Name("scene_fogFar")} cvar required for fog effects removal."));
        }
#endif

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
        var fMaxCamera_Default = asm.Variable(DefaultMaxCameraDistance);
        var fCameraStep = asm.Variable(CAMERA_DISTANCE_CHANGE_STEP);
        var fMaxCamera = asm.Variable(Config.MaxCameraDistance);
        var fActualCameraDistance = asm.Variable(DefaultMaxCameraDistance);
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

#if BUILD_REBORN
        // fix foggy effects in reborn on high distance
        if (CV_scene_fogFar.IsValid)
        {
            // backup regs
            asm.push(rax);
            asm.push(rbx);

            // replace scene_fogFar value for high distance
            asm.mov(rax, CV_scene_fogFar.ValuePtr);
            asm.mov(ebx, 0x469C4000); // 0x469C4000 is binary 20000.0f repr
            asm.mov(__qword_ptr[rax], ebx);

            // restore regs
            asm.pop(rbx);
            asm.pop(rax);

            AnsiPrint(@Good, $"Fog patch applied, cvar ptr {@Id(CV_scene_fogFar.Ptr)}");
        }
#endif

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
        // End New SetupCamera()
        // End Asm
    }

    protected override bool Inject()
    {
        var baseptr = Process.Alloc<byte>(MIN_CODE_SIZE);
        var compiled = asm.Compile(baseptr);
        Process.WriteMemory((void*)baseptr, compiled);
        AnsiPrint($"{Name} injected at {@Id(baseptr)}");

        Process.ApplyPatch((void*)orgZoomOut,     a => { a.jmp(compiled[lbZoomOut]); });
        Process.ApplyPatch((void*)orgZoomIn,      a => { a.jmp(compiled[lbZoomIn]); });
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
#else
        // fix clipping in reborn by cvar
        var CV_scene_farClip = HoN_CVar<float>.CreateFromExport(Process, EXPORTS.K2_DLL, "scene_farClip");
        if (CV_scene_farClip.IsValid)
        {
            var value = CV_scene_farClip.Value;
            if (value != 20000.0f)
            {
                AnsiPrint(@Good, $"{@Name("scene_farClip")} changed from {@Value(value)} to {@Value(20000.0)}, cvar ptr {@Id(CV_scene_farClip.ValuePtr)}");
                CV_scene_farClip.Value = 20000.0f;
            }
        }
        else
        {
            AnsiPrint(@Warning($"{@Name("scene_farClip")} cvar not found"));
        }

        // fix foggy effects in reborn on high distance
        // disabled now, using another way
        //var CV_scene_fogType = new HoN_CVar<int>(Process, EXPORTS.K2_DLL, "scene_fogType");
        //if (CV_scene_fogType.IsValid)
        //{
        //    CV_scene_fogType.Value = 0;
        //    AnsiPrint(@Good, $"scene_fogType changed to 0, cvar ptr {@Id(CV_scene_fogType.ValuePtr)}");
        //}
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
