#!/data/data/com.termux/files/usr/bin/bash
# Jellyfin 停止脚本

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

echo -e "${YELLOW}停止 Jellyfin...${NC}"

PIDS=$(ps aux | grep -E "(udocker run.*jellyfin|proot.*jellyfin|/jellyfin/jellyfin)" | grep -v grep | awk '{print $2}')

if [ -n "$PIDS" ]; then
    echo -e "${YELLOW}终止进程: $PIDS${NC}"
    kill -TERM $PIDS 2>/dev/null
    sleep 3
    kill -KILL $PIDS 2>/dev/null
    echo -e "${GREEN}进程已终止${NC}"
else
    echo -e "${YELLOW}未找到运行中的进程${NC}"
fi

source "$HOME/Termux-Udocker/source.env"

echo -e "${YELLOW}清理容器...${NC}"
udocker rm jellyfin-server 2>/dev/null
udocker rm jellyfin-media 2>/dev/null
echo -e "${GREEN}Jellyfin 已停止${NC}"
