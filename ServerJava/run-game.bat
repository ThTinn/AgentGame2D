@echo off
rem Bien dich va chay server game. Tham so: thu muc du lieu, cong, file cau hinh database, so bot.
cd /d "%~dp0"
set DATA=%~1
if "%DATA%"=="" set DATA=data
set PORT=%~2
if "%PORT%"=="" set PORT=18889
set CONFIG=%~3
if "%CONFIG%"=="" set CONFIG=db.properties
set BOTS=%~4
if "%BOTS%"=="" set BOTS=6
if not exist "%DATA%\manifest.csv" (
    echo Khong thay %DATA%\manifest.csv.
    pause
    exit /b 1
)
rem khong co %CONFIG% thi server chay tam khong MySQL (tai khoan chi o RAM)
if not exist bin mkdir bin
javac -encoding UTF-8 -d bin -sourcepath src src\game\GameServer.java
if errorlevel 1 (
    pause
    exit /b 1
)
java -Xmx256m -cp "bin;lib\*" game.GameServer "%DATA%" %PORT% "%CONFIG%" %BOTS%
pause
