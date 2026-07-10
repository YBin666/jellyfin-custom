#!/data/data/com.termux/files/usr/bin/bash
# Jellyfin 状态检查脚本

CONTAINER_NAME=jellyfin-media

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}      Jellyfin 状态检查${NC}"
echo -e "${BLUE}========================================${NC}"
echo ""

# 检查容器状态
echo -e "${YELLOW}容器状态:${NC}"
STATUS=$(udocker ps | grep $CONTAINER_NAME)
if [ -n "$STATUS" ]; then
    echo -e "${GREEN}✓ 运行中${NC}"
    echo "$STATUS"
    echo ""
    
    # 获取IP（尝试多种方式）
    LOCAL_IP=""
    if command -v ip >/dev/null 2>&1; then
        LOCAL_IP=$(ip addr show wlan0 2>/dev/null | grep -oP '(?<=inet\s)\d+(\.\d+){3}' | head -1)
        [ -z "$LOCAL_IP" ] && LOCAL_IP=$(ip addr show eth0 2>/dev/null | grep -oP '(?<=inet\s)\d+(\.\d+){3}' | head -1)
        [ -z "$LOCAL_IP" ] && LOCAL_IP=$(ip addr show | grep -oP '(?<=inet\s)\d+(\.\d+){3}' | grep -v '127.0.0.1' | head -1)
    fi
    if [ -z "$LOCAL_IP" ] && command -v hostname >/dev/null 2>&1; then
        LOCAL_IP=$(hostname -I 2>/dev/null | awk '{print $1}')
    fi
    
    echo -e "${YELLOW}访问地址:${NC}"
    echo -e "  本机: ${BLUE}http://127.0.0.1:8096${NC}"
    if [ -n "$LOCAL_IP" ]; then
        echo -e "  局域网: ${BLUE}http://$LOCAL_IP:8096${NC}"
    fi
else
    echo -e "${RED}✗ 未运行${NC}"
    echo -e "${YELLOW}启动命令: ./jellyfin_start.sh${NC}"
fi

echo ""
echo -e "${YELLOW}管理命令:${NC}"
echo -e "  启动: ${BLUE}./jellyfin_start.sh${NC}"
echo -e "  停止: ${BLUE}./jellyfin_stop.sh${NC}"
echo -e "  日志: ${BLUE}./jellyfin_logs.sh${NC}"
echo -e "  备份: ${BLUE}./jellyfin_backup.sh${NC}"
