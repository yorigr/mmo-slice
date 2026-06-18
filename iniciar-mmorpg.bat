@echo off
title MMO — Servidor Principal
cd /d "%~dp0server"

:: Node portátil bundled no projeto (não precisa instalar Node globalmente)
set NODE_EXE=%~dp0_archive\mmo-slice\node-v24.16.0-win-x64\node.exe
if not exist "%NODE_EXE%" set NODE_EXE=node

echo Usando Node: %NODE_EXE%
echo.

if not exist node_modules (
    echo Instalando dependencias...
    set NPM_CMD=%~dp0_archive\mmo-slice\node-v24.16.0-win-x64\npm.cmd
    if not exist "%NPM_CMD%" set NPM_CMD=npm
    "%NPM_CMD%" install
    echo.
)

echo Iniciando MMO v1...
echo Acesse: http://localhost:3000
echo.

start "" "http://localhost:3000"
timeout /t 2 /nobreak >nul
start "" "http://localhost:3000"

"%NODE_EXE%" src\server.js
pause
