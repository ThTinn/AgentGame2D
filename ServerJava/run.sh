#!/bin/sh
# Bien dich va chay server mau. Tham so: thu muc du lieu, cong.
cd "$(dirname "$0")" || exit 1
DATA="${1:-data}"
PORT="${2:-18888}"
if [ ! -f "$DATA/manifest.csv" ]; then
    echo "Khong thay $DATA/manifest.csv."
    exit 1
fi
mkdir -p bin
javac -encoding UTF-8 -d bin -sourcepath src src/game/data/DataServer.java || exit 1
exec java -Xmx256m -cp bin game.data.DataServer "$DATA" "$PORT"
