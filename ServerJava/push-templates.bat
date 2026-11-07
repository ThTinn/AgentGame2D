@echo off
rem Day bang Core len MySQL. Tham so: file templates.sql, file cau hinh database. Unity goi tu menu Tools/Core/Push Templates To MySQL.
cd /d "%~dp0"
set SQL=%~1
if "%SQL%"=="" set SQL=..\Builds\ServerData\templates.sql
set CONFIG=%~2
if "%CONFIG%"=="" set CONFIG=db.properties
if not exist "%SQL%" (
    echo Khong thay %SQL%. Trong Unity chay Tools/Core/Push Templates To MySQL.
    exit /b 1
)
if not exist "%CONFIG%" (
    echo Khong thay %CONFIG%. Chep db.properties.example thanh db.properties roi dien mat khau.
    exit /b 1
)
if not exist bin mkdir bin
javac -encoding UTF-8 -d bin -sourcepath src src\game\db\TemplateImport.java
if errorlevel 1 exit /b 1
java -cp "bin;lib\*" game.db.TemplateImport "%SQL%" "%CONFIG%"
