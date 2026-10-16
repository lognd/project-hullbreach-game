@echo off
rem Project Hullbreach doctor: checks this computer and explains every fix.
rem Double-click it, or type: doctor.cmd [--demo online^|guest^|platform] [--deep] [--collect]
call "%~dp0tools\hbtools\run.cmd" hb-doctor %*
set "HB_EXIT=%ERRORLEVEL%"
if not defined HB_NO_PAUSE (
    echo.
    pause
)
exit /b %HB_EXIT%
