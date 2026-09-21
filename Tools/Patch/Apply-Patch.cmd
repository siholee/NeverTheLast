@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Apply-Patch.ps1"
if errorlevel 1 (
  echo.
  echo Patch failed. The installed files were left unchanged or restored.
  pause
  exit /b 1
)
echo.
pause
