@echo off
title Push GitHub — MMO MMORPG
cd /d "%~dp0"

set NODE_EXE=%~dp0_archive\mmo-slice\node-v24.16.0-win-x64\node.exe
if not exist "%NODE_EXE%" (
    echo ERRO: Node portatil nao encontrado em:
    echo %NODE_EXE%
    pause
    exit /b 1
)

echo Usando Node: %NODE_EXE%
echo Enviando arquivos para github.com/yorigr/mmo-slice...
echo.

"%NODE_EXE%" push-github.js

echo.
pause
