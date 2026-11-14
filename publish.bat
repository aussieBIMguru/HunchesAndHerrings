@echo off
rem ---------------------------------------------------------------------------
rem  publish.bat - builds a Hunches & Herrings release: a Release build, the
rem  self-contained game and artifacts\HunchesAndHerrings-Setup-<version>.exe.
rem  Needs the .NET 10 SDK and Inno Setup 6.
rem
rem  Passes any arguments on to build\publish.ps1, for example:
rem    publish.bat -SkipTests
rem    publish.bat -InnoCompiler "D:\Tools\Inno Setup 6\ISCC.exe"
rem    publish.bat -Sign -CertThumbprint <thumbprint>
rem ---------------------------------------------------------------------------
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build\publish.ps1" %*
if errorlevel 1 (
    echo.
    echo Publish FAILED - see the messages above.
)
pause
