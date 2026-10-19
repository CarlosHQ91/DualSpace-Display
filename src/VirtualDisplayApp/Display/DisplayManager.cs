using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using VirtualDisplayApp.Display.Native;

namespace VirtualDisplayApp.Display
{
    public class DisplayManager
    {
        private static readonly string[] VirtualSignatures = new[]
        {
            "virtual", "iddcx", "parsec", "usbmmidd", "spacedesk", "amyuni", "mtt", "mttvdd"
        };

        public List<DisplayInfo> GetDisplays()
        {
            var displays = new List<DisplayInfo>();

            // 1. Enumerate active desktop monitors
            DisplayInterop.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdc, ref DisplayInterop.RECT rc, IntPtr data) =>
            {
                var mi = new DisplayInterop.MONITORINFOEX();
                mi.cbSize = Marshal.SizeOf<DisplayInterop.MONITORINFOEX>();

                if (DisplayInterop.GetMonitorInfo(hMonitor, ref mi))
                {
                    string devName = mi.szDevice;
                    var info = new DisplayInfo
                    {
                        HMonitor = hMonitor,
                        DeviceName = devName,
                        Left = mi.rcMonitor.left,
                        Top = mi.rcMonitor.top,
                        Width = mi.rcMonitor.Width,
                        Height = mi.rcMonitor.Height,
                        IsPrimary = (mi.dwFlags & 1) != 0
                    };

                    // Enumerate adapter details
                    var adapter = new DisplayInterop.DISPLAY_DEVICE();
                    adapter.cb = Marshal.SizeOf<DisplayInterop.DISPLAY_DEVICE>();
                    if (DisplayInterop.EnumDisplayDevices(devName, 0, ref adapter, 0))
                    {
                        info.AdapterString = adapter.DeviceString ?? string.Empty;
                        info.DeviceID = adapter.DeviceID ?? string.Empty;

                        // Enumerate monitor attached to adapter
                        var monitor = new DisplayInterop.DISPLAY_DEVICE();
                        monitor.cb = Marshal.SizeOf<DisplayInterop.DISPLAY_DEVICE>();
                        if (DisplayInterop.EnumDisplayDevices(devName, 0, ref monitor, 0))
                        {
                            info.MonitorString = string.IsNullOrWhiteSpace(monitor.DeviceString) ? info.AdapterString : monitor.DeviceString;
                            if (string.IsNullOrWhiteSpace(info.DeviceID))
                            {
                                info.DeviceID = monitor.DeviceID ?? string.Empty;
                            }
                        }
                    }

                    // Query display resolution and refresh rate
                    var devMode = new DisplayInterop.DEVMODE();
                    devMode.dmSize = (ushort)Marshal.SizeOf<DisplayInterop.DEVMODE>();
                    if (DisplayInterop.EnumDisplaySettings(devName, DisplayInterop.ENUM_CURRENT_SETTINGS, ref devMode))
                    {
                        info.RefreshRate = devMode.dmDisplayFrequency;
                        if (info.Width == 0) info.Width = (int)devMode.dmPelsWidth;
                        if (info.Height == 0) info.Height = (int)devMode.dmPelsHeight;
                    }

                    // Check if signature matches virtual display (NEVER mark primary display as virtual!)
                    if (!info.IsPrimary)
                    {
                        string checkText = $"{info.AdapterString} {info.MonitorString} {info.DeviceID}".ToLowerInvariant();
                        foreach (var sig in VirtualSignatures)
                        {
                            if (checkText.Contains(sig))
                            {
                                info.IsVirtual = true;
                                break;
                            }
                        }
                    }
                    else
                    {
                        info.IsVirtual = false;
                    }

                    displays.Add(info);
                }

                return true;
            }, IntPtr.Zero);

            return displays;
        }

        public List<string> GetAllDevices()
        {
            var results = new List<string>();
            uint devNum = 0;
            var dev = new DisplayInterop.DISPLAY_DEVICE();
            dev.cb = Marshal.SizeOf<DisplayInterop.DISPLAY_DEVICE>();

            while (DisplayInterop.EnumDisplayDevices(null, devNum, ref dev, 0))
            {
                bool attached = (dev.StateFlags & DisplayInterop.DisplayDeviceStateFlags.AttachedToDesktop) != 0;
                bool primary = (dev.StateFlags & DisplayInterop.DisplayDeviceStateFlags.PrimaryDevice) != 0;
                string status = attached ? (primary ? "[Activo - Primario]" : "[Activo - Extendido]") : "[Desconectado]";
                results.Add($"{dev.DeviceName} ({dev.DeviceString}) {status} - ID: {dev.DeviceID}");

                // Check monitor under this device
                var mon = new DisplayInterop.DISPLAY_DEVICE();
                mon.cb = Marshal.SizeOf<DisplayInterop.DISPLAY_DEVICE>();
                if (DisplayInterop.EnumDisplayDevices(dev.DeviceName, 0, ref mon, 0))
                {
                    results.Add($"   └── Monitor: {mon.DeviceString} | ID: {mon.DeviceID}");
                }

                devNum++;
                dev = new DisplayInterop.DISPLAY_DEVICE();
                dev.cb = Marshal.SizeOf<DisplayInterop.DISPLAY_DEVICE>();
            }

            return results;
        }

        public List<DisplayInfo> GetVirtualDisplays()
        {
            var list = GetDisplays();
            var virtuals = list.FindAll(d => !d.IsPrimary && d.IsVirtual);
            if (virtuals.Count > 0) return virtuals;
            return list.FindAll(d => !d.IsPrimary);
        }

        public DisplayInfo? FindVirtualDisplay()
        {
            var list = GetDisplays();
            return list.Find(d => !d.IsPrimary && d.IsVirtual)
                ?? list.Find(d => !d.IsPrimary);
        }

        public bool HasVirtualDisplay()
        {
            return FindVirtualDisplay() != null;
        }

        public bool SetDisplayResolution(string deviceName, int width, int height)
        {
            var devMode = new DisplayInterop.DEVMODE();
            devMode.dmSize = (ushort)Marshal.SizeOf<DisplayInterop.DEVMODE>();
            if (!DisplayInterop.EnumDisplaySettings(deviceName, DisplayInterop.ENUM_CURRENT_SETTINGS, ref devMode))
            {
                return false;
            }

            devMode.dmPelsWidth = (uint)width;
            devMode.dmPelsHeight = (uint)height;
            devMode.dmFields = DisplayInterop.DM_PELSWIDTH | DisplayInterop.DM_PELSHEIGHT;

            int result = DisplayInterop.ChangeDisplaySettingsEx(deviceName, ref devMode, IntPtr.Zero, DisplayInterop.CDS_UPDATEREGISTRY, IntPtr.Zero);
            return result == DisplayInterop.DISP_CHANGE_SUCCESSFUL;
        }
    }
}
