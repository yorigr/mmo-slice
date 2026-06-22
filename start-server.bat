@echo off
cd /d "%~dp0server"
start "MMORPG Server" cmd /k "npm start"
