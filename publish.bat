@echo off
setlocal

set PROJECT=src\IndexTTSStudio\IndexTTSStudio.csproj
set OUTPUT=publish\win-x64
set RID=win-x64
set CONFIG=Release

echo.
echo ============================================================
echo  IndexTTS Studio - Publish
echo ============================================================
echo  Config    : %CONFIG%
echo  Runtime   : %RID%
echo  Output    : %OUTPUT%
echo ============================================================
echo.

dotnet publish "%PROJECT%" ^
  --configuration %CONFIG% ^
  --runtime %RID% ^
  --self-contained true ^
  --output "%OUTPUT%" ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true ^
  -p:DebugType=none ^
  -p:DebugSymbols=false

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [FAILED] Build failed with error code %ERRORLEVEL%.
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo [SUCCESS] Published to: %~dp0%OUTPUT%
echo.
explorer "%~dp0%OUTPUT%"

pause
