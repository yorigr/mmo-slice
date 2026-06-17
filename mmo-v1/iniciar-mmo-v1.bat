@echo off
cd /d %~dp0
set NODE_EXE=..\mmo-slice-v2\mmo-slice\node-v24.16.0-win-x64\node.exe
if not exist "%NODE_EXE%" set NODE_EXE=node
echo Iniciando MMO v1...
echo Acesse: http://localhost:3000
"%NODE_EXE%" src\server.js
pause
