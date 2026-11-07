@echo off
rem Bien dich va chay server mau. Tham so: thu muc du lieu, cong.
cd /d "%~dp0"
set DATA=%~1
if "%DATA%"=="" set DATA=..\Builds\ServerData
set PORT=%~2
if "%PORT%"=="" set PORT=18888
if not exist "%DATA%\manifest.csv" (
    echo Khong thay %DATA%\manifest.csv. Trong Unity chay Tools/Core/Export Server Package truoc.
    pause
    exit /b 1
)
if not exist bin mkdir bin
javac -encoding UTF-8 -d bin -sourcepath src src\game\data\DataServer.java
if errorlevel 1 (
    pause
    exit /b 1
)
java -Xmx256m -cp bin game.data.DataServer "%DATA%" %PORT%
pause
