#!/bin/sh
# Day bang Core len MySQL. Tham so: file templates.sql, file cau hinh database.
cd "$(dirname "$0")" || exit 1
SQL="${1:-../Builds/ServerData/templates.sql}"
CONFIG="${2:-db.properties}"
if [ ! -f "$SQL" ]; then
    echo "Khong thay $SQL. Trong Unity chay Tools/Core/Push Templates To MySQL."
    exit 1
fi
if [ ! -f "$CONFIG" ]; then
    echo "Khong thay $CONFIG. Chep db.properties.example thanh db.properties roi dien mat khau."
    exit 1
fi
mkdir -p bin
javac -encoding UTF-8 -d bin -sourcepath src src/game/db/TemplateImport.java || exit 1
exec java -cp "bin:lib/*" game.db.TemplateImport "$SQL" "$CONFIG"
