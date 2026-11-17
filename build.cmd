@echo off
rem Builds TidyLite.exe with the C# compiler that ships with Windows (.NET Framework 4.x).
rem No SDK, no NuGet, no web runtime. Usage: build.cmd [output.exe]
setlocal
set OUT=%~1
if "%OUT%"=="" set OUT=%~dp0TidyLite.exe
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
"%CSC%" /nologo /target:winexe /platform:x64 /optimize+ /codepage:65001 /win32icon:"%~dp0assets\TidyLite.ico" /win32manifest:"%~dp0src\app.manifest" /out:"%OUT%" ^
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
  "%~dp0src\*.cs"
