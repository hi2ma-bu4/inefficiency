@echo off
setlocal enabledelayedexpansion
PATH="%WINDIR%\Microsoft.NET\Framework\v1.0.3705";%PATH%
PATH="%WINDIR%\Microsoft.NET\Framework\v1.1.4322";%PATH%
PATH="%WINDIR%\Microsoft.NET\Framework\v2.0.50727";%PATH%
PATH="%WINDIR%\Microsoft.NET\Framework\v3.0";%PATH%
PATH="%WINDIR%\Microsoft.NET\Framework\v3.5";%PATH%
PATH="%WINDIR%\Microsoft.NET\Framework\v4.0.30319";%PATH%

cd %~dp0

for /r %%f in (*.cs) do (
	Set file_path=%%f
	csc /target:library "!file_path!" /reference:"..\PlugInAttribute.dll"

	if not !errorlevel! == 0 (
		echo. Error!
		PAUSE
		exit /b 1
	)
)
echo. compile end.
exit /b 0