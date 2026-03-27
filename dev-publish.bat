@echo off
title Vectrik - Publish to Dev
set SRC=%~dp0
set PUB=C:\dev-publish\Vectrik

echo.
echo === Vectrik Dev Publish ===
echo Source : %SRC%
echo Output : %PUB%
echo.

echo [1/3] Publishing app...
dotnet publish "%SRC%." --configuration Release --output "%PUB%" --nologo
if %ERRORLEVEL% neq 0 (
    echo.
    echo PUBLISH FAILED.
    pause
    exit /b 1
)

echo [2/3] Copying databases...
if exist "%SRC%data" (
    xcopy "%SRC%data" "%PUB%\data" /E /I /Y /Q >nul
    echo Copied data folder.
) else (
    echo No data folder found - skipping.
)

echo [3/3] Copying run script...
copy /Y "%SRC%dev-run.bat" "%PUB%\dev-run.bat" >nul

echo.
echo ========================================
echo  DONE. Published to %PUB%
echo  Double-click dev-run.bat in that folder
echo  or run it from here to start the server.
echo ========================================
echo.
pause
