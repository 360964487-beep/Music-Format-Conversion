@echo off
setlocal
cd /d "%~dp0"
where py.exe >nul 2>nul
if not errorlevel 1 (
    py -3 scripts\video_service.py
    goto done
)
if exist "%USERPROFILE%\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe" (
    "%USERPROFILE%\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe" scripts\video_service.py
    goto done
)
where python.exe >nul 2>nul
if not errorlevel 1 (
    python.exe scripts\video_service.py
    goto done
)
echo Install Python 3 from https://www.python.org/downloads/windows/ and try again.
:done
pause
