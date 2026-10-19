using System;
using VirtualDisplayApp.Capture.Native;
using Vortice.Direct3D11;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;

namespace VirtualDisplayApp.Capture
{
    public class CaptureService : IDisposable
    {
        private readonly ID3D11Device _device;
        private readonly IDirect3DDevice _winrtDevice;
        private GraphicsCaptureItem? _captureItem;
        private Direct3D11CaptureFramePool? _framePool;
        private GraphicsCaptureSession? _session;

        public event Action<ID3D11Texture2D>? FrameArrived;
        public bool IsRunning { get; private set; }

        public CaptureService(ID3D11Device device, IDirect3DDevice winrtDevice)
        {
            _device = device;
            _winrtDevice = winrtDevice;
        }

        public void Start(IntPtr hMonitor)
        {
            if (IsRunning) Stop();

            try
            {
                File.AppendAllText("debug.log", $"[{DateTime.Now:HH:mm:ss.fff}] Starting capture for HMonitor: 0x{hMonitor.ToString("X")}\n");
                _captureItem = CaptureInterop.CreateItemForMonitor(hMonitor);
                File.AppendAllText("debug.log", $"[{DateTime.Now:HH:mm:ss.fff}] CaptureItem created. Size: {_captureItem.Size.Width}x{_captureItem.Size.Height}\n");

                _framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                    _winrtDevice,
                    DirectXPixelFormat.B8G8R8A8UIntNormalized,
                    2,
                    _captureItem.Size);

                _framePool.FrameArrived += OnFrameArrived;

                _session = _framePool.CreateCaptureSession(_captureItem);
                _session.IsCursorCaptureEnabled = true;

                _session.StartCapture();
                IsRunning = true;
                File.AppendAllText("debug.log", $"[{DateTime.Now:HH:mm:ss.fff}] Capture session started successfully.\n");
            }
            catch (Exception ex)
            {
                File.AppendAllText("debug.log", $"[{DateTime.Now:HH:mm:ss.fff}] CaptureService.Start ERROR: {ex}\n");
                throw;
            }
        }

        private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
        {
            try
            {
                using var frame = sender.TryGetNextFrame();
                if (frame == null) return;

                if (frame.ContentSize.Width != _captureItem?.Size.Width ||
                    frame.ContentSize.Height != _captureItem?.Size.Height)
                {
                    _framePool?.Recreate(
                        _winrtDevice,
                        DirectXPixelFormat.B8G8R8A8UIntNormalized,
                        2,
                        frame.ContentSize);
                }

                using var texture = Direct3D11Helper.CreateTexture2DFromSurface(frame.Surface);
                FrameArrived?.Invoke(texture);
            }
            catch (Exception ex)
            {
                File.AppendAllText("debug.log", $"[{DateTime.Now:HH:mm:ss.fff}] OnFrameArrived ERROR: {ex}\n");
            }
        }

        public void Stop()
        {
            if (!IsRunning) return;

            if (_framePool != null)
            {
                _framePool.FrameArrived -= OnFrameArrived;
            }

            _session?.Dispose();
            _session = null;

            _framePool?.Dispose();
            _framePool = null;

            _captureItem = null;
            IsRunning = false;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
