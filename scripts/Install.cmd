@echo off
setlocal
set "SCRIPT_DIR=%~dp0"
set "POWERSHELL=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"
set "BILICOPILOT_FROM_INSTALL_CMD=1"

rem Keep Windows PowerShell 5.1 away from PowerShell 7 modules inherited from
rem the parent terminal, which otherwise breaks the certificate provider.
set "PSModulePath=%USERPROFILE%\Documents\WindowsPowerShell\Modules;%ProgramFiles%\WindowsPowerShell\Modules;%SystemRoot%\System32\WindowsPowerShell\v1.0\Modules"

rem Bypass the execution policy for this process so Mark-of-the-Web on the
rem downloaded archive cannot block the unsigned install script.
"%POWERSHELL%" -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%Install.ps1"
set "EXIT_CODE=%ERRORLEVEL%"

if not "%EXIT_CODE%"=="0" (
    echo.
    echo Installation failed. Exit code: %EXIT_CODE%
    pause
)

exit /b %EXIT_CODE%
