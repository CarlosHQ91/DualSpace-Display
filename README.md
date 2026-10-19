# 🖥️ DualSpace-Display (Pantalla Virtual)

> **Un segundo monitor virtual completo, interactivo y acelerado por hardware, integrado en tiempo real dentro de tu pantalla física.**

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![DirectX 11](https://img.shields.io/badge/DirectX-11_GPU_Accelerated-0078D7?logo=windows)](https://learn.microsoft.com/en-us/windows/win32/direct3d11/)
[![Platform](https://img.shields.io/badge/Platform-Windows_10%2F11_(x64)-00adef?logo=windows)](https://microsoft.com)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

---

## 💡 ¿Qué es DualSpace-Display?

A diferencia de los escritorios virtuales convencionales de Windows (`Win + Ctrl + D`) o las herramientas de ordenamiento de ventanas (*FancyZones*), **SplitVirtual** crea un **monitor de hardware virtual real** reconocido por el sistema operativo mediante el estándar de controladores **IddCx** (Indirect Display Driver).

La aplicación captura este monitor secundario en tiempo real a **60 FPS** utilizando la GPU vía **Direct3D 11** y **Windows.Graphics.Capture**, mostrándolo en una ventana acoplada a la mitad de tu monitor principal con control de ratón y teclado totalmente transparente.

---

## ✨ Características Principales

- **🎮 Pipeline Gráfico Direct3D 11**: Renderizado con texturas compartidas y shaders de píxeles acelerados por GPU para mínima latencia (< 5ms) y cero sobrecarga de CPU.
- **🐭 Transición Fluida del Cursor (Auto-Capture / Auto-Release)**:
  - Al deslizar el cursor desde el escritorio principal hacia el visor, se captura automáticamente y se teletransporta a las coordenadas correspondientes del monitor virtual.
  - Al deslizar el cursor hacia el borde izquierdo del monitor virtual, se libera y regresa instantáneamente al escritorio principal.
  - Cuenta con un umbral de histéresis de 8 píxeles para evitar parpadeos u oscilaciones en los bordes.
- **📐 Acoplamiento de Espacio de Trabajo (`SPI_SETWORKAREA`)**:
  - Al iniciar, la aplicación reserva la mitad izquierda de la pantalla física para tus aplicaciones de Windows habituales y la mitad derecha para el visor virtual.
  - Al maximizar o abrir cualquier programa en la pantalla principal, respeta automáticamente este límite sin solaparse con el monitor virtual.
- **🖥️ Detección Inteligente y Prevención de Bucles**:
  - Filtro estricto que impide seleccionar o reflejar la pantalla primaria (evita el efecto de espejo infinito).
  - Compatible con múltiples resoluciones (800x600, 1024x768, 1280x720, 1920x1080) y escalado **PerMonitorV2** para pantallas 4K y TVs.
- **📦 Instalador Autónomo Todo-en-Uno**:
  - Un único instalador `.exe` que despliega el driver de pantalla virtual en modo silencioso (`devcon.exe`), configura el entorno de Windows, activa el escritorio extendido y crea los accesos directos sin requerir la instalación previa de .NET.

---

## 🏗️ Arquitectura Técnica

```
┌────────────────────────────────────────────────────────┐
│                   Sistema Windows                      │
│                                                        │
│  ┌─────────────────────────┐  ┌─────────────────────┐  │
│  │   Monitor Físico (1)    │  │ Monitor Virtual (2) │  │
│  │  (Escritorio Primario)  │  │ (IddCx / MttVDD)    │  │
│  └────────────┬────────────┘  └──────────┬──────────┘  │
│               │                          │             │
│               │            Windows.Graphics.Capture    │
│               │                          │             │
│  ┌────────────▼──────────────────────────▼──────────┐  │
│  │              SplitVirtual App                    │  │
│  │                                                  │  │
│  │  ┌──────────────────────┐  ┌──────────────────┐  │  │
│  │  │  Área de Trabajo     │  │ Direct3D 11      │  │  │
│  │  │  Física (50%)        │  │ Render Panel     │  │  │
│  │  │  SPI_SETWORKAREA     │  │ SwapChain / HLSL │  │  │
│  │  └──────────────────────┘  └──────────────────┘  │  │
│  │                                                  │  │
│  │        Mouse Hook & Coordinate Normalization     │  │
│  └──────────────────────────────────────────────────┘  │
└────────────────────────────────────────────────────────┘
```

### Componentes del Código (`src/VirtualDisplayApp`):

1. **`Display/`**:
   - `DisplayManager.cs`: Enumeración de monitores con `EnumDisplayMonitors`, consulta de adaptadores con `EnumDisplayDevices` y cambio de resolución con `ChangeDisplaySettingsEx`.
2. **`Capture/`**:
   - `GraphicsCaptureSession`: Interfaz moderna de captura de Windows a 60 FPS con exclusión de cursor propia o incrustada.
   - `Direct3D11Helper.cs`: Creación del dispositivo DirectX 11 e interoperabilidad WinRT (`CreateDirect3D11DeviceFromDXGIDevice`).
3. **`Rendering/`**:
   - `D3D11Renderer.cs`: Gestión del SwapChain, SamplerStates, Vertex Buffer y Shaders compilados mediante `Vortice.D3DCompiler`.
4. **`Input/`**:
   - `InputController.cs`: Normalización de coordenadas relativas entre el panel de visualización y el escritorio virtual, e inyección precisa mediante `SendInput` de Win32.
5. **`UI/`**:
   - `ViewerForm.cs`: Ventana principal sin bordes ni parpadeos, temporizador de transición de mouse y control de acoplamiento de pantalla.

---

## 📂 Estructura del Repositorio

```
├── driver/                    # Archivos del controlador de pantalla virtual (IddCx)
│   ├── devcon.exe             # Herramienta de despliegue de dispositivos
│   ├── MttVDD.dll             # Biblioteca del driver indirecto
│   ├── MttVDD.inf             # Manifiesto de instalación del driver
│   ├── mttvdd.cat             # Firma del paquete del driver
│   └── vdd_settings.xml       # Configuración de resoluciones del driver
├── src/                       # Código fuente C# (.NET 8)
│   └── VirtualDisplayApp/     # Proyecto principal WinForms + DirectX 11
├── build_installer.ps1        # Script para compilar la app y el instalador
├── installer.iss              # Script de Inno Setup 6 para empaquetar el setup
├── COMPILAR_INSTALADOR.exe    # Ejecutable de 1 clic para generar el instalador
├── EJECUTAR_APP.bat           # Acceso rápido para ejecutar la app
└── README.md                  # Esta documentación técnica
```

---

## ⚙️ Requisitos de Compilación

- **Windows 10 (versión 1903+) o Windows 11 (x64)**.
- **.NET 8.0 SDK** (o superior).
- **Inno Setup 6** (opcional, solo para generar el instalador distribuible).

---

## 🚀 Compilación y Ejecución

### Opción 1: Ejecutar en Desarrollo
```powershell
dotnet run --project src\VirtualDisplayApp\VirtualDisplayApp.csproj
```

### Opción 2: Publicar Versión Independiente (Single-File)
```powershell
dotnet publish src\VirtualDisplayApp\VirtualDisplayApp.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist\app
```

### Opción 3: Generar Instalador Autónomo Todo-en-Uno
Solo haz doble clic en:
> **`COMPILAR_INSTALADOR.exe`** (o ejecuta `.\build_installer.ps1` en PowerShell).

El instalador final se generará en:
```
dist/installer/Instalador_Pantalla_Virtual.exe
```

---

## 🛠️ Tecnologías Empleadas

- **Lenguaje**: C# 12 / .NET 8.0
- **Gráficos**: [Vortice.Windows](https://github.com/amerkoleci/Vortice.Windows) (Direct3D 11, DXGI, D3DCompiler)
- **Captura**: Windows.Graphics.Capture (WinRT Interop)
- **Controlador Virtual**: Windows Indirect Display Driver Model (IddCx)
- **Instalador**: Inno Setup 6

---

## 📄 Licencia

Este proyecto está bajo la Licencia MIT. Consulta el archivo `LICENSE` para más información.
