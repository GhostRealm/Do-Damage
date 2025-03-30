@echo off

set "current_dir=%~dp0"
cd /d "%current_dir%WorldServer\bin\Debug\net8.0"
start "" "WorldServer.exe"

start "" "%current_dir%Redis\redis-server.exe"

echo Running App.exe with admin privileges...
cd /d "%current_dir%App\bin\Debug\net8.0"

start "" "App.exe"
exit
