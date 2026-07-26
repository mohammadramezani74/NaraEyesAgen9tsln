@echo off
rem =========================
rem AgentWatchdog.bat
rem کنار NaraAgentClassification.exe قرار بگیرد
rem =========================

rem برای کار با کانفیگ و حلقه‌ها
setlocal enabledelayedexpansion

rem همیشه برو تو فولدر همین bat
cd /d "%~dp0"

rem نام فایل لاگ
set "LOGFILE=AgentWatchdog.log"

rem اسم ایجنت (طبق چیزی که گفتی)
set "AGENT_EXE=NaraAgentClassification.exe"

rem نام فایل کانفیگ متنی (اینو به اسم واقعی کانفیگت تغییر بده)
set "CONFIG_FILE=Config.txt"

rem =========================
rem بررسی مقدار TerminalCode در فایل کانفیگ
rem =========================
if exist "%CONFIG_FILE%" (
    rem اگر دقیقا TerminalCode=0 باشد، باید از کاربر بپرسیم
    findstr /R /C:"^TerminalCode=0$" "%CONFIG_FILE%" >nul
    if not errorlevel 1 (
        echo.
        echo *** Setup TerminalCode ***
:AskTerminalCode
        set "TERMINAL_CODE="
        set /p TERMINAL_CODE=please Enter TerminalCode: 

        rem اگر خالی بود، دوباره بپرس
        if "!TERMINAL_CODE!"=="" (
            echo كد ترمينال نمي‌تواند خالي باشد.
            goto :AskTerminalCode
        )

        rem بازنویسی فایل کانفیگ و جایگزینی TerminalCode=0 با مقدار جدید
        > "%CONFIG_FILE%.tmp" (
            for /f "usebackq delims=" %%L in ("%CONFIG_FILE%") do (
                set "line=%%L"
                if /I "!line!"=="TerminalCode=0" (
                    echo TerminalCode=!TERMINAL_CODE!
                ) else (
                    echo !line!
                )
            )
        )
        move /Y "%CONFIG_FILE%.tmp" "%CONFIG_FILE%" >nul
        echo كد ترمينال با موفقيت ذخيره شد.
        echo.
    )
) else (
    rem اگر فایل کانفیگ وجود نداشت، هیچ کاری نکن
    rem echo Config file "%CONFIG_FILE%" not found.
)

rem تنظیمات تأخیر
set "INITIAL_DELAY_SECONDS=5"
set "MAX_DELAY_SECONDS=300"
set "BACKOFF_MULTIPLIER=2"

rem مقدار اولیه‌ی delay
set /a "delay=%INITIAL_DELAY_SECONDS%"

:main_loop
rem اگر خواستی نگهبان متوقف شه، فقط یه فایل stop.watchdog بساز
if exist "stop.watchdog" (
    echo [%date% %time%] Stop file detected. Exiting watchdog. >> "%LOGFILE%"
    goto :eof
)

echo [%date% %time%] ===== Starting %AGENT_EXE% ===== >> "%LOGFILE%"

"%AGENT_EXE%"
set "EXIT_CODE=%ERRORLEVEL%"

echo [%date% %time%] %AGENT_EXE% exited with code %EXIT_CODE% >> "%LOGFILE%"

echo [%date% %time%] Restarting in %delay% seconds... >> "%LOGFILE%"

rem تاخیر (سازگار با XP)
ping 127.0.0.1 -n %delay% >nul

rem افزایش تدریجی delay تا حداکثر
set /a "nextDelay = delay * BACKOFF_MULTIPLIER"
if %nextDelay% gtr %MAX_DELAY_SECONDS% (
    set /a "delay=%MAX_DELAY_SECONDS%"
) else (
    set /a "delay=%nextDelay%"
)

goto :main_loop
