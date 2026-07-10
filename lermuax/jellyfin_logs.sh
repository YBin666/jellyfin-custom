#!/data/data/com.termux/files/usr/bin/bash
# Jellyfin 日志查看脚本

CONTAINER_NAME=jellyfin-media

echo "=== Jellyfin 日志 ==="

if udocker logs -f $CONTAINER_NAME; then
    exit 0
else
    echo "容器未运行或日志获取失败"
    exit 1
fi
