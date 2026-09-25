@echo off
rem Double-click to build WeekTray.exe. Runs build.ps1; nothing to install.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1"
if errorlevel 1 (echo. & echo Build failed - see the message above.) else (echo. & echo Done. Double-click WeekTray.exe to start it.)
pause
