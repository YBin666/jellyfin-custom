#!/data/data/com.termux/files/usr/bin/bash
# Jellyfin 启动脚本 - 基于 Termux-Udocker 官方配置

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}      Jellyfin 启动脚本${NC}"
echo -e "${BLUE}========================================${NC}"
echo ""

echo -e "${YELLOW}后台启动容器...${NC}"
nohup bash "$HOME/Termux-Udocker/jellyfin.sh" > "$HOME/jellyfin/jellyfin.log" 2>&1 &

echo -e "${YELLOW}等待启动...${NC}"
for i in {1..15}; do
    sleep 3
    PIDS=$(ps aux | grep "/jellyfin/jellyfin" | grep -v grep | awk '{print $2}')
    if [ -n "$PIDS" ]; then
        echo -e "${GREEN}========================================${NC}"
        echo -e "${GREEN}      Jellyfin 启动成功！${NC}"
        echo -e "${GREEN}========================================${NC}"
        echo ""
        echo -e "${YELLOW}访问地址: http://<设备IP>:8096${NC}"
        echo -e "${YELLOW}查看日志: ./jellyfin_logs.sh${NC}"
        echo -e "${YELLOW}关闭终端后服务继续运行${NC}"
        exit 0
    fi
done

echo -e "${RED}容器启动失败！${NC}"
echo -e "${YELLOW}查看日志: tail -50 ~/jellyfin/jellyfin.log${NC}"
exit 1
