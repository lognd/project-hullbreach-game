@echo off
rem Shared bootstrap for doctor.cmd and play.cmd: make sure uv exists, then
rem run the Python tool. All real logic lives in tools\hbtools (one
rem implementation for every OS); this file only finds or installs uv.
setlocal
set "HB_TOOL=%~1"
shift
set "HB_HERE=%~dp0"
set "HB_ARGS="
:collect
if "%~1"=="" goto args_done
set HB_ARGS=%HB_ARGS% %1
shift
goto collect
:args_done

set "HB_UV="
for %%U in (uv.exe) do set "HB_UV=%%~$PATH:U"
if not defined HB_UV if exist "%USERPROFILE%\.local\bin\uv.exe" set "HB_UV=%USERPROFILE%\.local\bin\uv.exe"
if not defined HB_UV if exist "%USERPROFILE%\.cargo\bin\uv.exe" set "HB_UV=%USERPROFILE%\.cargo\bin\uv.exe"
if defined HB_UV goto have_uv

echo The Hullbreach tools need a small helper program called uv.
echo uv downloads the right Python for you; it does not change anything else.
echo It is installed with the official installer from https://astral.sh/uv
echo (the command is: powershell -ExecutionPolicy ByPass -c "irm https://astral.sh/uv/install.ps1 ^| iex").
echo.
set "HB_ANSWER="
set /p "HB_ANSWER=Install uv now? Type y and press Enter for yes [y/N]: "
if /i "%HB_ANSWER%"=="y" goto install_uv
if /i "%HB_ANSWER%"=="yes" goto install_uv
echo OK, nothing was installed. Install uv yourself with the command above,
echo close and reopen this window, then run this again.
exit /b 1

:install_uv
powershell -NoProfile -ExecutionPolicy ByPass -c "irm https://astral.sh/uv/install.ps1 | iex"
if errorlevel 1 (
    echo The uv installer failed - see the messages above. Check your internet
    echo connection and run this again.
    exit /b 1
)
if exist "%USERPROFILE%\.local\bin\uv.exe" set "HB_UV=%USERPROFILE%\.local\bin\uv.exe"
if not defined HB_UV (
    echo uv was installed but this window cannot see it yet.
    echo Close this window, open a new one, and run this again.
    exit /b 1
)

:have_uv
if not exist "%HB_HERE%.venv" echo Preparing the Hullbreach tools (the first time takes a minute)...
"%HB_UV%" run --quiet --project "%HB_HERE%." %HB_TOOL% %HB_ARGS%
exit /b %ERRORLEVEL%
