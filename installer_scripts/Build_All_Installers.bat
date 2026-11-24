@echo off
chcp 65001 >nul
setlocal
set "ISCC=%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" (
    echo [ERROR] No se encontro Inno Setup 6 (ISCC.exe).
    pause
    exit /b 1
)

echo ========================================================================
echo   Compilando Instalador 1: GTX 1650 (Perfil Probado Win 10/11)...
echo ========================================================================
"%ISCC%" "%~dp0Setup_GTX1650_15kHz_240p_224p.iss" || exit /b 1

echo.
echo ========================================================================
echo   Compilando Instalador 2: NVIDIA Experimental (Otras GPUs Win 10/11)...
echo ========================================================================
"%ISCC%" "%~dp0Setup_NVIDIA_Experimental_15kHz_240p_224p.iss" || exit /b 1

echo.
echo [OK] Ambos instaladores compilados en build_output\
dir "%~dp0..\build_output\*.exe"
endlocal
