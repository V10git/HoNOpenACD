namespace HoNOpenACD;

internal static class Consts
{
    internal const string PROCESS_NAME = "hon";
    internal static readonly string[] REQUIRED_MODULES = { EXPORTS.K2_DLL, EXPORTS.GS_DLL };

    internal static class EXPORTS
    {
        internal const string GS_DLL = "game_shared_x64.dll";
        internal static class GS
        {
            internal const string CPlayer__ZoomIn = "?ZoomIn@CPlayer@@QEAAXXZ";
            internal const string CPlayer__ZoomOut = "?ZoomOut@CPlayer@@QEAAXXZ";
            internal const string CPlayer__SetupCamera = "?SetupCamera@CPlayer@@QEAAXAEAVCCamera@@AEBV?$CVec3@M@@1@Z";
        }

        internal const string K2_DLL = "k2_x64.dll";
        internal static class K2
        {
            internal const string g_pConsole = "?g_pConsole@@3PEAVCConsole@@EA";
            internal const string CConsole__Execute = "?Execute@CConsole@@QEAAXAEBV?$basic_string@_WU?$char_traits@_W@std@@V?$K2StringAllocator@_W@@@std@@I@Z";
            internal const string CWaterMap__GetRegionAlphaFlowmap = "?GetRegionAlphaFlowmap@CWaterMap@@QEBA_NHHHHPEAV?$CVec4@E@@H@Z";
        }
    }

    internal static class OFFSETS
    {
        internal static class CPlayer
        {
            internal const ushort fRenderCameraDistance = 0x118; // CPlayer::m_fRenderCameraDistance offset
        }
    }
}
