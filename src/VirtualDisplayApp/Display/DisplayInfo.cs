using System;

namespace VirtualDisplayApp.Display
{
    public class DisplayInfo
    {
        public IntPtr HMonitor { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        public string AdapterString { get; set; } = string.Empty;
        public string MonitorString { get; set; } = string.Empty;
        public string DeviceID { get; set; } = string.Empty;
        public int Left { get; set; }
        public int Top { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public uint RefreshRate { get; set; }
        public bool IsPrimary { get; set; }
        public bool IsVirtual { get; set; }

        public override string ToString()
        {
            string type = IsVirtual ? "[VIRTUAL (IddCx)]" : "[FÍSICO]";
            string primary = IsPrimary ? " (Primario)" : "";
            return $"{type}{primary} {DeviceName}: {MonitorString} | {Width}x{Height} @ {RefreshRate}Hz ({Left},{Top}) Handle: 0x{HMonitor.ToString("X")}";
        }
    }
}
