using System.Runtime.InteropServices;
using System.Text;

namespace Crt240pSwitch;

internal static class NvApiBridge
{
    private const int NVAPI_OK = 0;
    private const uint ID_NvAPI_Initialize = 0x0150E828;
    private const uint ID_NvAPI_Unload = 0xD22BDD7E;
    private const uint ID_NvAPI_GetErrorMessage = 0x6C2D048C;
    private const uint ID_NvAPI_DISP_GetDisplayIdByDisplayName = 0xAE457190;
    private const uint ID_NvAPI_DISP_TryCustomDisplay = 0x1F7DB630;
    private const uint ID_NvAPI_DISP_SaveCustomDisplay = 0x49882876;
    private const uint ID_NvAPI_DISP_RevertCustomDisplayTrial = 0xCBBD40F0;

    [DllImport("nvapi64.dll", EntryPoint = "nvapi_QueryInterface", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr NvApiQueryInterface(uint id);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NvAPI_Initialize_t();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NvAPI_Unload_t();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NvAPI_GetErrorMessage_t(int nr, [Out] byte[] szDesc);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NvAPI_DISP_GetDisplayIdByDisplayName_t([MarshalAs(UnmanagedType.LPStr)] string displayName, out uint displayId);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NvAPI_DISP_TryCustomDisplay_t([In] uint[] pDispIds, uint count, [In] NV_CUSTOM_DISPLAY_V1[] pCustDisp);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NvAPI_DISP_SaveCustomDisplay_t([In] uint[] pDispIds, uint count, uint isThisOutputIdOnly, uint isThisMonitorIdOnly);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int NvAPI_DISP_RevertCustomDisplayTrial_t([In] uint[] pDispIds, uint count);

    [StructLayout(LayoutKind.Sequential)]
    private struct NV_VIEWPORTF { public float x, y, w, h; }

    [StructLayout(LayoutKind.Sequential)]
    private struct NV_TIMINGEXT
    {
        public uint flag;
        public ushort rr;
        public uint rr_x1k;
        public uint aspect;
        public ushort rep;
        public uint status;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 40)]
        public byte[] name;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NV_TIMING
    {
        public ushort HVisible, HBorder, HFrontPorch, HSyncWidth, HTotal;
        public byte HSyncPol;
        public ushort VVisible, VBorder, VFrontPorch, VSyncWidth, VTotal;
        public byte VSyncPol;
        public ushort interlaced;
        public uint pclk;
        public NV_TIMINGEXT etc;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NV_CUSTOM_DISPLAY_V1
    {
        public uint version;
        public uint width, height, depth, colorFormat;
        public NV_VIEWPORTF srcPartition;
        public float xRatio, yRatio;
        public NV_TIMING timing;
        public uint hwModeSetOnly;
    }

    private static T GetDelegate<T>(uint id) where T : Delegate
    {
        IntPtr ptr = NvApiQueryInterface(id);
        if (ptr == IntPtr.Zero) throw new EntryPointNotFoundException($"NVAPI 0x{id:X8} no disponible.");
        return Marshal.GetDelegateForFunctionPointer<T>(ptr);
    }

    private static string GetStatusText(int status)
    {
        try
        {
            var fn = GetDelegate<NvAPI_GetErrorMessage_t>(ID_NvAPI_GetErrorMessage);
            var buf = new byte[64];
            if (fn(status, buf) == NVAPI_OK)
                return $"{Encoding.ASCII.GetString(buf).TrimEnd('\0')} ({status})";
        }
        catch { }
        return $"NVAPI_STATUS ({status})";
    }

    private static NV_CUSTOM_DISPLAY_V1 BuildTiming(
        string name, uint w, uint h,
        ushort hFp, ushort hSw, ushort hTot,
        ushort vFp, ushort vSw, ushort vTot,
        uint pclk10kHz, uint rrX1k)
    {
        var nameBytes = new byte[40];
        var ascii = Encoding.ASCII.GetBytes(name);
        Array.Copy(ascii, nameBytes, Math.Min(ascii.Length, 39));

        return new NV_CUSTOM_DISPLAY_V1
        {
            version = (uint)Marshal.SizeOf<NV_CUSTOM_DISPLAY_V1>() | (1u << 16), // 144 bytes (0x00010090)
            width = w, height = h, depth = 32, colorFormat = 0,
            srcPartition = new NV_VIEWPORTF { x = 0f, y = 0f, w = 1f, h = 1f },
            xRatio = 1f, yRatio = 1f,
            timing = new NV_TIMING
            {
                HVisible = (ushort)w, HBorder = 0, HFrontPorch = hFp, HSyncWidth = hSw, HTotal = hTot, HSyncPol = 1,
                VVisible = (ushort)h, VBorder = 0, VFrontPorch = vFp, VSyncWidth = vSw, VTotal = vTot, VSyncPol = 1,
                interlaced = 0, pclk = pclk10kHz,
                etc = new NV_TIMINGEXT { flag = 0, rr = 60, rr_x1k = rrX1k, aspect = 0, rep = 1, status = 0, name = nameBytes }
            },
            hwModeSetOnly = 0
        };
    }

    public static (bool ok, string message) TryApply2560x224(string gdiDisplayName)
    {
        var cust = BuildTiming("CRT_2560x224p", 2560, 224, 104, 240, 3264, 11, 3, 262, 5130, 59988);
        return RunTryOnly(gdiDisplayName, cust);
    }

    public static (bool ok, string message) TryAndConfirmMode(
        string gdiDisplayName, bool is224p, Func<bool> confirmCallback)
    {
        var cust = is224p
            ? BuildTiming("CRT_2560x224p", 2560, 224, 104, 240, 3264, 11, 3, 262, 5130, 59988)
            : BuildTiming("CRT_2560x240p", 2560, 240, 104, 240, 3256, 3, 3, 262, 5118, 59995);

        try
        {
            var init = GetDelegate<NvAPI_Initialize_t>(ID_NvAPI_Initialize);
            var unload = GetDelegate<NvAPI_Unload_t>(ID_NvAPI_Unload);
            if (init() != NVAPI_OK) return (false, "No se pudo inicializar nvapi64.dll.");

            try
            {
                var getDispId = GetDelegate<NvAPI_DISP_GetDisplayIdByDisplayName_t>(ID_NvAPI_DISP_GetDisplayIdByDisplayName);
                var tryCust = GetDelegate<NvAPI_DISP_TryCustomDisplay_t>(ID_NvAPI_DISP_TryCustomDisplay);
                var saveCust = GetDelegate<NvAPI_DISP_SaveCustomDisplay_t>(ID_NvAPI_DISP_SaveCustomDisplay);
                var revertCust = GetDelegate<NvAPI_DISP_RevertCustomDisplayTrial_t>(ID_NvAPI_DISP_RevertCustomDisplayTrial);

                int stId = getDispId(gdiDisplayName, out uint dispId);
                if (stId != NVAPI_OK || dispId == 0)
                    return (false, $"NVAPI no encontró DisplayId para {gdiDisplayName} ({GetStatusText(stId)}).");

                var dispIds = new[] { dispId };
                int res = tryCust(dispIds, 1, new[] { cust });
                if (res != NVAPI_OK)
                    return (false, $"NvAPI_DISP_TryCustomDisplay falló: {GetStatusText(res)}.");

                if (confirmCallback())
                {
                    int saveRes = saveCust(dispIds, 1, 1, 1);
                    return saveRes == NVAPI_OK
                        ? (true, $"Modo {cust.width}×{cust.height} @ 60 Hz guardado en NVIDIA vía NVAPI.")
                        : (false, $"TryOK pero SaveCustomDisplay devolvió: {GetStatusText(saveRes)}.");
                }
                else
                {
                    revertCust(dispIds, 1);
                    return (false, "Prueba revertida automáticamente (watchdog de 15 segundos).");
                }
            }
            finally { unload(); }
        }
        catch (Exception ex) { return (false, $"Error NVAPI: {ex.Message}"); }
    }

    private static (bool ok, string message) RunTryOnly(string gdiDisplayName, NV_CUSTOM_DISPLAY_V1 cust)
    {
        try
        {
            var init = GetDelegate<NvAPI_Initialize_t>(ID_NvAPI_Initialize);
            var unload = GetDelegate<NvAPI_Unload_t>(ID_NvAPI_Unload);
            if (init() != NVAPI_OK) return (false, "No se pudo inicializar nvapi64.dll.");

            try
            {
                var getDispId = GetDelegate<NvAPI_DISP_GetDisplayIdByDisplayName_t>(ID_NvAPI_DISP_GetDisplayIdByDisplayName);
                var tryCust = GetDelegate<NvAPI_DISP_TryCustomDisplay_t>(ID_NvAPI_DISP_TryCustomDisplay);

                int stId = getDispId(gdiDisplayName, out uint dispId);
                if (stId != NVAPI_OK || dispId == 0)
                    return (false, $"NVAPI no encontró DisplayId para {gdiDisplayName}.");

                int res = tryCust(new[] { dispId }, 1, new[] { cust });
                return res == NVAPI_OK
                    ? (true, $"Aplicado {cust.width}×{cust.height} @ 60 Hz vía NVAPI en {gdiDisplayName}.")
                    : (false, $"NVAPI TryCustomDisplay falló: {GetStatusText(res)}.");
            }
            finally { unload(); }
        }
        catch (Exception ex) { return (false, $"Error NVAPI: {ex.Message}"); }
    }
}
