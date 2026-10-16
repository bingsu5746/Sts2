@echo off
rem Double-click: build the mod and upload the update to the Steam Workshop.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0workshop_update.ps1"
pause
