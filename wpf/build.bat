@echo off
setlocal
cd /d "%~dp0"
set MSBUILD=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe
if not exist "%MSBUILD%" set MSBUILD=%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe
"%MSBUILD%" PipelineGenWpf.csproj /p:Configuration=Release /v:minimal
if %ERRORLEVEL% NEQ 0 (
    echo Build failed with exit code %ERRORLEVEL%
    exit /b %ERRORLEVEL%
)
echo.
echo Build succeeded: %~dp0bin\Release\PipelineGenWpf.exe
endlocal
