@echo off
title Vectrik - LAN Dev Server
set PUB=C:\dev-publish\Vectrik
set ASPNETCORE_ENVIRONMENT=Development
set ASPNETCORE_URLS=http://192.168.7.103:5100

echo.
echo === Vectrik LAN Dev Server ===
echo URL  : http://192.168.7.103:5100
echo From : %PUB%
echo.

if not exist "%PUB%\Vectrik.exe" (
    echo ERROR: Published app not found.
    echo Run dev-publish.bat first.
    echo.
    pause
    exit /b 1
)

echo Open on any device: http://192.168.7.103:5100
echo Press Ctrl+C to stop.
echo.

cd /d "%PUB%"
"Vectrik.exe"
