using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using VirtualDisplayApp.Display;
using VirtualDisplayApp.Display.Native;

namespace VirtualDisplayApp.Utils
{
    public static class WindowManager
    {
        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out DisplayInterop.RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_NOSIZE = 0x0001;

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        public const int SW_SHOWNORMAL = 1;
        public const int SW_MAXIMIZE = 3;
        public const int SW_RESTORE = 9;

        public static bool MoveWindowToDisplay(IntPtr hWnd, DisplayInfo targetDisplay)
        {
            if (hWnd == IntPtr.Zero) return false;
            if (!GetWindowRect(hWnd, out var rect)) return false;

            int width = Math.Min(rect.Width, targetDisplay.Width - 40);
            int height = Math.Min(rect.Height, targetDisplay.Height - 40);
            if (width <= 100) width = Math.Max(400, targetDisplay.Width - 40);
            if (height <= 100) height = Math.Max(300, targetDisplay.Height - 40);

            int targetX = targetDisplay.Left + 20;
            int targetY = targetDisplay.Top + 20;

            return SetWindowPos(hWnd, IntPtr.Zero, targetX, targetY, width, height, SWP_NOZORDER | SWP_SHOWWINDOW);
        }

        public static bool FitWindowToDisplay(IntPtr hWnd, DisplayInfo targetDisplay)
        {
            if (hWnd == IntPtr.Zero) return false;

            // Restore if previously maximized on another screen
            ShowWindow(hWnd, SW_RESTORE);

            // Move inside target display boundaries
            SetWindowPos(hWnd, IntPtr.Zero, targetDisplay.Left + 10, targetDisplay.Top + 10, targetDisplay.Width - 20, targetDisplay.Height - 20, SWP_NOZORDER | SWP_SHOWWINDOW);

            // Maximize cleanly to take full work area of the virtual display
            ShowWindow(hWnd, SW_MAXIMIZE);
            return true;
        }

        public static bool MoveForegroundWindowToDisplay(DisplayInfo targetDisplay)
        {
            IntPtr fg = GetForegroundWindow();
            if (fg == IntPtr.Zero) return false;
            return FitWindowToDisplay(fg, targetDisplay);
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;
        }

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        public static extern bool SetCursorPos(int X, int Y);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct STARTUPINFO
        {
            public int cb;
            public string? lpReserved;
            public string? lpDesktop;
            public string? lpTitle;
            public int dwX;
            public int dwY;
            public int dwXSize;
            public int dwYSize;
            public int dwXCountChars;
            public int dwYCountChars;
            public int dwFillAttribute;
            public int dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public uint dwProcessId;
            public uint dwThreadId;
        }

        private const int STARTF_USESHOWWINDOW = 0x00000001;
        private const int STARTF_USESIZE = 0x00000002;
        private const int STARTF_USEPOSITION = 0x00000004;

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool CreateProcess(
            string? lpApplicationName,
            string? lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string? lpCurrentDirectory,
            [In] ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr hObject);

        public static void LaunchAppOnDisplay(string commandOrPath, DisplayInfo targetDisplay)
        {
            Task.Run(() =>
            {
                try
                {
                    GetCursorPos(out var prevPos);
                    // Temporarily move cursor to virtual display so Windows creates windows on virtual display
                    SetCursorPos(targetDisplay.Left + targetDisplay.Width / 2, targetDisplay.Top + targetDisplay.Height / 2);

                    uint pid = 0;
                    var si = new STARTUPINFO();
                    si.cb = Marshal.SizeOf<STARTUPINFO>();
                    si.dwFlags = STARTF_USEPOSITION | STARTF_USESIZE | STARTF_USESHOWWINDOW;
                    si.dwX = targetDisplay.Left;
                    si.dwY = targetDisplay.Top;
                    si.dwXSize = targetDisplay.Width;
                    si.dwYSize = targetDisplay.Height;
                    si.wShowWindow = (short)SW_MAXIMIZE;

                    if (CreateProcess(null, commandOrPath, IntPtr.Zero, IntPtr.Zero, false, 0, IntPtr.Zero, null, ref si, out var pi))
                    {
                        pid = pi.dwProcessId;
                        CloseHandle(pi.hProcess);
                        CloseHandle(pi.hThread);
                    }
                    else
                    {
                        var psi = new ProcessStartInfo(commandOrPath)
                        {
                            UseShellExecute = true
                        };
                        var proc = Process.Start(psi);
                        if (proc != null) pid = (uint)proc.Id;
                    }

                    // Fast aggressive check: 10ms intervals for 3 seconds
                    for (int i = 0; i < 300; i++)
                    {
                        Thread.Sleep(10);
                        IntPtr foundHandle = IntPtr.Zero;

                        EnumWindows((hWnd, lParam) =>
                        {
                            if (!IsWindowVisible(hWnd)) return true;
                            GetWindowThreadProcessId(hWnd, out uint wPid);
                            if (pid != 0 && wPid == pid)
                            {
                                foundHandle = hWnd;
                                return false;
                            }
                            return true;
                        }, IntPtr.Zero);

                        if (foundHandle != IntPtr.Zero)
                        {
                            FitWindowToDisplay(foundHandle, targetDisplay);
                            break;
                        }
                    }

                    // Restore host cursor position
                    SetCursorPos(prevPos.x, prevPos.y);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[LaunchAppOnDisplay Error] {ex.Message}");
                }
            });
        }
    }
}
