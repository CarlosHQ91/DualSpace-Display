; Inno Setup Script para Pantalla Virtual
; Empaqueta la aplicación auto-contenida y el controlador de pantalla virtual

[Setup]
AppId={{C47F3389-70AA-4702-8BC3-431A2FD5FCE1}
AppName=Pantalla Virtual
AppVersion=1.0.0
AppPublisher=Antigravity
DefaultDirName={autopf}\PantallaVirtual
DefaultGroupName=Pantalla Virtual
AllowNoIcons=yes
OutputDir=dist\installer
OutputBaseFilename=Instalador_Pantalla_Virtual
Compression=lzma2/ultra64
SolidCompression=yes
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
DisableProgramGroupPage=yes

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
; Aplicación Principal Auto-Contenida
Source: "dist\app\VirtualDisplayApp.exe"; DestDir: "{app}"; Flags: ignoreversion

; Archivos del Controlador Virtual (VDD)
Source: "driver\*"; DestDir: "{app}\driver"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "driver\vdd_settings.xml"; DestDir: "C:\VirtualDisplayDriver"; Flags: ignoreversion uninsneveruninstall

[Icons]
Name: "{autoprograms}\Pantalla Virtual"; Filename: "{app}\VirtualDisplayApp.exe"
Name: "{autodesktop}\Pantalla Virtual"; Filename: "{app}\VirtualDisplayApp.exe"; Tasks: desktopicon

[Run]
; 1. Instalar el controlador virtual de forma silenciosa usando devcon
Filename: "{app}\driver\devcon.exe"; Parameters: "install ""{app}\driver\MttVDD.inf"" Root\MttVDD"; Flags: runhidden waituntilterminated; StatusMsg: "Instalando controlador de monitor virtual..."

; 2. Activar modo extender en Windows
Filename: "DisplaySwitch.exe"; Parameters: "/extend"; Flags: runhidden waituntilterminated

; 3. Opción para iniciar la aplicación de inmediato
Filename: "{app}\VirtualDisplayApp.exe"; Description: "Ejecutar Pantalla Virtual ahora"; Flags: postinstall nowait skipifsilent

[UninstallRun]
; Desinstalar el dispositivo virtual de Windows al desinstalar
Filename: "{app}\driver\devcon.exe"; Parameters: "remove Root\MttVDD"; Flags: runhidden waituntilterminated
