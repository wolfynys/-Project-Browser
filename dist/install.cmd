@echo off
rem Installs Project Browser+ for Revit 2022 for the current user (no admin rights needed).
rem Put this file next to ProjectBrowserPlus.dll and double-click it. Close Revit first when updating.
setlocal
set "VER=2022"
if not "%~1"=="" set "VER=%~1"
set "ROOT=%APPDATA%\Autodesk\Revit\Addins\%VER%"
set "DIR=%ROOT%\ProjectBrowserPlus"
if not exist "%~dp0ProjectBrowserPlus.dll" (
  echo ProjectBrowserPlus.dll not found next to install.cmd
  pause & exit /b 1
)
mkdir "%DIR%" 2>nul
copy /Y "%~dp0ProjectBrowserPlus.dll" "%DIR%\" >nul || (echo Cannot copy DLL. Is Revit running? Close it and retry. & pause & exit /b 1)
if exist "%~dp0ProjectBrowserPlus.pdb" copy /Y "%~dp0ProjectBrowserPlus.pdb" "%DIR%\" >nul
rem remove the "downloaded from the Internet" mark so .NET loads the DLL
powershell -NoProfile -Command "Unblock-File -LiteralPath '%DIR%\ProjectBrowserPlus.dll'" 2>nul
(
echo ^<?xml version="1.0" encoding="utf-8"?^>
echo ^<RevitAddIns^>
echo   ^<AddIn Type="Application"^>
echo     ^<Name^>Project Browser+^</Name^>
echo     ^<Assembly^>%DIR%\ProjectBrowserPlus.dll^</Assembly^>
echo     ^<AddInId^>7E5A3C1D-4B2F-4E8A-9C6D-2F1B8A7D5E31^</AddInId^>
echo     ^<FullClassName^>ProjectBrowserPlus.App^</FullClassName^>
echo     ^<VendorId^>PBPL^</VendorId^>
echo     ^<VendorDescription^>Project Browser+ (open source add-in)^</VendorDescription^>
echo   ^</AddIn^>
echo ^</RevitAddIns^>
) > "%ROOT%\ProjectBrowserPlus.addin"
echo.
echo Installed to %DIR%
echo Restart Revit %VER%. Then open the new ribbon tab and press the Project Browser+ button.
pause
