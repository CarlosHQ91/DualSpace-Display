# Script para compilar la aplicacion e instalador con un solo clic
$ErrorActionPreference = "Stop"

Write-Host "===> 1. Publicando aplicacion en modo Single-File Auto-Contenido..." -ForegroundColor Cyan
dotnet publish src\VirtualDisplayApp\VirtualDisplayApp.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist\app
Copy-Item dist\app\VirtualDisplayApp.exe .\PantallaVirtual.exe -Force

Write-Host "`n===> 2. Compilando Instalador .EXE con Inno Setup..." -ForegroundColor Cyan
$iscc = "C:\Users\carlo\AppData\Local\Programs\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $iscc)) {
    $iscc = (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source
}

if ($iscc -and (Test-Path $iscc)) {
    & $iscc installer.iss
    Write-Host "`n[OK] Instalador generado exitosamente en: dist\installer\Instalador_Pantalla_Virtual.exe" -ForegroundColor Green
} else {
    Write-Error "No se encontro ISCC.exe. Asegurate de tener Inno Setup instalado."
}
