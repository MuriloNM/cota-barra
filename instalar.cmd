@echo off
rem Opens the pill. If the ready-made program is not here, compiles it with what ships with Windows. Installs nothing.
if exist "%~dp0bin\pilula.exe" goto abrir
call "%~dp0compilar.cmd" || exit /b 1
:abrir
call "%~dp0abrir-pilula.cmd"
