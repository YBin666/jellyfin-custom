#!/data/data/com.termux/files/usr/bin/bash
source "$HOME/Termux-Udocker/source.env"

echo "Stopping all containers..."
for id in $(udocker ps | awk 'NR>1 {print $1}'); do
    udocker stop "$id" 2>/dev/null
done

echo "Removing all containers..."
for id in $(udocker ps | awk 'NR>1 {print $1}'); do
    udocker rm "$id" 2>/dev/null
done

echo "Cleanup done!"
udocker ps
