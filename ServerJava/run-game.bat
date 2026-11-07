@echo off
rem Bien dich va chay server game. Tham so: thu muc du lieu, cong, file cau hinh database, so bot.
cd /d "%~dp0"
set DATA=%~1
if "%DATA%"=="" set DATA=..\Builds\ServerData
set PORT=%~2
if "%PORT%"=="" set PORT=18889
set CONFIG=%~3
if "%CONFIG%"=="" set CONFIG=db.properties
set BOTS=%~4
if "%BOTS%"=="" set BOTS=50
if not exist "%DATA%\manifest.csv" (
    echo Khong thay %DATA%\manifest.csv. Trong Unity chay Tools/Core/Export Server Package truoc.
    pause
    exit /b 1
)
if not exist "%CONFIG%" (
    echo Khong thay %CONFIG%. Chep db.properties.example thanh db.properties roi dien mat khau.
    pause
    exit /b 1
)
if not exist bin mkdir bin
javac -encoding UTF-8 -d bin -sourcepath src src\game\GameServer.java
if errorlevel 1 (
    pause
    exit /b 1
)
java -Xmx256m -cp "bin;lib\*" game.GameServer "%DATA%" %PORT% "%CONFIG%" %BOTS%
pause
