@echo off
title Compilando Instalador de Pantalla Virtual...
color 0b
echo ======================================================
echo    COMPILANDO INSTALADOR TODO EN UNO
echo ======================================================
echo.

cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build_installer.ps1"

if %ERRORLEVEL% equ 0 (
    echo.
    echo ======================================================
    echo  [EXITO] Instalador creado correctamente!
    echo ======================================================
    echo Abriendo carpeta del instalador...
    explorer /select,"%~dp0dist\installer\Instalador_Pantalla_Virtual.exe"
) else (
    echo.
    echo ======================================================
    echo  [ERROR] Ocurrio un fallo durante la compilacion.
    echo ======================================================
)

echo.
pause
