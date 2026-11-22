#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
RAW_DIR="$SCRIPT_DIR/raw"

if [ ! -d "$RAW_DIR" ] || [ "$(ls -1 "$RAW_DIR"/shot*.png 2>/dev/null | wc -l | tr -d ' ')" -lt 6 ]; then
  echo "Ham 1920x1080 ekran görüntüleri eksik ($RAW_DIR/shot1..shot6.png)."
  echo "Unity Editor menüsünden çalıştır:"
  echo "  GRIDROAM -> Store -> 1. Capture Raw Store Screenshots (6 Shots, 1920x1080)"
  exit 1
fi

python3 "$SCRIPT_DIR/generate_store_posters.py" --lang tr
python3 "$SCRIPT_DIR/generate_store_posters.py" --lang en
echo "=== Tüm TR & EN App Store / Google Play mağaza görselleri hazır! ==="
