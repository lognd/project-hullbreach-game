@echo off
rem Project Hullbreach launcher: build, host, join, serve. Type: play.cmd --help
call "%~dp0tools\hbtools\run.cmd" hb-play %*
exit /b %ERRORLEVEL%
