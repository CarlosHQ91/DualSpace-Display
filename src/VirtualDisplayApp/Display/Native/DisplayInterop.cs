using System;
using System.Runtime.InteropServices;

namespace VirtualDisplayApp.Display.Native
{
    public static class DisplayInterop
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct DISPLAY_DEVICE
        {
            [MarshalAs(UnmanagedType.U4)]
            public int cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceString;
            [MarshalAs(UnmanagedType.U4)]
            public DisplayDeviceStateFlags StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceKey;
        }

        [Flags]
        public enum DisplayDeviceStateFlags : int
        {
            AttachedToDesktop = 0x1,
            MultiDriver = 0x2,
            PrimaryDevice = 0x4,
            MirroringDriver = 0x8,
            VGACompatible = 0x10,
            Removable = 0x20,
            ModesPruned = 0x8000000,
            Remote = 0x4000000,
            Disconnect = 0x2000000
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;

            public int Width => right - left;
            public int Height => bottom - top;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct MONITORINFOEX
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            public ushort dmSpecVersion;
            public ushort dmDriverVersion;
            public ushort dmSize;
            public ushort dmDriverExtra;
            public uint dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public uint dmDisplayOrientation;
            public uint dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            public ushort dmLogPixels;
            public uint dmBitsPerPel;
            public uint dmPelsWidth;
            public uint dmPelsHeight;
            public uint dmDisplayFlags;
            public uint dmDisplayFrequency;
            public uint dmICMMethod;
            public uint dmICMIntent;
            public uint dmMediaType;
            public uint dmDitherType;
            public uint dmReserved1;
            public uint dmReserved2;
            public uint dmPanningWidth;
            public uint dmPanningHeight;
        }

        public delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool EnumDisplayDevices(string? lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

        [DllImport("user32.dll")]
        public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool EnumDisplaySettings(string? lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);

        public const int ENUM_CURRENT_SETTINGS = -1;
        public const int ENUM_REGISTRY_SETTINGS = -2;

        public const uint DM_PELSWIDTH = 0x00080000;
        public const uint DM_PELSHEIGHT = 0x00100000;
        public const int DISP_CHANGE_SUCCESSFUL = 0;
        public const uint CDS_UPDATEREGISTRY = 0x00000001;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int ChangeDisplaySettingsEx(string? lpszDeviceName, ref DEVMODE lpDevMode, IntPtr hwnd, uint dwflags, IntPtr lParam);
    }
}
