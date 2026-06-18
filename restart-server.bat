@echo off
title MMO — Reiniciando servidor
echo Matando processos node.exe existentes...
taskkill /F /IM node.exe 2>nul
echo Aguardando 2 segundos...
timeout /t 2 /nobreak >nul

cd /d "%~dp0server"
set NODE_EXE=%~dp0_archive\mmo-slice\node-v24.16.0-win-x64\node.exe
if not exist "%NODE_EXE%" set NODE_EXE=node

echo Iniciando MMO v1...
echo Acesse: http://localhost:3000
echo.
"%NODE_EXE%" src\server.js
pause
