Actúa como un ingeniero senior de Windows con experiencia en C#/.NET, DirectX y drivers de pantalla. Quiero que me ayudes a construir, paso a paso, una aplicación para Windows 10/11 que emule tener un segundo monitor conectado, pero viéndolo dentro de un solo monitor físico.



\## Objetivo

Una app que cree un monitor virtual que Windows reconozca como pantalla real (escritorio extendido, con sus propias ventanas y aplicaciones independientes), y que muestre ese monitor en tiempo real en una región de mi monitor físico (por ejemplo la mitad de la pantalla), de forma interactiva, como si estuviera conectado con otro cable.



No quiero el clásico "acomodar ventanas" ni los escritorios virtuales de Windows (Win+Ctrl+D), porque necesito ver ambos entornos al mismo tiempo.



\## Arquitectura propuesta

1\. Monitor virtual: usar un Indirect Display Driver (IddCx) ya existente, como Virtual Display Driver o Parsec VDD. Que la app pueda instalar, activar, desactivar y configurar la resolución y frecuencia del monitor virtual.

2\. Captura: capturar el monitor virtual con Windows.Graphics.Capture o Desktop Duplication API (DXGI), acelerado por GPU, a 60 fps.

3\. Visualización: renderizar los fotogramas en una ventana de la app (WPF o WinUI 3 con Direct3D 11), redimensionable y acoplable a la mitad de la pantalla física.

4\. Entrada: reenviar mouse y teclado de esa ventana al monitor virtual, traduciendo coordenadas y usando SendInput. Incluir un atajo global para alternar el control entre ambos entornos.



\## Requisitos funcionales

\- Detectar si el driver virtual está instalado y guiarme en la instalación si no lo está.

\- Crear y quitar el monitor virtual desde la interfaz de la app.

\- Elegir resolución (1920x1080 por defecto) y frecuencia de actualización.

\- Lanzar aplicaciones directamente en el monitor virtual.

\- Vista en tiempo real con latencia mínima y bajo consumo de CPU/GPU.

\- Cursor visible dentro de la vista y bordes claros de cuál pantalla tiene el foco.

\- Manejo de DPI, escalado y múltiples monitores físicos.



\## Stack

\- Lenguaje: C# (.NET 8) con interop a Win32/DirectX (Vortice.Windows o SharpDX).

\- Solo usar un driver existente en esta primera fase; no escribir driver propio.



\## Forma de trabajar

1\. Primero, confirma la arquitectura y señala riesgos o limitaciones (firma de drivers, permisos de administrador, bucles de captura, etc.).

2\. Después, entrégame un plan por fases: (a) crear el monitor virtual, (b) capturarlo, (c) mostrarlo, (d) reenviar entrada, (e) pulido.

3\. Implementa cada fase con código completo y funcional, indicando estructura del proyecto, paquetes NuGet, y cómo compilar y probar.

4\. Al terminar cada fase, dime cómo verificar que funciona antes de pasar a la siguiente.

5\. Evita el feedback visual infinito (que la ventana de la app capture su propia imagen): excluye la ventana de la captura o haz que la captura apunte solo al monitor virtual.



Empieza con la fase (a) y la confirmación de arquitectura.

