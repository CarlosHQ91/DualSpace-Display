using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using VirtualDisplayApp.Display;
using VirtualDisplayApp.Driver;
using VirtualDisplayApp.UI;

namespace VirtualDisplayApp
{
    internal class Program
    {
        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int dwProcessId);

        private const int ATTACH_PARENT_PROCESS = -1;

        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                if (AttachConsole(ATTACH_PARENT_PROCESS))
                {
                    Console.OutputEncoding = System.Text.Encoding.UTF8;
                }
            }
            catch
            {
                // Ignorar si no hay consola asociada (ej. al abrir con doble clic desde el Explorador de Windows)
            }

            bool scanOnce = args.Length > 0 && args[0] == "--scan-once";

            var manager = new DisplayManager();

            if (scanOnce)
            {
                Console.WriteLine("==========================================================");
                Console.WriteLine(" VIRTUAL SCREEN APP - DIAGNÓSTICO DE PANTALLAS");
                Console.WriteLine("==========================================================\n");

                Console.WriteLine("Dispositivos de pantalla registrados en Windows:");
                foreach (var deviceStr in manager.GetAllDevices())
                {
                    Console.WriteLine("  " + deviceStr);
                }
                Console.WriteLine();

                Console.WriteLine("Escaneando monitores activos en el escritorio de Windows...\n");
                var displays = manager.GetDisplays();
                for (int i = 0; i < displays.Count; i++)
                {
                    var d = displays[i];
                    Console.WriteLine($"[{i + 1}] {d}");
                }

                var vDisplay = manager.FindVirtualDisplay();
                if (vDisplay != null)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("\n>> MONITOR VIRTUAL DETECTADO CON ÉXITO <<");
                    Console.ResetColor();
                    Console.WriteLine($"Dispositivo : {vDisplay.DeviceName}");
                    Console.WriteLine($"Resolución  : {vDisplay.Width}x{vDisplay.Height} @ {vDisplay.RefreshRate}Hz");
                    Console.WriteLine($"Handle Win32: 0x{vDisplay.HMonitor.ToString("X")}");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("\n>> NO SE ENCONTRÓ MONITOR VIRTUAL ACTIVO <<");
                    Console.ResetColor();
                }

                return;
            }

            // Normal Execution: Launch GUI Viewer
            try
            {
                var virtualDisplay = manager.FindVirtualDisplay();
                if (virtualDisplay != null)
                {
                    Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new ViewerForm(virtualDisplay));
                }
                else
                {
                    MessageBox.Show(
                        "No se detectó ningún monitor virtual activo.\n\nPor favor presiona la tecla Windows + P y selecciona 'Extender', o verifica que el monitor virtual esté habilitado.",
                        "Virtual Screen - Monitor no encontrado",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Ocurrió un error al iniciar la aplicación:\n\n" + ex.Message + "\n\n" + ex.StackTrace,
                    "Virtual Screen - Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}
