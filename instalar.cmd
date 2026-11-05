@echo off
rem Compila com o compilador que ja vem no Windows e abre a pilula. Nao instala nada.
call "%~dp0compilar.cmd" || exit /b 1
call "%~dp0abrir-pilula.cmd"
