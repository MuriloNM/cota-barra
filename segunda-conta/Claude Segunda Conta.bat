@echo off
title Claude - second account
rem Opens a separate copy of Claude Desktop for a second account and shows any error.
rem To use another name, change the line below. Use the same name in the .vbs file.
set "CONTA=Trabalho"

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0abrir-conta.ps1" -Nome "%CONTA%" %1

if errorlevel 1 (
  echo.
  echo Something went wrong. Show this window to whoever is helping you.
  echo.
  pause
)
