using System;
using System.Runtime.InteropServices;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Windows.Graphics.DirectX.Direct3D11;

namespace VirtualDisplayApp.Capture.Native
{
    [ComImport]
    [Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IDirect3DDxgiInterfaceAccess
    {
        int GetInterface(in Guid iid, out IntPtr p);
    }

    public static class Direct3D11Helper
    {
        private static readonly Guid ID3D11Texture2DGuid = new Guid("6f15aaf2-d208-4e89-9ab4-489535d34f9c");

        public static (ID3D11Device Device, ID3D11DeviceContext Context, IDirect3DDevice WinRTDevice) CreateDevice()
        {
            var creationFlags = DeviceCreationFlags.BgraSupport;
            var featureLevels = new[]
            {
                FeatureLevel.Level_11_1,
                FeatureLevel.Level_11_0
            };

            ID3D11Device? device;
            ID3D11DeviceContext? context;

            var hr = D3D11.D3D11CreateDevice(
                (IDXGIAdapter?)null,
                DriverType.Hardware,
                creationFlags,
                featureLevels,
                out device,
                out context);

            if (hr.Failure || device == null || context == null)
            {
                // Fallback to WARP if hardware unavailable
                D3D11.D3D11CreateDevice(
                    (IDXGIAdapter?)null,
                    DriverType.Warp,
                    creationFlags,
                    featureLevels,
                    out device,
                    out context).CheckError();
            }

            using var dxgiDevice = device.QueryInterface<IDXGIDevice>();
            int res = CaptureInterop.CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.NativePointer, out IntPtr pWinRT);
            if (res != 0) Marshal.ThrowExceptionForHR(res);

            var winrtDevice = WinRT.MarshalInterface<IDirect3DDevice>.FromAbi(pWinRT);
            return (device, context, winrtDevice);
        }

        public static ID3D11Texture2D CreateTexture2DFromSurface(IDirect3DSurface surface)
        {
            IntPtr pNative;
            if (surface is WinRT.IWinRTObject winrtObj)
            {
                pNative = winrtObj.NativeObject.ThisPtr;
            }
            else
            {
                pNative = Marshal.GetIUnknownForObject(surface);
            }

            Guid dxgiInterfaceAccessGuid = new Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1");
            int hr = Marshal.QueryInterface(pNative, ref dxgiInterfaceAccessGuid, out IntPtr pAccess);
            if (hr != 0 || pAccess == IntPtr.Zero)
            {
                Marshal.ThrowExceptionForHR(hr);
            }

            try
            {
                var access = (IDirect3DDxgiInterfaceAccess)Marshal.GetObjectForIUnknown(pAccess);
                int res = access.GetInterface(ID3D11Texture2DGuid, out IntPtr pTexture);
                if (res != 0 || pTexture == IntPtr.Zero)
                {
                    Marshal.ThrowExceptionForHR(res);
                }

                return new ID3D11Texture2D(pTexture);
            }
            finally
            {
                Marshal.Release(pAccess);
            }
        }
    }
}
