using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using VirtualDisplayApp.Capture;
using VirtualDisplayApp.Capture.Native;
using VirtualDisplayApp.Display;
using VirtualDisplayApp.Display.Native;
using VirtualDisplayApp.Input;
using VirtualDisplayApp.Rendering;
using VirtualDisplayApp.Utils;

namespace VirtualDisplayApp.UI
{
    public class ViewerForm : Form
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref DisplayInterop.RECT pvParam, uint fWinIni);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr GetShellWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsZoomed(IntPtr hWnd);

        private const uint SPI_GETWORKAREA = 0x0030;
        private const uint SPI_SETWORKAREA = 0x002F;
        private const uint SPIF_SENDCHANGE = 0x0002;

        private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        private const uint WINEVENT_OUTOFCONTEXT = 0;

        private IntPtr _winEventHook = IntPtr.Zero;
        private WinEventDelegate? _winEventProc;

        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_RELEASE_ESC = 9001;
        private const int HOTKEY_RELEASE_F8 = 9002;
        private const int HOTKEY_FIT_WINDOW = 9003;
        private const int HOTKEY_MOVE_WINDOW = 9004;
        private const int HOTKEY_SPLIT = 9005;
        private const int HOTKEY_SEAMLESS = 9006;

        private const uint MOD_NONE = 0x0000;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;

        private const uint VK_ESCAPE = 0x1B;
        private const uint VK_F4 = 0x73;
        private const uint VK_F5 = 0x74;
        private const uint VK_F6 = 0x75;
        private const uint VK_F7 = 0x76;
        private const uint VK_F8 = 0x77;

        private DisplayInfo _virtualDisplay;
        private readonly D3D11Renderer _renderer;
        private readonly CaptureService _captureService;
        private readonly InputForwarder _inputForwarder;
        private readonly Panel _renderPanel;
        private readonly Panel _topBar;
        private readonly Button _btnToggleInput;
        private readonly Button _btnSeamlessMouse;
        private readonly Button _btnSplitDesktop;
        private readonly Label _lblStatus;

        private DisplayInterop.RECT _originalWorkArea;
        private bool _isWorkAreaSplit = false;
        private bool _seamlessMouseEnabled = true;
        private readonly System.Windows.Forms.Timer _seamlessMouseTimer;

        private int _frameCount = 0;
        private readonly Stopwatch _fpsStopwatch = Stopwatch.StartNew();
        private System.Windows.Forms.Timer? _fpsTimer;

        public ViewerForm(DisplayInfo virtualDisplay)
        {
            _virtualDisplay = virtualDisplay;

            // Form properties
            Text = $"Monitor Virtual - {_virtualDisplay.DeviceName} ({_virtualDisplay.Width}x{_virtualDisplay.Height})";
            BackColor = Color.FromArgb(20, 20, 20);
            ForeColor = Color.White;
            StartPosition = FormStartPosition.Manual;
            KeyPreview = true;

            // Query initial work area
            SystemParametersInfo(SPI_GETWORKAREA, 0, ref _originalWorkArea, 0);
            AppDomain.CurrentDomain.ProcessExit += (s, e) => RestoreDesktopWorkArea();

            // Snap to right half of primary display by default
            var primaryArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
            int halfWidth = primaryArea.Width / 2;
            Bounds = new Rectangle(primaryArea.X + halfWidth, primaryArea.Y, halfWidth, primaryArea.Height);

            // Modern Dark Mode for Title Bar
            int darkMode = 1;
            DwmSetWindowAttribute(Handle, 20, ref darkMode, sizeof(int));

            // Top Control Bar
            _topBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 38,
                BackColor = Color.FromArgb(30, 30, 35),
                Padding = new Padding(6, 4, 6, 4)
            };

            _btnToggleInput = new Button
            {
                Text = "⚪ En Principal",
                Width = 150,
                Dock = DockStyle.Left,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(50, 50, 55),
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            _btnToggleInput.FlatAppearance.BorderSize = 0;
            _btnToggleInput.Click += (s, e) => ToggleInputState();

            // Seamless Mouse Toggle Button
            _btnSeamlessMouse = CreateBarButton("⚡ Fluido: ON [F4]", 95, (s, e) => ToggleSeamlessMouse());
            _btnSeamlessMouse.BackColor = Color.FromArgb(20, 110, 50);

            // Monitor Selector Dropdown
            var cmbDisplays = new ComboBox
            {
                Dock = DockStyle.Left,
                Width = 145,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(40, 40, 45),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8.5f)
            };

            var allDisplays = new DisplayManager().GetVirtualDisplays();
            int selectedIndex = 0;
            for (int i = 0; i < allDisplays.Count; i++)
            {
                var d = allDisplays[i];
                string label = $"{d.DeviceName} ({d.Width}x{d.Height})";
                cmbDisplays.Items.Add(label);
                if (d.DeviceName == _virtualDisplay.DeviceName)
                {
                    selectedIndex = i;
                }
            }
            if (cmbDisplays.Items.Count > 0)
            {
                cmbDisplays.SelectedIndex = selectedIndex;
            }

            cmbDisplays.SelectedIndexChanged += (s, e) =>
            {
                if (cmbDisplays.SelectedIndex >= 0 && cmbDisplays.SelectedIndex < allDisplays.Count)
                {
                    SwitchDisplay(allDisplays[cmbDisplays.SelectedIndex]);
                }
            };

            // Resolution Presets Dropdown
            var cmbRes = new ComboBox
            {
                Dock = DockStyle.Left,
                Width = 90,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(40, 40, 45),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8.5f)
            };
            cmbRes.Items.Add("1920x1080");
            cmbRes.Items.Add("1366x768");
            cmbRes.Items.Add("800x600");

            string currentResStr = $"{_virtualDisplay.Width}x{_virtualDisplay.Height}";
            if (!cmbRes.Items.Contains(currentResStr))
            {
                cmbRes.Items.Insert(0, currentResStr);
            }
            cmbRes.SelectedItem = currentResStr;

            cmbRes.SelectedIndexChanged += (s, e) =>
            {
                if (cmbRes.SelectedItem is string resStr)
                {
                    var parts = resStr.Split('x');
                    if (parts.Length == 2 && int.TryParse(parts[0], out int w) && int.TryParse(parts[1], out int h))
                    {
                        if (w != _virtualDisplay.Width || h != _virtualDisplay.Height)
                        {
                            ChangeResolution(w, h);
                        }
                    }
                }
            };

            // Desktop Split 50/50 Button
            _btnSplitDesktop = CreateBarButton("📌 50/50 [F5]", 80, (s, e) => ToggleSplitDesktop());

            // Window Management Buttons
            var btnFitWindow = CreateBarButton("📐 Ajustar [F6]", 85, (s, e) => FitActiveWindowInVirtualDisplay());
            var btnMoveWindow = CreateBarButton("📲 Traer [F7]", 80, (s, e) => MoveCurrentActiveWindowHere());
            var btnLaunchApp = CreateBarButton("🚀 Lanzar", 70, (s, e) => ShowLaunchMenu((Button)s!));

            // Snap / Fullscreen Buttons
            var btnSnapLeft = CreateBarButton("Izq [F10]", 65, (s, e) => SnapLeft());
            var btnSnapRight = CreateBarButton("Der [F9]", 65, (s, e) => SnapRight());
            var btnFullscreen = CreateBarButton("Compl [F11]", 75, (s, e) => ToggleFullscreen());

            _lblStatus = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = Color.FromArgb(180, 220, 255),
                Font = new Font("Segoe UI", 9f),
                Text = $"Monitor: {_virtualDisplay.Width}x{_virtualDisplay.Height} | 0 FPS"
            };

            // Add controls to top bar (in reverse order for DockStyle.Left layout)
            _topBar.Controls.Add(_lblStatus);
            _topBar.Controls.Add(btnFullscreen);
            _topBar.Controls.Add(btnSnapRight);
            _topBar.Controls.Add(btnSnapLeft);
            _topBar.Controls.Add(btnLaunchApp);
            _topBar.Controls.Add(btnMoveWindow);
            _topBar.Controls.Add(btnFitWindow);
            _topBar.Controls.Add(_btnSplitDesktop);
            _topBar.Controls.Add(cmbRes);
            _topBar.Controls.Add(cmbDisplays);
            _topBar.Controls.Add(_btnSeamlessMouse);
            _topBar.Controls.Add(_btnToggleInput);
            Controls.Add(_topBar);

            // Render Panel filling rest of window
            _renderPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black
            };
            Controls.Add(_renderPanel);
            _renderPanel.BringToFront();

            // Initialize Direct3D 11
            var (device, context, winrtDevice) = Direct3D11Helper.CreateDevice();
            _renderer = new D3D11Renderer(device, context);
            _renderer.InitializeSwapChain(_renderPanel.Handle, Math.Max(10, _renderPanel.Width), Math.Max(10, _renderPanel.Height));

            // Initialize Capture Service
            _captureService = new CaptureService(device, winrtDevice);
            _captureService.FrameArrived += OnFrameArrived;

            // Initialize Input Forwarder (disabled by default)
            _inputForwarder = new InputForwarder(_virtualDisplay) { IsInputActive = false };

            // Event Bindings
            _renderPanel.Resize += (s, e) =>
            {
                if (_renderPanel.Width > 0 && _renderPanel.Height > 0)
                {
                    _renderer.Resize(_renderPanel.Width, _renderPanel.Height);
                }
            };

            HookInputEvents();

            // FPS Timer
            _fpsTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _fpsTimer.Tick += (s, e) =>
            {
                double seconds = _fpsStopwatch.Elapsed.TotalSeconds;
                double fps = _frameCount / Math.Max(0.001, seconds);
                _frameCount = 0;
                _fpsStopwatch.Restart();

                _lblStatus.Text = $"{_virtualDisplay.Width}x{_virtualDisplay.Height} | {fps:F0} FPS";
            };
            _fpsTimer.Start();

            // Seamless Mouse Transition Timer (15ms polling = smooth 60Hz transition)
            _seamlessMouseTimer = new System.Windows.Forms.Timer { Interval = 15 };
            _seamlessMouseTimer.Tick += OnSeamlessMouseTick;
            _seamlessMouseTimer.Start();

            // Start capture on shown and register global emergency release & management hotkeys
            Shown += (s, e) =>
            {
                try
                {
                    _captureService.Start(_virtualDisplay.HMonitor);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Error al iniciar captura de monitor virtual:\n\n{ex.Message}",
                        "Error de Captura GPU",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }

                // Global hotkeys (active even if focus is inside other windows)
                RegisterHotKey(Handle, HOTKEY_RELEASE_ESC, MOD_CONTROL | MOD_ALT, VK_ESCAPE);
                RegisterHotKey(Handle, HOTKEY_RELEASE_F8, MOD_NONE, VK_F8);
                RegisterHotKey(Handle, HOTKEY_SEAMLESS, MOD_NONE, VK_F4);
                RegisterHotKey(Handle, HOTKEY_SPLIT, MOD_NONE, VK_F5);
                RegisterHotKey(Handle, HOTKEY_FIT_WINDOW, MOD_NONE, VK_F6);
                RegisterHotKey(Handle, HOTKEY_MOVE_WINDOW, MOD_NONE, VK_F7);

                // Auto-enable 50/50 split on startup so primary screen apps immediately dock & fit the other half!
                _isWorkAreaSplit = true;
                _btnSplitDesktop.Text = "📌 50/50: ON";
                _btnSplitDesktop.BackColor = Color.FromArgb(20, 110, 50);
                SetDesktopWorkAreaForDock(rightHalf: true);

                // Watch for new windows created while input is active or when opened on primary monitor
                _winEventProc = (hHook, eventType, hwnd, idObject, idChild, dwEventThread, dwmsEventTime) =>
                {
                    if (hwnd == IntPtr.Zero || hwnd == Handle || hwnd == GetDesktopWindow() || hwnd == GetShellWindow()) return;
                    if (!WindowManager.IsWindowVisible(hwnd)) return;
                    if (!WindowManager.GetWindowRect(hwnd, out var rect)) return;
                    if (rect.Width <= 120 || rect.Height <= 120) return;

                    if (_inputForwarder.IsInputActive)
                    {
                        // User is working inside virtual monitor: keep apps inside virtual monitor
                        if (rect.left < _virtualDisplay.Left || rect.right > _virtualDisplay.Left + _virtualDisplay.Width + 100)
                        {
                            WindowManager.FitWindowToDisplay(hwnd, _virtualDisplay);
                        }
                    }
                    else if (_isWorkAreaSplit && !_isFullscreen)
                    {
                        // User is working on primary monitor: ensure newly opened apps fit the dock half without covering viewer
                        if (rect.left < _virtualDisplay.Left - 50)
                        {
                            int halfW = _originalWorkArea.Width / 2;
                            bool rightHalfViewer = Bounds.X > 0;
                            int targetX = rightHalfViewer ? _originalWorkArea.left : _originalWorkArea.left + halfW;
                            int targetY = _originalWorkArea.top;
                            int targetW = halfW;
                            int targetH = _originalWorkArea.Height;

                            bool overlapsViewer = rightHalfViewer ? (rect.right > _originalWorkArea.left + halfW) : (rect.left < _originalWorkArea.left + halfW);
                            if (overlapsViewer || rect.Width > halfW + 15)
                            {
                                if (IsZoomed(hwnd))
                                {
                                    WindowManager.ShowWindow(hwnd, WindowManager.SW_RESTORE);
                                    WindowManager.SetWindowPos(hwnd, IntPtr.Zero, targetX, targetY, targetW, targetH, 0x0004 | 0x0040);
                                    WindowManager.ShowWindow(hwnd, WindowManager.SW_MAXIMIZE);
                                }
                                else
                                {
                                    WindowManager.SetWindowPos(hwnd, IntPtr.Zero, targetX, targetY, targetW, targetH, 0x0004 | 0x0040);
                                }
                            }
                        }
                    }
                };
                _winEventHook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, _winEventProc, 0, 0, WINEVENT_OUTOFCONTEXT);
            };

            // Clean up on form closing
            FormClosing += (s, e) =>
            {
                RestoreDesktopWorkArea();
                _seamlessMouseTimer.Stop();
                _seamlessMouseTimer.Dispose();
                if (_winEventHook != IntPtr.Zero)
                {
                    UnhookWinEvent(_winEventHook);
                    _winEventHook = IntPtr.Zero;
                }
                UnregisterHotKey(Handle, HOTKEY_RELEASE_ESC);
                UnregisterHotKey(Handle, HOTKEY_RELEASE_F8);
                UnregisterHotKey(Handle, HOTKEY_SEAMLESS);
                UnregisterHotKey(Handle, HOTKEY_SPLIT);
                UnregisterHotKey(Handle, HOTKEY_FIT_WINDOW);
                UnregisterHotKey(Handle, HOTKEY_MOVE_WINDOW);
                _fpsTimer?.Stop();
                _captureService.Stop();
                _captureService.Dispose();
                _renderer.Dispose();
            };
        }

        private void ToggleSeamlessMouse()
        {
            _seamlessMouseEnabled = !_seamlessMouseEnabled;
            if (_seamlessMouseEnabled)
            {
                _btnSeamlessMouse.Text = "⚡ Fluido: ON [F4]";
                _btnSeamlessMouse.BackColor = Color.FromArgb(20, 110, 50);
            }
            else
            {
                _btnSeamlessMouse.Text = "⚡ Fluido: OFF [F4]";
                _btnSeamlessMouse.BackColor = Color.FromArgb(50, 50, 55);
            }
        }

        private void OnSeamlessMouseTick(object? sender, EventArgs e)
        {
            if (!_seamlessMouseEnabled || _isFullscreen || WindowState == FormWindowState.Minimized) return;
            if (!WindowManager.GetCursorPos(out var pt)) return;

            bool rightHalfViewer = Bounds.X > 0;
            var panelScreenBounds = _renderPanel.RectangleToScreen(_renderPanel.ClientRectangle);

            if (_inputForwarder.IsInputActive)
            {
                // Cursor is currently inside the virtual display
                if (rightHalfViewer)
                {
                    // Viewer is on the right half, primary monitor apps are on the left
                    // Exit if user pushes mouse against the left edge of the virtual display
                    if (pt.x <= _virtualDisplay.Left + 4 || pt.x < _virtualDisplay.Left)
                    {
                        int exitX = panelScreenBounds.Left - 15;
                        float ratioY = (float)(pt.y - _virtualDisplay.Top) / Math.Max(1, _virtualDisplay.Height);
                        int exitY = panelScreenBounds.Top + (int)(ratioY * panelScreenBounds.Height);

                        _inputForwarder.IsInputActive = false;
                        UpdateInputUi();
                        WindowManager.SetCursorPos(exitX, exitY);
                    }
                }
                else
                {
                    // Viewer is on the left half, primary monitor apps are on the right
                    // Exit if user pushes mouse against the right edge of the virtual display
                    if (pt.x >= _virtualDisplay.Left + _virtualDisplay.Width - 5)
                    {
                        int exitX = panelScreenBounds.Right + 15;
                        float ratioY = (float)(pt.y - _virtualDisplay.Top) / Math.Max(1, _virtualDisplay.Height);
                        int exitY = panelScreenBounds.Top + (int)(ratioY * panelScreenBounds.Height);

                        _inputForwarder.IsInputActive = false;
                        UpdateInputUi();
                        WindowManager.SetCursorPos(exitX, exitY);
                    }
                }
            }
            else
            {
                // Cursor is currently on the primary screen
                // Enter if cursor moves into the virtual render panel
                if (panelScreenBounds.Contains(pt.x, pt.y))
                {
                    // Do not capture if user is holding/dragging mouse on primary screen
                    if (Control.MouseButtons != MouseButtons.None) return;

                    float ratioX = (float)(pt.x - panelScreenBounds.Left) / Math.Max(1, panelScreenBounds.Width);
                    float ratioY = (float)(pt.y - panelScreenBounds.Top) / Math.Max(1, panelScreenBounds.Height);

                    // Compute virtual enter coordinates
                    int enterX = _virtualDisplay.Left + (int)(ratioX * _virtualDisplay.Width);
                    int enterY = _virtualDisplay.Top + (int)(ratioY * _virtualDisplay.Height);

                    // Offset slightly inside to prevent bounce oscillation
                    if (rightHalfViewer && enterX <= _virtualDisplay.Left + 10)
                    {
                        enterX = _virtualDisplay.Left + 14;
                    }
                    else if (!rightHalfViewer && enterX >= _virtualDisplay.Left + _virtualDisplay.Width - 10)
                    {
                        enterX = _virtualDisplay.Left + _virtualDisplay.Width - 14;
                    }

                    _inputForwarder.IsInputActive = true;
                    UpdateInputUi();
                    WindowManager.SetCursorPos(enterX, enterY);
                }
            }
        }

        private void ToggleSplitDesktop()
        {
            _isWorkAreaSplit = !_isWorkAreaSplit;
            if (_isWorkAreaSplit)
            {
                _btnSplitDesktop.Text = "📌 50/50: ON";
                _btnSplitDesktop.BackColor = Color.FromArgb(20, 110, 50);
                SetDesktopWorkAreaForDock(rightHalf: Bounds.X > 0);
            }
            else
            {
                _btnSplitDesktop.Text = "📌 50/50: OFF";
                _btnSplitDesktop.BackColor = Color.FromArgb(40, 40, 45);
                RestoreDesktopWorkArea();
            }
        }

        private void SetDesktopWorkAreaForDock(bool rightHalf)
        {
            if (_originalWorkArea.Width <= 0)
            {
                SystemParametersInfo(SPI_GETWORKAREA, 0, ref _originalWorkArea, 0);
            }
            if (_originalWorkArea.Width <= 0) return;

            int halfW = _originalWorkArea.Width / 2;
            var targetArea = new DisplayInterop.RECT
            {
                top = _originalWorkArea.top,
                bottom = _originalWorkArea.bottom,
                left = rightHalf ? _originalWorkArea.left : _originalWorkArea.left + halfW,
                right = rightHalf ? _originalWorkArea.left + halfW : _originalWorkArea.right
            };

            SystemParametersInfo(SPI_SETWORKAREA, 0, ref targetArea, SPIF_SENDCHANGE);

            // Adapt open windows on primary monitor to the other half
            EnumWindows((hWnd, lParam) =>
            {
                if (hWnd == Handle) return true;
                if (!WindowManager.IsWindowVisible(hWnd)) return true;
                if (WindowManager.GetWindowRect(hWnd, out var r))
                {
                    // Check if window is located on the primary monitor
                    if (r.left >= _originalWorkArea.left - 20 && r.left < _originalWorkArea.right)
                    {
                        // If it overlaps into our viewer's half, move and resize it into targetArea
                        bool overlapsViewer = rightHalf ? (r.right > targetArea.right) : (r.left < targetArea.left);
                        if (overlapsViewer)
                        {
                            WindowManager.SetWindowPos(hWnd, IntPtr.Zero, targetArea.left, targetArea.top, targetArea.Width, targetArea.Height, 0x0004 | 0x0040);
                        }
                    }
                }
                return true;
            }, IntPtr.Zero);
        }

        private void RestoreDesktopWorkArea()
        {
            if (_originalWorkArea.Width > 0)
            {
                SystemParametersInfo(SPI_SETWORKAREA, 0, ref _originalWorkArea, SPIF_SENDCHANGE);
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                if (id == HOTKEY_RELEASE_ESC || id == HOTKEY_RELEASE_F8)
                {
                    ReleaseMouseToHost();
                    return;
                }
                if (id == HOTKEY_SEAMLESS)
                {
                    ToggleSeamlessMouse();
                    return;
                }
                if (id == HOTKEY_SPLIT)
                {
                    ToggleSplitDesktop();
                    return;
                }
                if (id == HOTKEY_FIT_WINDOW)
                {
                    FitActiveWindowInVirtualDisplay();
                    return;
                }
                if (id == HOTKEY_MOVE_WINDOW)
                {
                    MoveCurrentActiveWindowHere();
                    return;
                }
            }
            base.WndProc(ref m);
        }

        private void FitActiveWindowInVirtualDisplay()
        {
            IntPtr fg = WindowManager.GetForegroundWindow();
            if (fg != IntPtr.Zero && fg != Handle)
            {
                WindowManager.FitWindowToDisplay(fg, _virtualDisplay);
            }
        }

        private void MoveCurrentActiveWindowHere()
        {
            IntPtr fg = WindowManager.GetForegroundWindow();
            if (fg != IntPtr.Zero && fg != Handle)
            {
                WindowManager.FitWindowToDisplay(fg, _virtualDisplay);
            }
        }

        private void ChangeResolution(int width, int height)
        {
            var dm = new DisplayManager();
            if (dm.SetDisplayResolution(_virtualDisplay.DeviceName, width, height))
            {
                _captureService.Stop();
                Thread.Sleep(250);
                var updated = dm.GetDisplays().Find(d => d.DeviceName == _virtualDisplay.DeviceName);
                if (updated != null)
                {
                    _virtualDisplay = updated;
                    _inputForwarder.TargetDisplay = updated;
                }
                else
                {
                    _virtualDisplay.Width = width;
                    _virtualDisplay.Height = height;
                }
                Text = $"Monitor Virtual - {_virtualDisplay.DeviceName} ({_virtualDisplay.Width}x{_virtualDisplay.Height})";
                _lblStatus.Text = $"{_virtualDisplay.Width}x{_virtualDisplay.Height} | Reiniciando...";
                _captureService.Start(_virtualDisplay.HMonitor);
            }
        }

        private void ReleaseMouseToHost()
        {
            _inputForwarder.IsInputActive = false;
            UpdateInputUi();
            // Bring mouse safely to center of physical primary screen
            var primaryArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
            Cursor.Position = new Point(primaryArea.Width / 4, primaryArea.Height / 2);
        }

        private Button CreateBarButton(string text, int width, EventHandler onClick)
        {
            var btn = new Button
            {
                Text = text,
                Width = width,
                Dock = DockStyle.Left,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(40, 40, 45),
                ForeColor = Color.Gainsboro,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 8.5f)
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += onClick;
            return btn;
        }

        private void ToggleInputState()
        {
            _inputForwarder.IsInputActive = !_inputForwarder.IsInputActive;
            UpdateInputUi();
        }

        private void UpdateInputUi()
        {
            if (_inputForwarder.IsInputActive)
            {
                _btnToggleInput.Text = "🟢 En Virtual";
                _btnToggleInput.BackColor = Color.FromArgb(20, 120, 50);
            }
            else
            {
                _btnToggleInput.Text = "⚪ En Principal";
                _btnToggleInput.BackColor = Color.FromArgb(50, 50, 55);
            }
        }

        private void SwitchDisplay(DisplayInfo newDisplay)
        {
            if (_virtualDisplay.DeviceName == newDisplay.DeviceName) return;

            try
            {
                _captureService.Stop();
                _virtualDisplay = newDisplay;
                _inputForwarder.TargetDisplay = newDisplay;
                Text = $"Monitor Virtual - {_virtualDisplay.DeviceName} ({_virtualDisplay.Width}x{_virtualDisplay.Height})";
                _lblStatus.Text = $"{_virtualDisplay.Width}x{_virtualDisplay.Height} | Conectando...";
                _captureService.Start(_virtualDisplay.HMonitor);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cambiar a {newDisplay.DeviceName}:\n{ex.Message}");
            }
        }

        private void ShowLaunchMenu(Button parent)
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("📝 Bloc de Notas (Notepad)", null, (s, e) => WindowManager.LaunchAppOnDisplay("notepad.exe", _virtualDisplay));
            menu.Items.Add("🌐 Navegador Edge", null, (s, e) => WindowManager.LaunchAppOnDisplay("msedge.exe", _virtualDisplay));
            menu.Items.Add("📁 Explorador de Archivos", null, (s, e) => WindowManager.LaunchAppOnDisplay("explorer.exe", _virtualDisplay));
            menu.Items.Add("🔢 Calculadora", null, (s, e) => WindowManager.LaunchAppOnDisplay("calc.exe", _virtualDisplay));
            menu.Items.Add("💻 Terminal PowerShell", null, (s, e) => WindowManager.LaunchAppOnDisplay("powershell.exe", _virtualDisplay));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("📂 Abrir cualquier ejecutable (.exe)...", null, (s, e) =>
            {
                using var dlg = new OpenFileDialog
                {
                    Title = "Selecciona la aplicación a abrir en el monitor virtual",
                    Filter = "Programas (*.exe)|*.exe|Todos los archivos (*.*)|*.*"
                };
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    WindowManager.LaunchAppOnDisplay(dlg.FileName, _virtualDisplay);
                }
            });

            menu.Show(parent, new Point(0, parent.Height));
        }

        private void OnFrameArrived(Vortice.Direct3D11.ID3D11Texture2D texture)
        {
            _frameCount++;
            _renderer.RenderFrame(texture);
        }

        private (float rx, float ry, bool valid) GetNormalizedCoordinates(int mouseX, int mouseY)
        {
            var vp = _renderer.CurrentViewport;
            if (vp.Width <= 0 || vp.Height <= 0)
            {
                return ((float)mouseX / Math.Max(1, _renderPanel.Width), (float)mouseY / Math.Max(1, _renderPanel.Height), true);
            }

            float rx = (float)(mouseX - vp.X) / vp.Width;
            float ry = (float)(mouseY - vp.Y) / vp.Height;

            if (rx < 0f || rx > 1f || ry < 0f || ry > 1f)
            {
                return (0, 0, false);
            }
            return (rx, ry, true);
        }

        private void HookInputEvents()
        {
            _renderPanel.MouseMove += (s, e) =>
            {
                if (!_inputForwarder.IsInputActive) return;
                var (rx, ry, valid) = GetNormalizedCoordinates(e.X, e.Y);
                if (valid)
                {
                    _inputForwarder.SendMouseMove(rx, ry);
                }
            };

            _renderPanel.MouseDown += (s, e) =>
            {
                if (!_inputForwarder.IsInputActive) return;

                _renderPanel.Focus();
                var (rx, ry, valid) = GetNormalizedCoordinates(e.X, e.Y);
                if (valid)
                {
                    if (e.Button == MouseButtons.Left) _inputForwarder.SendLeftDown(rx, ry);
                    else if (e.Button == MouseButtons.Right) _inputForwarder.SendRightDown(rx, ry);
                    else if (e.Button == MouseButtons.Middle) _inputForwarder.SendMiddleDown(rx, ry);
                }
            };

            _renderPanel.MouseUp += (s, e) =>
            {
                if (!_inputForwarder.IsInputActive) return;
                var (rx, ry, valid) = GetNormalizedCoordinates(e.X, e.Y);
                if (valid)
                {
                    if (e.Button == MouseButtons.Left) _inputForwarder.SendLeftUp(rx, ry);
                    else if (e.Button == MouseButtons.Right) _inputForwarder.SendRightUp(rx, ry);
                    else if (e.Button == MouseButtons.Middle) _inputForwarder.SendMiddleUp(rx, ry);
                }
            };

            _renderPanel.MouseWheel += (s, e) =>
            {
                if (!_inputForwarder.IsInputActive) return;
                var (rx, ry, valid) = GetNormalizedCoordinates(e.X, e.Y);
                if (valid)
                {
                    _inputForwarder.SendWheel(rx, ry, e.Delta);
                }
            };

            KeyDown += (s, e) =>
            {
                // ESC ALWAYS releases mouse and exits control
                if (e.KeyCode == Keys.Escape)
                {
                    if (_inputForwarder.IsInputActive)
                    {
                        _inputForwarder.IsInputActive = false;
                        UpdateInputUi();
                        Cursor.Position = PointToScreen(new Point(Width / 2, Height / 2));
                        e.Handled = true;
                        return;
                    }
                }

                if (e.KeyCode == Keys.F4)
                {
                    ToggleSeamlessMouse();
                    e.Handled = true;
                    return;
                }
                if (e.KeyCode == Keys.F5)
                {
                    ToggleSplitDesktop();
                    e.Handled = true;
                    return;
                }
                if (e.KeyCode == Keys.F6)
                {
                    FitActiveWindowInVirtualDisplay();
                    e.Handled = true;
                    return;
                }
                if (e.KeyCode == Keys.F7)
                {
                    MoveCurrentActiveWindowHere();
                    e.Handled = true;
                    return;
                }
                if (e.KeyCode == Keys.F8)
                {
                    ToggleInputState();
                    e.Handled = true;
                    return;
                }
                if (e.KeyCode == Keys.F9)
                {
                    SnapRight();
                    e.Handled = true;
                    return;
                }
                if (e.KeyCode == Keys.F10)
                {
                    SnapLeft();
                    e.Handled = true;
                    return;
                }
                if (e.KeyCode == Keys.F11)
                {
                    ToggleFullscreen();
                    e.Handled = true;
                    return;
                }

                if (_inputForwarder.IsInputActive)
                {
                    _inputForwarder.SendKey((ushort)e.KeyValue, false);
                }
            };

            KeyUp += (s, e) =>
            {
                if (e.KeyCode is Keys.Escape or Keys.F4 or Keys.F5 or Keys.F6 or Keys.F7 or Keys.F8 or Keys.F9 or Keys.F10 or Keys.F11) return;
                if (_inputForwarder.IsInputActive)
                {
                    _inputForwarder.SendKey((ushort)e.KeyValue, true);
                }
            };
        }

        private void SnapRight()
        {
            var primaryArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
            int half = primaryArea.Width / 2;
            Bounds = new Rectangle(primaryArea.X + half, primaryArea.Y, half, primaryArea.Height);
            if (_isWorkAreaSplit) SetDesktopWorkAreaForDock(rightHalf: true);
        }

        private void SnapLeft()
        {
            var primaryArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
            int half = primaryArea.Width / 2;
            Bounds = new Rectangle(primaryArea.X, primaryArea.Y, half, primaryArea.Height);
            if (_isWorkAreaSplit) SetDesktopWorkAreaForDock(rightHalf: false);
        }

        private bool _isFullscreen = false;
        private FormWindowState _prevWindowState;
        private FormBorderStyle _prevBorderStyle;
        private Rectangle _prevBounds;

        private void ToggleFullscreen()
        {
            if (!_isFullscreen)
            {
                RestoreDesktopWorkArea();
                _prevWindowState = WindowState;
                _prevBorderStyle = FormBorderStyle;
                _prevBounds = Bounds;

                FormBorderStyle = FormBorderStyle.None;
                WindowState = FormWindowState.Maximized;
                _isFullscreen = true;
            }
            else
            {
                FormBorderStyle = _prevBorderStyle;
                WindowState = _prevWindowState;
                Bounds = _prevBounds;
                _isFullscreen = false;
                if (_isWorkAreaSplit) SetDesktopWorkAreaForDock(rightHalf: Bounds.X > 0);
            }
        }
    }
}
