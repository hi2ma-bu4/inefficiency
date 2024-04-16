@echo off
PATH="%WINDIR%\Microsoft.NET\Framework\v1.0.3705";%PATH%
PATH="%WINDIR%\Microsoft.NET\Framework\v1.1.4322";%PATH%
PATH="%WINDIR%\Microsoft.NET\Framework\v2.0.50727";%PATH%
PATH="%WINDIR%\Microsoft.NET\Framework\v3.0";%PATH%
PATH="%WINDIR%\Microsoft.NET\Framework\v3.5";%PATH%
PATH="%WINDIR%\Microsoft.NET\Framework\v4.0.30319";%PATH%

cd %~dp0

Set proPath=inefficiency

Set TYPEs="debug"
rem debug	デバッグ表示
rem production	なし

if %TYPEs%=="debug" (
	csc "%proPath%.cs" /win32icon:icon.ico /reference:PlugInAttribute.dll
) else if %TYPEs%=="production" (
	csc /target:winexe "%proPath%.cs" /win32icon:icon.ico /reference:PlugInAttribute.dll
) else (
	echo. StartUpError!
	PAUSE
	exit /b 0
)

if not %errorlevel% == 0 (
	echo. Error!
	PAUSE
) else (
	start "" "%proPath%.exe"
)
exit /b 0