@echo off
REM ============================================================================
REM  Build VirtualGarage.dll with the Roslyn C# compiler directly (no .NET SDK).
REM  Outputs bin\VirtualGarage.dll and copies it (plus MySql.Data.dll) to dist\.
REM ============================================================================
setlocal enabledelayedexpansion

set CSC="C:\Program Files\Microsoft Visual Studio\2022\Preview\MSBuild\Current\Bin\Roslyn\csc.exe"
set UM=D:\SteamLibrary\steamapps\common\Unturned\Unturned_Data\Managed
set RD=D:\SteamLibrary\steamapps\common\Unturned\Extras\Rocket.Unturned
set FW=C:\Windows\Microsoft.NET\Framework64\v4.0.30319

if not exist "%~dp0bin" mkdir "%~dp0bin"

REM Gather every .cs file under the project (skip bin/obj/dist).
set SRC=
for /r "%~dp0" %%f in (*.cs) do (
  echo %%f | findstr /i "\\bin\\ \\obj\\ \\dist\\" >nul || set SRC=!SRC! "%%f"
)

%CSC% /nologo /noconfig /nostdlib+ /target:library /langversion:latest /optimize+ ^
  /out:"%~dp0bin\VirtualGarage.dll" ^
  /reference:"%FW%\mscorlib.dll" ^
  /reference:"%FW%\System.dll" ^
  /reference:"%FW%\System.Core.dll" ^
  /reference:"%FW%\System.Xml.dll" ^
  /reference:"%FW%\System.Data.dll" ^
  /reference:"%UM%\netstandard.dll" ^
  /reference:"%UM%\Assembly-CSharp.dll" ^
  /reference:"%UM%\UnityEngine.dll" ^
  /reference:"%UM%\UnityEngine.CoreModule.dll" ^
  /reference:"%UM%\UnityEngine.PhysicsModule.dll" ^
  /reference:"%UM%\com.rlabrecque.steamworks.net.dll" ^
  /reference:"%RD%\Rocket.API.dll" ^
  /reference:"%RD%\Rocket.Core.dll" ^
  /reference:"%RD%\Rocket.Unturned.dll" ^
  /reference:"%~dp0libs\MySql.Data.dll" ^
  !SRC!

if errorlevel 1 ( echo BUILD FAILED & exit /b 1 )

if not exist "%~dp0dist" mkdir "%~dp0dist"
copy /Y "%~dp0bin\VirtualGarage.dll" "%~dp0dist\VirtualGarage.dll" >nul
copy /Y "%~dp0libs\MySql.Data.dll" "%~dp0dist\MySql.Data.dll" >nul
echo.
echo Built: dist\VirtualGarage.dll
endlocal
