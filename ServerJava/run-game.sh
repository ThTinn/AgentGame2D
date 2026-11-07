#!/bin/sh
# Bien dich va chay server game. Tham so: thu muc du lieu, cong, file cau hinh database, so bot.
cd "$(dirname "$0")" || exit 1
DATA="${1:-../Builds/ServerData}"
PORT="${2:-18889}"
CONFIG="${3:-db.properties}"
BOTS="${4:-50}"
if [ ! -f "$DATA/manifest.csv" ]; then
    echo "Khong thay $DATA/manifest.csv. Trong Unity chay Tools/Core/Export Server Package truoc."
    exit 1
fi
if [ ! -f "$CONFIG" ]; then
    echo "Khong thay $CONFIG. Chep db.properties.example thanh db.properties roi dien mat khau."
    exit 1
fi
mkdir -p bin
javac -encoding UTF-8 -d bin -sourcepath src src/game/GameServer.java || exit 1
exec java -Xmx256m -cp "bin:lib/*" game.GameServer "$DATA" "$PORT" "$CONFIG" "$BOTS"
