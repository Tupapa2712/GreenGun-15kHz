using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;

namespace Crt240pSwitch;

internal sealed class MonitorEntry
{
    public string GdiName { get; init; } = "";
    public bool IsPrimary { get; init; }
    public string FriendlyName { get; init; } = "";
    public string DeviceInstanceId { get; init; } = "";
    public string RegistryDeviceParamsPath { get; init; } = "";
    public uint Width { get; init; }
    public uint Height { get; init; }
    public uint RefreshHz { get; init; }
    public double HFreqKHz { get; init; }
    public string Scanline { get; init; } = "";
    public byte[]? EdidBytes { get; init; }
    public int DisplayIndex => int.TryParse(GdiName.Replace(@"\\.\DISPLAY", ""), out int n) ? n : 2;
    public override string ToString() => $"{GdiName} — {(string.IsNullOrWhiteSpace(FriendlyName) ? "Monitor" : FriendlyName)} ({Width}×{Height}@{RefreshHz}Hz){(IsPrimary ? " [PRINCIPAL]" : "")}";
}

internal static class DisplayManager
{
    private const uint QDC_ONLY_ACTIVE_PATHS = 0x00000002;
    private const int ERROR_SUCCESS = 0;
    private const int ENUM_CURRENT_SETTINGS = -1;
    private const uint EDS_RAWMODE = 0x00000002;
    private const uint DISPLAY_DEVICE_ATTACHED_TO_DESKTOP = 0x00000001;
    private const uint DISPLAY_DEVICE_PRIMARY_DEVICE = 0x00000004;

    private const uint DM_POSITION = 0x00000020;
    private const uint DM_BITSPERPEL = 0x00040000;
    private const uint DM_PELSWIDTH = 0x00080000;
    private const uint DM_PELSHEIGHT = 0x00100000;
    private const uint DM_DISPLAYFLAGS = 0x00200000;
    private const uint DM_DISPLAYFREQUENCY = 0x00400000;

    private const uint CDS_UPDATEREGISTRY = 0x00000001;
    private const uint CDS_TEST = 0x00000002;
    private const uint CDS_NORESET = 0x10000000;
    private const int DISP_CHANGE_SUCCESSFUL = 0;

    private const uint DIF_PROPERTYCHANGE = 0x00000012;
    private const uint DICS_ENABLE = 0x00000001;
    private const uint DICS_DISABLE = 0x00000002;
    private const uint DICS_FLAG_GLOBAL = 0x00000001;

    private enum DISPLAYCONFIG_DEVICE_INFO_TYPE : uint { GET_SOURCE_NAME = 1, GET_TARGET_NAME = 2 }
    private enum DISPLAYCONFIG_SCANLINE_ORDERING : uint { UNSPECIFIED = 0, PROGRESSIVE = 1, INTERLACED = 2, INTERLACED_LOWER = 3 }
    private enum DISPLAYCONFIG_MODE_INFO_TYPE : uint { SOURCE = 1, TARGET = 2, DESKTOP_IMAGE = 3 }

    [StructLayout(LayoutKind.Sequential)] private struct LUID { public uint LowPart; public int HighPart; }
    [StructLayout(LayoutKind.Sequential)] private struct DISPLAYCONFIG_RATIONAL { public uint Numerator, Denominator; public double AsDouble() => Denominator == 0 ? 0 : (double)Numerator / Denominator; }
    [StructLayout(LayoutKind.Sequential)] private struct DISPLAYCONFIG_2DREGION { public uint cx, cy; }
    [StructLayout(LayoutKind.Sequential)] private struct DISPLAYCONFIG_PATH_SOURCE_INFO { public LUID adapterId; public uint id, modeInfoIdx, statusFlags; }
    [StructLayout(LayoutKind.Sequential)] private struct DISPLAYCONFIG_PATH_TARGET_INFO { public LUID adapterId; public uint id, modeInfoIdx, outputTechnology, rotation, scaling; public DISPLAYCONFIG_RATIONAL refreshRate; public DISPLAYCONFIG_SCANLINE_ORDERING scanLineOrdering; [MarshalAs(UnmanagedType.Bool)] public bool targetAvailable; public uint statusFlags; }
    [StructLayout(LayoutKind.Sequential)] private struct DISPLAYCONFIG_PATH_INFO { public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo; public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo; public uint flags; }
    [StructLayout(LayoutKind.Sequential)] private struct DISPLAYCONFIG_VIDEO_SIGNAL_INFO { public ulong pixelRate; public DISPLAYCONFIG_RATIONAL hSyncFreq, vSyncFreq; public DISPLAYCONFIG_2DREGION activeSize, totalSize; public uint videoStandard; public DISPLAYCONFIG_SCANLINE_ORDERING scanLineOrdering; }
    [StructLayout(LayoutKind.Sequential)] private struct DISPLAYCONFIG_TARGET_MODE { public DISPLAYCONFIG_VIDEO_SIGNAL_INFO targetVideoSignalInfo; }
    [StructLayout(LayoutKind.Sequential)] private struct POINTL { public int x, y; }
    [StructLayout(LayoutKind.Sequential)] private struct DISPLAYCONFIG_SOURCE_MODE { public uint width, height, pixelFormat; public POINTL position; }
    [StructLayout(LayoutKind.Explicit)] private struct DISPLAYCONFIG_MODE_INFO_UNION { [FieldOffset(0)] public DISPLAYCONFIG_TARGET_MODE targetMode; [FieldOffset(0)] public DISPLAYCONFIG_SOURCE_MODE sourceMode; }
    [StructLayout(LayoutKind.Sequential)] private struct DISPLAYCONFIG_MODE_INFO { public DISPLAYCONFIG_MODE_INFO_TYPE infoType; public uint id; public LUID adapterId; public DISPLAYCONFIG_MODE_INFO_UNION modeInfo; }
    [StructLayout(LayoutKind.Sequential)] private struct DISPLAYCONFIG_DEVICE_INFO_HEADER { public DISPLAYCONFIG_DEVICE_INFO_TYPE type; public uint size; public LUID adapterId; public uint id; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DISPLAYCONFIG_SOURCE_DEVICE_NAME
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string viewGdiDeviceName;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DISPLAYCONFIG_TARGET_DEVICE_NAME
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
        public uint flags, outputTechnology;
        public ushort edidManufactureId, edidProductCodeId;
        public uint connectorInstance;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string monitorFriendlyDeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string monitorDevicePath;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DISPLAY_DEVICE
    {
        public uint cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
        public uint StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
        public ushort dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
        public uint dmFields;
        public int dmPositionX, dmPositionY;
        public uint dmDisplayOrientation, dmDisplayFixedOutput;
        public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
        public ushort dmLogPixels;
        public uint dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
        public uint dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
    }

    [StructLayout(LayoutKind.Sequential)] private struct SP_DEVINFO_DATA { public uint cbSize; public Guid ClassGuid; public uint DevInst; public IntPtr Reserved; }
    [StructLayout(LayoutKind.Sequential)] private struct SP_CLASSINSTALL_HEADER { public uint cbSize, InstallFunction; }
    [StructLayout(LayoutKind.Sequential)] private struct SP_PROPCHANGE_PARAMS { public SP_CLASSINSTALL_HEADER ClassInstallHeader; public uint StateChange, Scope, HwProfile; }

    [DllImport("user32.dll")] private static extern int GetDisplayConfigBufferSizes(uint flags, out uint paths, out uint modes);
    [DllImport("user32.dll")] private static extern int QueryDisplayConfig(uint flags, ref uint paths, [Out] DISPLAYCONFIG_PATH_INFO[] p, ref uint modes, [Out] DISPLAYCONFIG_MODE_INFO[] m, IntPtr id);
    [DllImport("user32.dll")] private static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_SOURCE_DEVICE_NAME req);
    [DllImport("user32.dll")] private static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_TARGET_DEVICE_NAME req);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool EnumDisplayDevices(string? dev, uint idx, ref DISPLAY_DEVICE dd, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool EnumDisplaySettingsEx(string dev, int mode, ref DEVMODE dm, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int ChangeDisplaySettingsEx(string? dev, ref DEVMODE dm, IntPtr hwnd, uint flags, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int ChangeDisplaySettingsEx(string? dev, IntPtr dm, IntPtr hwnd, uint flags, IntPtr lParam);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr SetupDiCreateDeviceInfoList(IntPtr guid, IntPtr hwnd);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetupDiOpenDeviceInfo(IntPtr set, string instId, IntPtr hwnd, uint flags, ref SP_DEVINFO_DATA data);
    [DllImport("setupapi.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetupDiSetClassInstallParams(IntPtr set, ref SP_DEVINFO_DATA data, ref SP_PROPCHANGE_PARAMS p, uint size);
    [DllImport("setupapi.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetupDiCallClassInstaller(uint fn, IntPtr set, ref SP_DEVINFO_DATA data);
    [DllImport("setupapi.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);

    public static bool IsAdministrator()
    {
        using var id = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static List<MonitorEntry> EnumerateMonitors()
    {
        var result = new List<MonitorEntry>();
        if (GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out uint pCount, out uint mCount) != ERROR_SUCCESS)
            return result;

        var paths = new DISPLAYCONFIG_PATH_INFO[pCount];
        var modes = new DISPLAYCONFIG_MODE_INFO[mCount];
        if (QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref pCount, paths, ref mCount, modes, IntPtr.Zero) != ERROR_SUCCESS)
            return result;

        var gdiMap = new Dictionary<string, DISPLAY_DEVICE>(StringComparer.OrdinalIgnoreCase);
        for (uint i = 0; ; i++)
        {
            var dd = new DISPLAY_DEVICE { cb = (uint)Marshal.SizeOf<DISPLAY_DEVICE>() };
            if (!EnumDisplayDevices(null, i, ref dd, 0)) break;
            if ((dd.StateFlags & DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) != 0) gdiMap[dd.DeviceName] = dd;
        }

        for (int i = 0; i < pCount; i++)
        {
            var p = paths[i];
            var src = new DISPLAYCONFIG_SOURCE_DEVICE_NAME { header = new DISPLAYCONFIG_DEVICE_INFO_HEADER { type = DISPLAYCONFIG_DEVICE_INFO_TYPE.GET_SOURCE_NAME, size = (uint)Marshal.SizeOf<DISPLAYCONFIG_SOURCE_DEVICE_NAME>(), adapterId = p.sourceInfo.adapterId, id = p.sourceInfo.id } };
            DisplayConfigGetDeviceInfo(ref src);

            var tgt = new DISPLAYCONFIG_TARGET_DEVICE_NAME { header = new DISPLAYCONFIG_DEVICE_INFO_HEADER { type = DISPLAYCONFIG_DEVICE_INFO_TYPE.GET_TARGET_NAME, size = (uint)Marshal.SizeOf<DISPLAYCONFIG_TARGET_DEVICE_NAME>(), adapterId = p.targetInfo.adapterId, id = p.targetInfo.id } };
            DisplayConfigGetDeviceInfo(ref tgt);

            bool isPrimary = gdiMap.TryGetValue(src.viewGdiDeviceName, out var dd) && (dd.StateFlags & DISPLAY_DEVICE_PRIMARY_DEVICE) != 0;
            var dm = new DEVMODE { dmSize = (ushort)Marshal.SizeOf<DEVMODE>() };
            EnumDisplaySettingsEx(src.viewGdiDeviceName, ENUM_CURRENT_SETTINGS, ref dm, 0);

            double fh = 0;
            string scan = "PROGRESSIVE";
            if (p.targetInfo.modeInfoIdx < mCount && modes[p.targetInfo.modeInfoIdx].infoType == DISPLAYCONFIG_MODE_INFO_TYPE.TARGET)
            {
                var sig = modes[p.targetInfo.modeInfoIdx].modeInfo.targetMode.targetVideoSignalInfo;
                fh = sig.hSyncFreq.AsDouble() / 1000.0;
                scan = sig.scanLineOrdering.ToString();
            }

            string instId = ParseInstanceId(tgt.monitorDevicePath);
            string regPath = string.IsNullOrEmpty(instId) ? "" : $@"SYSTEM\CurrentControlSet\Enum\{instId}\Device Parameters";
            byte[]? edid = null;
            if (!string.IsNullOrEmpty(regPath))
            {
                using var k = Registry.LocalMachine.OpenSubKey(regPath, false);
                edid = k?.GetValue("EDID") as byte[];
            }

            result.Add(new MonitorEntry
            {
                GdiName = src.viewGdiDeviceName,
                IsPrimary = isPrimary,
                FriendlyName = tgt.monitorFriendlyDeviceName,
                DeviceInstanceId = instId,
                RegistryDeviceParamsPath = regPath,
                Width = dm.dmPelsWidth,
                Height = dm.dmPelsHeight,
                RefreshHz = dm.dmDisplayFrequency,
                HFreqKHz = fh,
                Scanline = scan,
                EdidBytes = edid
            });
        }

        return result.OrderBy(m => m.GdiName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string ParseInstanceId(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "";
        string t = path.StartsWith(@"\\?\") ? path[4..] : path;
        int idx = t.IndexOf("#{", StringComparison.Ordinal);
        if (idx > 0) t = t[..idx];
        var parts = t.Split('#');
        return parts.Length >= 3 ? $@"{parts[0]}\{parts[1]}\{parts[2]}" : "";
    }

    public static MonitorEntry? ResolveTargetCrt(AppConfig cfg, out string? error)
    {
        var all = EnumerateMonitors();
        MonitorEntry? crt = null;

        if (!string.IsNullOrWhiteSpace(cfg.TargetCrtInstanceId))
            crt = all.FirstOrDefault(m => m.DeviceInstanceId.Equals(cfg.TargetCrtInstanceId, StringComparison.OrdinalIgnoreCase));

        crt ??= all.FirstOrDefault(m => !m.IsPrimary && !m.DeviceInstanceId.Equals(cfg.ProtectedMonitorInstanceId, StringComparison.OrdinalIgnoreCase));

        if (crt == null)
        {
            error = "No se encontró ningún monitor CRT secundario activo.";
            return null;
        }
        if (crt.IsPrimary || (!string.IsNullOrWhiteSpace(cfg.ProtectedMonitorInstanceId) && crt.DeviceInstanceId.Equals(cfg.ProtectedMonitorInstanceId, StringComparison.OrdinalIgnoreCase)))
        {
            error = $"[HARD-LOCK] Operación bloqueada sobre el monitor protegido ({crt.GdiName} / {crt.DeviceInstanceId}).";
            return null;
        }

        error = null;
        return crt;
    }

    public static (bool ok, string message) SetCrtMode(AppConfig cfg, uint width, uint height, uint freq = 60, bool persistInRegistry = true)
    {
        var crt = ResolveTargetCrt(cfg, out var err);
        if (crt == null) return (false, err ?? "Monitor CRT no disponible.");

        var cur = new DEVMODE { dmSize = (ushort)Marshal.SizeOf<DEVMODE>() };
        if (!EnumDisplaySettingsEx(crt.GdiName, ENUM_CURRENT_SETTINGS, ref cur, 0))
            return (false, $"No se pudo leer el modo actual de {crt.GdiName}.");

        if (cur.dmPelsWidth == width && cur.dmPelsHeight == height && cur.dmDisplayFrequency == freq)
            return (true, $"{crt.GdiName} ya está en {width}×{height} @ {freq} Hz.");

        cur.dmPelsWidth = width;
        cur.dmPelsHeight = height;
        cur.dmDisplayFrequency = freq;
        cur.dmBitsPerPel = 32;
        cur.dmDisplayFlags = 0;
        cur.dmFields = DM_PELSWIDTH | DM_PELSHEIGHT | DM_DISPLAYFREQUENCY | DM_BITSPERPEL | DM_DISPLAYFLAGS | DM_POSITION;

        if (persistInRegistry)
        {
            int test = ChangeDisplaySettingsEx(crt.GdiName, ref cur, IntPtr.Zero, CDS_TEST, IntPtr.Zero);
            if (test != DISP_CHANGE_SUCCESSFUL)
                return (false, $"Windows/NVIDIA rechazó {width}×{height}@{freq}Hz (CDS_TEST={test}).");

            int stage = ChangeDisplaySettingsEx(crt.GdiName, ref cur, IntPtr.Zero, CDS_UPDATEREGISTRY | CDS_NORESET, IntPtr.Zero);
            int commit = ChangeDisplaySettingsEx(null, IntPtr.Zero, IntPtr.Zero, 0, IntPtr.Zero);
            return (stage == DISP_CHANGE_SUCCESSFUL && commit == DISP_CHANGE_SUCCESSFUL)
                ? (true, $"Aplicado y guardado como arranque: {width}×{height} @ {freq} Hz en {crt.GdiName}.")
                : (false, $"Error al aplicar modo persistente (Stage={stage}, Commit={commit}).");
        }
        else
        {
            DEVMODE rawMatch = cur;
            bool foundRaw = false;
            for (int i = 0; ; i++)
            {
                var probe = new DEVMODE { dmSize = (ushort)Marshal.SizeOf<DEVMODE>() };
                if (!EnumDisplaySettingsEx(crt.GdiName, i, ref probe, EDS_RAWMODE)) break;
                if (probe.dmPelsWidth == width && probe.dmPelsHeight == height && probe.dmDisplayFrequency == freq && probe.dmBitsPerPel == 32)
                {
                    rawMatch = probe;
                    foundRaw = true;
                    break;
                }
            }
            if (!foundRaw) rawMatch.dmFields = DM_PELSWIDTH | DM_PELSHEIGHT | DM_DISPLAYFREQUENCY | DM_BITSPERPEL;

            int res = ChangeDisplaySettingsEx(crt.GdiName, ref rawMatch, IntPtr.Zero, 0, IntPtr.Zero);
            if (res == DISP_CHANGE_SUCCESSFUL)
                return (true, $"Aplicado {width}×{height} @ {freq} Hz en {crt.GdiName} (en sesión; arranque intacto).");

            if (width == 2560 && height == 224)
                return NvApiBridge.TryApply2560x224(crt.GdiName);

            return (false, $"Windows/NVIDIA rechazó {width}×{height}@{freq}Hz (Code={res}).");
        }
    }

    public static (bool ok, string message) InstallEdidOverride(AppConfig cfg)
    {
        if (!IsAdministrator()) return (false, "Se requieren privilegios de Administrador para escribir EDID_OVERRIDE.");
        var crt = ResolveTargetCrt(cfg, out var err);
        if (crt == null) return (false, err ?? "CRT no disponible.");
        if (crt.EdidBytes == null || crt.EdidBytes.Length < 128)
            return (false, "No se encontró bloque EDID válido (>=128 bytes) para el monitor CRT.");

        // 1) Backup atomico en backup\
        uint prevW = crt.Width;
        uint prevH = crt.Height;
        uint prevHz = crt.RefreshHz;
        byte[]? prevOv0 = null;
        byte[]? prevOv1 = null;
        bool hadPrevOverride = false;

        try
        {
            // 0) Respaldar estado previo (resolucion, refresco y EDID_OVERRIDE previo si existia)
            using (var existingOv = Registry.LocalMachine.OpenSubKey($@"{crt.RegistryDeviceParamsPath}\EDID_OVERRIDE", writable: false))
            {
                if (existingOv != null)
                {
                    hadPrevOverride = true;
                    prevOv0 = existingOv.GetValue("0") as byte[];
                    prevOv1 = existingOv.GetValue("1") as byte[];
                }
            }

            Directory.CreateDirectory(AppConfig.BackupDir);
            string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            File.WriteAllBytes(Path.Combine(AppConfig.BackupDir, $"edid_backup_{ts}.bin"), crt.EdidBytes);
            WriteRegBackup(Path.Combine(AppConfig.BackupDir, $"edid_backup_{ts}.reg"), crt.RegistryDeviceParamsPath, crt.EdidBytes);
            SavePreChangeState(crt.DeviceInstanceId, crt.GdiName, prevW, prevH, prevHz, hadPrevOverride);
            Log($"Snapshot pre-cambio guardado para {crt.DeviceInstanceId}: {prevW}x{prevH}@{prevHz}Hz, hadOverride={hadPrevOverride}, backup=edid_backup_{ts}.bin");

            // 2) Construir Bloque 0 (Slot 0 nativo, Slot 1 = 2560x240, Slot 2 = 2560x224, Slot 3 = Range 15-100kHz)
            byte[] block0 = new byte[128];
            Array.Copy(crt.EdidBytes, 0, block0, 0, 128);
            byte[] dtd2560x240 = { 0xFE, 0x13, 0x00, 0xB8, 0xA2, 0xF0, 0x16, 0x00, 0x68, 0xF0, 0x33, 0x00, 0x90, 0x2C, 0x11, 0x00, 0x00, 0x18 };
            byte[] dtd2560x224 = { 0x0A, 0x14, 0x00, 0xC0, 0xA2, 0xE0, 0x26, 0x00, 0x68, 0xF0, 0xB3, 0x00, 0x90, 0x2C, 0x11, 0x00, 0x00, 0x18 };
            byte[] dtd1920x240 = { 0xFC, 0x0E, 0x80, 0x08, 0x72, 0xF0, 0x16, 0x00, 0x50, 0xB0, 0x33, 0x00, 0x90, 0x2C, 0x11, 0x00, 0x00, 0x18 };
            byte[] range15kHz = { 0x00, 0x00, 0x00, 0xFD, 0x00, 50, 75, 15, 100, 20, 0x00, 0x0A, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20 };

            Array.Copy(dtd2560x240, 0, block0, 54 + 18 * 1, 18);
            Array.Copy(dtd2560x224, 0, block0, 54 + 18 * 2, 18);
            Array.Copy(range15kHz, 0, block0, 54 + 18 * 3, 18);
            block0[127] = ComputeChecksum(block0);

            using (var devParams = Registry.LocalMachine.OpenSubKey(crt.RegistryDeviceParamsPath, writable: true))
            {
                if (devParams == null)
                {
                    Log($"ERROR: No se pudo abrir HKLM\\{crt.RegistryDeviceParamsPath}.");
                    return (false, $"No se pudo abrir HKLM\\{crt.RegistryDeviceParamsPath}.");
                }
                using var ov = devParams.CreateSubKey("EDID_OVERRIDE", writable: true);
                ov.SetValue("0", block0, RegistryValueKind.Binary);

                if (crt.EdidBytes.Length >= 256 && crt.EdidBytes[128] == 0x02)
                {
                    int dtdStart = crt.EdidBytes[128 + 2];
                    if (dtdStart >= 4 && dtdStart <= 90)
                    {
                        byte[] block1 = new byte[128];
                        Array.Copy(crt.EdidBytes, 128, block1, 0, dtdStart);
                        Array.Copy(dtd1920x240, 0, block1, dtdStart, 18);
                        Array.Copy(dtd2560x224, 0, block1, dtdStart + 18, 18);
                        block1[3] = (byte)((block1[3] & 0xF0) | 2);
                        block1[127] = ComputeChecksum(block1);
                        ov.SetValue("1", block1, RegistryValueKind.Binary);
                    }
                }
            }

            // 3) Recargar unicamente el nodo del monitor CRT (compatible Win10/Win11 con fallback)
            bool restarted = ToggleDeviceNode(crt.DeviceInstanceId);
            Thread.Sleep(2000);
            string restartNote = restarted ? "" : " (Aviso: reinicia Windows para recargar el monitor si el modo aun no aparece)";
            Log($"EDID_OVERRIDE instalado en {crt.DeviceInstanceId}. RestartOK={restarted}");
            return (true, $"EDID_OVERRIDE instalado en {crt.DeviceInstanceId} (backup guardado en backup\\edid_backup_{ts}.bin).{restartNote}");
        }
        catch (Exception ex)
        {
            Log($"ERROR en InstallEdidOverride ({crt.DeviceInstanceId}): {ex.Message}. Ejecutando rollback automatico completo...");
            RestoreEdidStateSafe(crt.RegistryDeviceParamsPath, crt.DeviceInstanceId, crt.GdiName, hadPrevOverride, prevOv0, prevOv1, prevW, prevH, prevHz);
            return (false, $"Error detectado: se revirtio automaticamente el monitor a su estado original ({prevW}x{prevH}@{prevHz}Hz). Detalle: {ex.Message}");
        }
    }

    public static (bool ok, string message) RollbackEdidOverride(AppConfig cfg)
    {
        if (!IsAdministrator()) return (false, "Se requieren privilegios de Administrador para restaurar EDID_OVERRIDE.");
        var crt = ResolveTargetCrt(cfg, out var err);
        if (crt == null) return (false, err ?? "CRT no disponible.");

        try
        {
            using (var devParams = Registry.LocalMachine.OpenSubKey(crt.RegistryDeviceParamsPath, writable: true))
                devParams?.DeleteSubKeyTree("EDID_OVERRIDE", throwOnMissingSubKey: false);

            bool restarted = ToggleDeviceNode(crt.DeviceInstanceId);
            Log($"EDID_OVERRIDE revertido en {crt.DeviceInstanceId}. RestartOK={restarted}");
            return (true, $"EDID_OVERRIDE eliminado limpiamente de {crt.DeviceInstanceId}.");
        }
        catch (Exception ex)
        {
            Log($"ERROR en RollbackEdidOverride ({crt.DeviceInstanceId}): {ex.Message}");
            return (false, $"Error al revertir EDID_OVERRIDE: {ex.Message}");
        }
    }

    private static byte ComputeChecksum(byte[] block128)
    {
        int sum = 0;
        for (int i = 0; i < 127; i++) sum += block128[i];
        return (byte)((256 - (sum % 256)) % 256);
    }

    private static void WriteRegBackup(string regPath, string subKey, byte[] edid)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Windows Registry Editor Version 5.00");
        sb.AppendLine();
        sb.AppendLine($@"[HKEY_LOCAL_MACHINE\{subKey}]");
        sb.AppendLine($"\"EDID\"=hex:{string.Join(",", edid.Select(b => b.ToString("x2")))}");
        sb.AppendLine();
        sb.AppendLine($@"[-HKEY_LOCAL_MACHINE\{subKey}\EDID_OVERRIDE]");
        File.WriteAllText(regPath, sb.ToString(), Encoding.Unicode);
    }

    private static bool ToggleDeviceNode(string instanceId)
    {
        // Nivel 1: SetupAPI nativo (Windows 10 y Windows 11)
        try
        {
            bool disabled = InvokePropChange(instanceId, DICS_DISABLE);
            Thread.Sleep(800);
            bool enabled = InvokePropChange(instanceId, DICS_ENABLE);
            if (disabled && enabled)
            {
                Log($"Nodo PnP reiniciado via SetupAPI: {instanceId}");
                return true;
            }
        }
        catch (Exception ex)
        {
            Log($"SetupAPI fallback en {instanceId}: {ex.Message}");
        }

        // Nivel 2: pnputil /restart-device (Windows 10 1903+ y Windows 11)
        if (RunHiddenProcess("pnputil.exe", $"/restart-device \"{instanceId}\""))
        {
            Log($"Nodo PnP reiniciado via pnputil /restart-device: {instanceId}");
            return true;
        }

        // Nivel 3: pnputil /disable-device + /enable-device
        bool pnpDis = RunHiddenProcess("pnputil.exe", $"/disable-device \"{instanceId}\"");
        Thread.Sleep(800);
        bool pnpEn = RunHiddenProcess("pnputil.exe", $"/enable-device \"{instanceId}\"");
        if (pnpDis && pnpEn)
        {
            Log($"Nodo PnP reiniciado via pnputil disable/enable: {instanceId}");
            return true;
        }

        Log($"AVISO: No se pudo reiniciar automaticamente el nodo PnP {instanceId} (se aplicara al reiniciar Windows).");
        return false;
    }

    private static bool RunHiddenProcess(string fileName, string arguments)
    {
        try
        {
            using var proc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            if (proc == null) return false;
            proc.WaitForExit(10000);
            return proc.HasExited && proc.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool InvokePropChange(string instanceId, uint stateChange)
    {
        IntPtr set = SetupDiCreateDeviceInfoList(IntPtr.Zero, IntPtr.Zero);
        if (set == new IntPtr(-1)) return false;
        try
        {
            var data = new SP_DEVINFO_DATA { cbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>() };
            if (!SetupDiOpenDeviceInfo(set, instanceId, IntPtr.Zero, 0, ref data)) return false;
            var p = new SP_PROPCHANGE_PARAMS
            {
                ClassInstallHeader = new SP_CLASSINSTALL_HEADER { cbSize = (uint)Marshal.SizeOf<SP_CLASSINSTALL_HEADER>(), InstallFunction = DIF_PROPERTYCHANGE },
                StateChange = stateChange,
                Scope = DICS_FLAG_GLOBAL,
                HwProfile = 0
            };
            if (!SetupDiSetClassInstallParams(set, ref data, ref p, (uint)Marshal.SizeOf<SP_PROPCHANGE_PARAMS>()))
                return false;
            return SetupDiCallClassInstaller(DIF_PROPERTYCHANGE, set, ref data);
        }
        finally { SetupDiDestroyDeviceInfoList(set); }
    }

    private static void SavePreChangeState(string instanceId, string gdiName, uint width, uint height, uint refreshHz, bool hadOverride)
    {
        try
        {
            Directory.CreateDirectory(AppConfig.ConfigDir);
            string statePath = Path.Combine(AppConfig.ConfigDir, "pre_change_state.json");
            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                Timestamp = DateTime.Now.ToString("o"),
                DeviceInstanceId = instanceId,
                GdiName = gdiName,
                OriginalWidth = width,
                OriginalHeight = height,
                OriginalRefreshHz = refreshHz,
                HadPreviousEdidOverride = hadOverride
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(statePath, payload, Encoding.UTF8);
        }
        catch { }
    }

    private static void RestoreEdidStateSafe(string regDeviceParamsPath, string instanceId, string gdiName, bool hadPrevOverride, byte[]? prevOv0, byte[]? prevOv1, uint prevW, uint prevH, uint prevHz)
    {
        try
        {
            using var devParams = Registry.LocalMachine.OpenSubKey(regDeviceParamsPath, writable: true);
            if (devParams != null)
            {
                if (hadPrevOverride && prevOv0 != null)
                {
                    using var ov = devParams.CreateSubKey("EDID_OVERRIDE", writable: true);
                    ov.SetValue("0", prevOv0, RegistryValueKind.Binary);
                    if (prevOv1 != null) ov.SetValue("1", prevOv1, RegistryValueKind.Binary);
                    else ov.DeleteValue("1", throwOnMissingValue: false);
                }
                else
                {
                    devParams.DeleteSubKeyTree("EDID_OVERRIDE", throwOnMissingSubKey: false);
                }
            }
            ToggleDeviceNode(instanceId);
            Thread.Sleep(1000);
            if (prevW >= 640 && prevH >= 200 && prevHz >= 50)
            {
                var dm = new DEVMODE { dmSize = (ushort)Marshal.SizeOf<DEVMODE>() };
                if (EnumDisplaySettingsEx(gdiName, ENUM_CURRENT_SETTINGS, ref dm, 0))
                {
                    dm.dmPelsWidth = prevW;
                    dm.dmPelsHeight = prevH;
                    dm.dmDisplayFrequency = prevHz;
                    dm.dmFields = DM_PELSWIDTH | DM_PELSHEIGHT | DM_DISPLAYFREQUENCY;
                    ChangeDisplaySettingsEx(gdiName, ref dm, IntPtr.Zero, 0, IntPtr.Zero);
                }
            }
            Log($"Rollback automatico completado para {instanceId} ({prevW}x{prevH}@{prevHz}Hz).");
        }
        catch (Exception rex)
        {
            Log($"AVISO durante rollback automatico de {instanceId}: {rex.Message}");
        }
    }

    public static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(AppConfig.ConfigDir);
            string logPath = Path.Combine(AppConfig.ConfigDir, "crt240p.log");
            File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}", Encoding.UTF8);
        }
        catch { }
    }
}
