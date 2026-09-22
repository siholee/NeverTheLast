@echo off
setlocal
rem Clear the inherited PSModulePath: when this is launched from a pwsh 7 terminal it
rem hides Windows PowerShell's own modules (Get-FileHash, ConvertFrom-Json).
set "PSModulePath="
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Apply-Patch.ps1"
if errorlevel 1 (
  echo.
  echo Patch failed. The installed files were left unchanged or restored.
  pause
  exit /b 1
)
echo.
pause
