using System;
using System.Runtime.InteropServices;
using Windows.Graphics.Capture;

namespace VirtualDisplayApp.Capture.Native
{
    [ComImport]
    [Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IGraphicsCaptureItemInterop
    {
        int CreateForWindow(IntPtr appWindow, in Guid riid, out IntPtr result);
        int CreateForMonitor(IntPtr monitor, in Guid riid, out IntPtr result);
    }

    [ComImport]
    [Guid("3E68B4BD-CEC5-47F8-B1A3-B642DE7F0D1B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IInitializeWithWindow
    {
        void Initialize(IntPtr hwnd);
    }

    public static class CaptureInterop
    {
        [DllImport("d3d11.dll", EntryPoint = "CreateDirect3D11DeviceFromDXGIDevice", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern int CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);

        private static readonly Guid GraphicsCaptureItemGuid = new Guid("79C3F95B-31F7-4EC2-A464-632EF5D30760");

        public static GraphicsCaptureItem CreateItemForMonitor(IntPtr hMonitor)
        {
            var factory = (IGraphicsCaptureItemInterop)WindowsRuntimeMarshal.GetActivationFactory(typeof(GraphicsCaptureItem));
            int hr = factory.CreateForMonitor(hMonitor, GraphicsCaptureItemGuid, out IntPtr rawItem);
            if (hr != 0 || rawItem == IntPtr.Zero)
            {
                Marshal.ThrowExceptionForHR(hr);
            }

            var item = WinRT.MarshalInterface<GraphicsCaptureItem>.FromAbi(rawItem);
            return item;
        }

        private static class WindowsRuntimeMarshal
        {
            [DllImport("api-ms-win-core-winrt-l1-1-0.dll")]
            private static extern int RoGetActivationFactory(IntPtr hstring, in Guid iid, out IntPtr factory);

            [DllImport("api-ms-win-core-winrt-string-l1-1-0.dll")]
            private static extern int WindowsCreateString([MarshalAs(UnmanagedType.LPWStr)] string sourceString, int length, out IntPtr hstring);

            [DllImport("api-ms-win-core-winrt-string-l1-1-0.dll")]
            private static extern int WindowsDeleteString(IntPtr hstring);

            public static object GetActivationFactory(Type type)
            {
                string typeName = type.FullName ?? throw new ArgumentNullException(nameof(type));
                int hr = WindowsCreateString(typeName, typeName.Length, out IntPtr hstring);
                if (hr != 0) Marshal.ThrowExceptionForHR(hr);

                try
                {
                    Guid iid = typeof(IGraphicsCaptureItemInterop).GUID;
                    hr = RoGetActivationFactory(hstring, in iid, out IntPtr pFactory);
                    if (hr != 0) Marshal.ThrowExceptionForHR(hr);

                    object factory = Marshal.GetObjectForIUnknown(pFactory);
                    Marshal.Release(pFactory);
                    return factory;
                }
                finally
                {
                    WindowsDeleteString(hstring);
                }
            }
        }
    }
}
