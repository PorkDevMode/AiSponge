@echo off

:loop
taskkill /f /im python.exe /t >nul 2>&1
start "" python bot.py
start "" python blacklist.py

timeout /t 2400 >nul
goto loop