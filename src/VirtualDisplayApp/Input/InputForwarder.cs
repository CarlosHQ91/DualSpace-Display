using System;
using System.Runtime.InteropServices;
using VirtualDisplayApp.Display;

namespace VirtualDisplayApp.Input
{
    public class InputForwarder
    {
        private const int SM_XVIRTUALSCREEN = 76;
        private const int SM_YVIRTUALSCREEN = 77;
        private const int SM_CXVIRTUALSCREEN = 78;
        private const int SM_CYVIRTUALSCREEN = 79;

        private const uint INPUT_MOUSE = 0;
        private const uint INPUT_KEYBOARD = 1;

        private const uint MOUSEEVENTF_MOVE = 0x0001;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
        private const uint MOUSEEVENTF_WHEEL = 0x0800;
        private const uint MOUSEEVENTF_VIRTUALDESK = 0x4000;
        private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;

        private const uint KEYEVENTF_KEYUP = 0x0002;

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public InputUnion u;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)]
            public MOUSEINPUT mi;
            [FieldOffset(0)]
            public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        public DisplayInfo TargetDisplay { get; set; }
        public bool IsInputActive { get; set; } = false;

        public InputForwarder(DisplayInfo targetDisplay)
        {
            TargetDisplay = targetDisplay;
        }

        private (int normX, int normY) MapToVirtualDesk(float localRatioX, float localRatioY)
        {
            int vDeskX = GetSystemMetrics(SM_XVIRTUALSCREEN);
            int vDeskY = GetSystemMetrics(SM_YVIRTUALSCREEN);
            int vDeskW = GetSystemMetrics(SM_CXVIRTUALSCREEN);
            int vDeskH = GetSystemMetrics(SM_CYVIRTUALSCREEN);

            if (vDeskW <= 0) vDeskW = 1920;
            if (vDeskH <= 0) vDeskH = 1080;

            int targetScreenX = TargetDisplay.Left + (int)(localRatioX * TargetDisplay.Width);
            int targetScreenY = TargetDisplay.Top + (int)(localRatioY * TargetDisplay.Height);

            int normX = (int)(((targetScreenX - vDeskX) * 65535.0f) / vDeskW);
            int normY = (int)(((targetScreenY - vDeskY) * 65535.0f) / vDeskH);

            return (normX, normY);
        }

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

        public void SendMouseMove(float localRatioX, float localRatioY)
        {
            if (!IsInputActive) return;
            var (normX, normY) = MapToVirtualDesk(localRatioX, localRatioY);

            int targetScreenX = TargetDisplay.Left + (int)(localRatioX * TargetDisplay.Width);
            int targetScreenY = TargetDisplay.Top + (int)(localRatioY * TargetDisplay.Height);
            SetCursorPos(targetScreenX, targetScreenY);

            var input = new INPUT
            {
                type = INPUT_MOUSE,
                u = new InputUnion
                {
                    mi = new MOUSEINPUT
                    {
                        dx = normX,
                        dy = normY,
                        dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK
                    }
                }
            };

            SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
        }

        public void SendMouseButton(float localRatioX, float localRatioY, uint flag, uint mouseData = 0)
        {
            if (!IsInputActive) return;
            var (normX, normY) = MapToVirtualDesk(localRatioX, localRatioY);

            int targetScreenX = TargetDisplay.Left + (int)(localRatioX * TargetDisplay.Width);
            int targetScreenY = TargetDisplay.Top + (int)(localRatioY * TargetDisplay.Height);
            SetCursorPos(targetScreenX, targetScreenY);

            var input = new INPUT
            {
                type = INPUT_MOUSE,
                u = new InputUnion
                {
                    mi = new MOUSEINPUT
                    {
                        dx = normX,
                        dy = normY,
                        mouseData = mouseData,
                        dwFlags = flag | MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK
                    }
                }
            };

            SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
        }

        public void SendLeftDown(float x, float y) => SendMouseButton(x, y, MOUSEEVENTF_LEFTDOWN);
        public void SendLeftUp(float x, float y) => SendMouseButton(x, y, MOUSEEVENTF_LEFTUP);
        public void SendRightDown(float x, float y) => SendMouseButton(x, y, MOUSEEVENTF_RIGHTDOWN);
        public void SendRightUp(float x, float y) => SendMouseButton(x, y, MOUSEEVENTF_RIGHTUP);
        public void SendMiddleDown(float x, float y) => SendMouseButton(x, y, MOUSEEVENTF_MIDDLEDOWN);
        public void SendMiddleUp(float x, float y) => SendMouseButton(x, y, MOUSEEVENTF_MIDDLEUP);
        public void SendWheel(float x, float y, int delta) => SendMouseButton(x, y, MOUSEEVENTF_WHEEL, (uint)delta);

        public void SendKey(ushort vkCode, bool isKeyUp)
        {
            if (!IsInputActive) return;

            var input = new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = vkCode,
                        dwFlags = isKeyUp ? KEYEVENTF_KEYUP : 0
                    }
                }
            };

            SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
        }
    }
}
