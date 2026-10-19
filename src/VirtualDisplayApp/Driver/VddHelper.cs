using System;
using System.IO;

namespace VirtualDisplayApp.Driver
{
    public static class VddHelper
    {
        public const string VddRepoUrl = "https://github.com/itsmikethetech/Virtual-Display-Driver/releases";

        public static string GetInstallationInstructions()
        {
            return """
========================================================================
GUÍA DE INSTALACIÓN DEL DRIVER VIRTUAL (IddCx - Virtual Display Driver)
========================================================================
1. Descarga el archivo ZIP más reciente desde:
   https://github.com/itsmikethetech/Virtual-Display-Driver/releases
2. Descomprime la carpeta en una ruta fija (ej: C:\VirtualDisplayDriver).
3. Haz clic derecho sobre el archivo 'vdd.inf' o ejecuta 'install.bat' como Administrador.
4. Windows reconocerá el monitor virtual inmediatamente.
5. Para configurar resoluciones: edita el archivo 'options.txt' en la carpeta del driver.
   Ejemplo:
   1920, 1080, 60
   1920, 1080, 120
========================================================================
""";
        }
    }
}
