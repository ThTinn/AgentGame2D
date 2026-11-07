#!/bin/sh
# Bien dich va chay server game. Tham so: thu muc du lieu, cong, file cau hinh database, so bot.
cd "$(dirname "$0")" || exit 1
DATA="${1:-data}"
PORT="${2:-18889}"
CONFIG="${3:-db.properties}"
BOTS="${4:-6}"
if [ ! -f "$DATA/manifest.csv" ]; then
    echo "Khong thay $DATA/manifest.csv."
    exit 1
fi
# khong co $CONFIG thi server chay tam khong MySQL (tai khoan chi o RAM)
mkdir -p bin
javac -encoding UTF-8 -d bin -sourcepath src src/game/GameServer.java || exit 1
exec java -Xmx256m -cp "bin:lib/*" game.GameServer "$DATA" "$PORT" "$CONFIG" "$BOTS"
