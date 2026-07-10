#!/data/data/com.termux/files/usr/bin/bash
# Jellyfin 备份脚本

# === 配置参数 ===
DIR_CONFIG=~/jellyfin/config
DIR_BACKUP=~/storage/shared/MediaLib/Backup
MAX_BACKUPS=10

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

echo -e "${YELLOW}========================================${NC}"
echo -e "${YELLOW}      Jellyfin 备份脚本${NC}"
echo -e "${YELLOW}========================================${NC}"
echo ""

# 创建备份目录
mkdir -p $DIR_BACKUP

# 备份时间戳
TIME=$(date +%Y%m%d_%H%M%S)
BACKUP_FILE="$DIR_BACKUP/jellyfin_config_$TIME.tar.gz"

# 停止容器以确保数据一致性
echo -e "${YELLOW}停止容器...${NC}"
udocker stop jellyfin-media 2>/dev/null

# 执行备份
echo -e "${YELLOW}正在备份配置目录...${NC}"
if tar -zcf "$BACKUP_FILE" "$DIR_CONFIG"; then
    echo -e "${GREEN}✓ 备份成功: $BACKUP_FILE${NC}"
    
    # 显示备份大小
    SIZE=$(du -h "$BACKUP_FILE" | awk '{print $1}')
    echo -e "${YELLOW}备份大小: $SIZE${NC}"
    
    # 重启容器
    echo -e "${YELLOW}重启容器...${NC}"
    udocker start jellyfin-media 2>/dev/null
else
    echo -e "${RED}✗ 备份失败！${NC}"
    udocker start jellyfin-media 2>/dev/null
    exit 1
fi

# 清理旧备份（保留最近10份）
echo -e "${YELLOW}清理旧备份（保留最近$MAX_BACKUPS份）...${NC}"
cd "$DIR_BACKUP"
ls -t jellyfin_config_*.tar.gz | tail -n +$(($MAX_BACKUPS + 1)) | while read file; do
    rm -f "$file"
    echo -e "${RED}删除: $file${NC}"
done

# 统计当前备份数量
COUNT=$(ls -1 jellyfin_config_*.tar.gz 2>/dev/null | wc -l)
echo -e "${GREEN}当前备份数: $COUNT 份${NC}"

echo ""
echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}      备份完成！${NC}"
echo -e "${GREEN}========================================${NC}"
