@echo off
title Vectrik - Deploy to Azure
set SRC=%~dp0
set APP_NAME=vectrik-dev
set RESOURCE_GROUP=vectrik-dev-rg

echo.
echo === Vectrik Azure Deploy ===
echo.

echo [1/4] Publishing self-contained for win-x64...
dotnet publish "%SRC%Vectrik.csproj" --configuration Release --runtime win-x64 --self-contained --output "%SRC%publish" --nologo
if %ERRORLEVEL% neq 0 (
    echo.
    echo PUBLISH FAILED.
    pause
    exit /b 1
)

echo [2/4] Creating deployment package...
powershell -Command "Compress-Archive -Path '%SRC%publish\*' -DestinationPath '%SRC%deploy.zip' -Force"

echo [3/4] Deploying to Azure (%APP_NAME%)...
az webapp deploy --name %APP_NAME% --resource-group %RESOURCE_GROUP% --src-path "%SRC%deploy.zip" --type zip --clean true --restart true
if %ERRORLEVEL% neq 0 (
    echo.
    echo DEPLOY FAILED. Make sure you are logged in: az login
    pause
    exit /b 1
)

echo [4/4] Verifying health...
timeout /t 10 /nobreak >nul
curl -s https://www.vectrik.com/healthz
echo.

echo.
echo ========================================
echo  DEPLOYED to https://www.vectrik.com
echo ========================================
echo.
pause
