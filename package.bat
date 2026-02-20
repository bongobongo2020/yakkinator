@echo off
setlocal

set APP_NAME=TheYakkinator
set VERSION=1.0.0
set PROJECT=src\IndexTTSStudio\IndexTTSStudio.csproj
set PUBLISH_DIR=publish\win-x64
set RELEASE_DIR=release
set ZIP_NAME=%APP_NAME%-v%VERSION%-win-x64.zip

echo.
echo ============================================================
echo  The Yakkinator - Build and Package
echo ============================================================
echo  Version   : %VERSION%
echo  Output    : %RELEASE_DIR%\%ZIP_NAME%
echo ============================================================
echo.

:: Step 1: Build
echo [1/3] Building...
dotnet publish "%PROJECT%" ^
  --configuration Release ^
  --runtime win-x64 ^
  --self-contained true ^
  --output "%PUBLISH_DIR%" ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true ^
  -p:DebugType=none ^
  -p:DebugSymbols=false

if %ERRORLEVEL% NEQ 0 (
    echo [FAILED] Build failed.
    pause
    exit /b %ERRORLEVEL%
)
echo [OK] Build complete.

:: Step 2: Prepare release folder
echo.
echo [2/3] Preparing release package...
if exist "%RELEASE_DIR%" rd /s /q "%RELEASE_DIR%"
mkdir "%RELEASE_DIR%\%APP_NAME%"

:: Copy exe
copy "%PUBLISH_DIR%\IndexTTSStudio.exe" "%RELEASE_DIR%\%APP_NAME%\TheYakkinator.exe" >nul

:: Copy python server
mkdir "%RELEASE_DIR%\%APP_NAME%\python"
copy "python\api_server.py" "%RELEASE_DIR%\%APP_NAME%\python\api_server.py" >nul

:: Copy readme
copy "README.md" "%RELEASE_DIR%\%APP_NAME%\README.md" >nul

:: Create placeholder for checkpoints folder
mkdir "%RELEASE_DIR%\%APP_NAME%\checkpoints"
echo Place your IndexTTS-2 model files here. See README.md for download instructions. > "%RELEASE_DIR%\%APP_NAME%\checkpoints\PUT_MODEL_FILES_HERE.txt"

echo [OK] Package prepared.

:: Step 3: Zip
echo.
echo [3/3] Creating zip...
powershell -NoProfile -Command "Compress-Archive -Path '%RELEASE_DIR%\%APP_NAME%' -DestinationPath '%RELEASE_DIR%\%ZIP_NAME%' -Force"

if %ERRORLEVEL% NEQ 0 (
    echo [FAILED] Zip creation failed.
    pause
    exit /b %ERRORLEVEL%
)

echo [OK] Zip created.
echo.
echo ============================================================
echo  Done! Upload this file to GitHub Releases:
echo  %RELEASE_DIR%\%ZIP_NAME%
echo ============================================================
echo.
explorer "%~dp0%RELEASE_DIR%"
pause
