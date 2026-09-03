@echo off
setlocal EnableExtensions
cd /d "%~dp0.."

set "EXE=%cd%\TouhouFumoRacing.exe"
if not exist "%EXE%" (
    echo TouhouFumoRacing.exe not found at "%EXE%"
    exit /b 1
)

rem Two windowed 1280x720 instances. Positions are top-left of the window on the
rem primary display (pixels from the left-top origin).
rem --tfr-skip-splash ends SplashScript immediately (skips the custom overlay too).
rem Left hosts and stays on mode selection; right joins 127.0.0.1:7777.
set "LEFT_POS=1,640"
set "RIGHT_POS=1280,640"
set "RES=1280x720"
set "COMMON=-screen-fullscreen 0 -screen-width 1280 -screen-height 720 -popupwindow --tfr-res=%RES% --tfr-skip-splash"

start "TFR Left" "%EXE%" %COMMON% --tfr-pos=%LEFT_POS% --tfr-host -logFile "%cd%\TFR_instance_left.log"
timeout /t 1 /nobreak >nul
start "TFR Right" "%EXE%" %COMMON% --tfr-pos=%RIGHT_POS% --tfr-join=127.0.0.1 -logFile "%cd%\TFR_instance_right.log"
