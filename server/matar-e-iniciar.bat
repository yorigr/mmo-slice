@echo off
cd /d %~dp0
echo Matando processos node.exe antigos...
taskkill /f /im node.exe 2>nul
timeout /t 2 /nobreak >nul
echo Iniciando servidor...
echo Acesse: http://localhost:3000
node src\server.js
pause
