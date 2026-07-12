window.ShortsModule = (function() {
        var getToken = window.__svGetToken || function(){return '';};
        var getUserId = window.__svGetUserId || function(){return '';};

        var P = 'sv'; // 样式前缀
        var CONTAINER_ID = 'shortid-spa-view';

        var styles = [
            '#' + CONTAINER_ID + ' * { margin: 0; padding: 0; box-sizing: border-box; }',
            '#' + CONTAINER_ID + ' .' + P + '-feed { width: 100%; height: calc(100% - 80px); overflow-y: scroll; scroll-snap-type: y mandatory; scrollbar-width: none; }',
            '#' + CONTAINER_ID + ' .' + P + '-feed::-webkit-scrollbar { display: none; }',
            '#' + CONTAINER_ID + ' .' + P + '-card { width: 100%; height: 100%; scroll-snap-align: start; scroll-snap-stop: always; position: relative; background: #000; display: flex; align-items: center; justify-content: center; overflow: hidden; }',
            '#' + CONTAINER_ID + ' video { max-width: 100%; max-height: 100%; object-fit: contain; background: #000; pointer-events: none; }',
            '#' + CONTAINER_ID + ' .' + P + '-overlay { position: absolute; top: 0; left: 0; width: 100%; height: 100%; z-index: 3; pointer-events: auto; -webkit-tap-highlight-color: transparent; }',
            '#' + CONTAINER_ID + ' .' + P + '-poster { position: absolute; top: 0; left: 0; width: 100%; height: 100%; background-size: cover; background-position: center; background-color: #000; z-index: 1; transition: opacity 0.3s ease; pointer-events: none; }',
            '#' + CONTAINER_ID + ' .' + P + '-poster.hidden { opacity: 0; pointer-events: none; }',
            '#' + CONTAINER_ID + ' .' + P + '-loader { position: absolute; top: 50%; left: 50%; transform: translate(-50%, -50%); z-index: 2; width: 50px; height: 50px; border: 4px solid rgba(255,255,255,0.2); border-top-color: #fff; border-radius: 50%; animation: ' + P + '-spin 0.8s linear infinite; display: none; pointer-events: none; }',
            '#' + CONTAINER_ID + ' .' + P + '-loader.show { display: block; }',
            '@keyframes ' + P + '-spin { to { transform: translate(-50%, -50%) rotate(360deg); } }',
            '#' + CONTAINER_ID + ' .' + P + '-top-gradient { position: absolute; top: 0; left: 0; right: 0; height: 120px; background: linear-gradient(to bottom, rgba(0,0,0,0.6), transparent); z-index: 5; pointer-events: none; }',
            '#' + CONTAINER_ID + ' .' + P + '-bottom-gradient { position: absolute; bottom: 0; left: 0; right: 0; height: 200px; background: linear-gradient(to top, rgba(0,0,0,0.7), transparent); z-index: 5; pointer-events: none; }',
            '#' + CONTAINER_ID + ' .' + P + '-actions { position: absolute; right: 12px; bottom: 30px; display: flex; flex-direction: column; gap: 12px; color: #fff; font-size: 11px; text-align: center; z-index: 10; }',
            '#' + CONTAINER_ID + ' .' + P + '-actions .action-item { display: flex; flex-direction: column; align-items: center; gap: 4px; }',
            '#' + CONTAINER_ID + ' .' + P + '-actions .icon { width: 40px; height: 40px; border-radius: 50%; background: rgba(255,255,255,0.15); backdrop-filter: blur(10px); -webkit-backdrop-filter: blur(10px); display: flex; align-items: center; justify-content: center; font-size: 20px; cursor: pointer; transition: transform 0.15s ease, background 0.15s ease; border: 1px solid rgba(255,255,255,0.1); }',
            '#' + CONTAINER_ID + ' .' + P + '-actions .icon:active { transform: scale(0.9); background: rgba(255,255,255,0.25); }',
            '#' + CONTAINER_ID + ' .' + P + '-actions .icon.like.liked { color: #ff4757; background: rgba(255,71,87,0.2); }',
            
            '#' + CONTAINER_ID + ' .' + P + '-caption { position: absolute; left: 16px; right: 72px; bottom: 30px; color: #fff; z-index: 10; }',
            '#' + CONTAINER_ID + ' .' + P + '-caption .title { font-weight: 600; font-size: 14px; margin-bottom: 4px; text-shadow: 0 2px 8px rgba(0,0,0,0.8); line-height: 1.4; }',
            '#' + CONTAINER_ID + ' .' + P + '-caption .meta { font-size: 12px; opacity: 0.85; text-shadow: 0 1px 4px rgba(0,0,0,0.8); }',
            '#' + CONTAINER_ID + ' .' + P + '-back { position: absolute; top: 16px; left: 16px; z-index: 20; width: 40px; height: 40px; border-radius: 50%; background: rgba(0,0,0,0.3); backdrop-filter: blur(10px); -webkit-backdrop-filter: blur(10px); display: flex; align-items: center; justify-content: center; color: #fff; font-size: 28px; text-decoration: none; line-height: 1; border: 1px solid rgba(255,255,255,0.1); cursor: pointer; }',
            '#' + CONTAINER_ID + ' .' + P + '-back svg { display: block; }',
            '#' + CONTAINER_ID + ' .' + P + '-empty { color: #888; text-align: center; padding: 40px; }',
            '#' + CONTAINER_ID + ' .' + P + '-center-anim { position: absolute; top: 50%; left: 50%; transform: translate(-50%, -50%) scale(0.5); width: 80px; height: 80px; border-radius: 50%; background: rgba(0,0,0,0.5); backdrop-filter: blur(10px); -webkit-backdrop-filter: blur(10px); display: flex; align-items: center; justify-content: center; color: #fff; font-size: 36px; opacity: 0; pointer-events: none; z-index: 15; transition: opacity 0.2s ease, transform 0.2s ease; }',
            '#' + CONTAINER_ID + ' .' + P + '-center-anim.show { opacity: 1; transform: translate(-50%, -50%) scale(1); }',
            '#' + CONTAINER_ID + ' .' + P + '-heart { position: absolute; pointer-events: none; z-index: 20; font-size: 80px; animation: ' + P + '-heart-pop 0.8s ease forwards; }',
            '@keyframes ' + P + '-heart-pop { 0% { opacity: 0; transform: translate(-50%, -50%) scale(0.3); } 20% { opacity: 1; transform: translate(-50%, -50%) scale(1.2); } 40% { transform: translate(-50%, -50%) scale(0.95); } 60% { transform: translate(-50%, -50%) scale(1); } 100% { opacity: 0; transform: translate(-50%, -50%) scale(0.8) translateY(-30px); } }',
            // 抖音风格进度条：默认隐藏，hover/触摸/暂停时显示，显示时变粗
            '#' + CONTAINER_ID + ' .' + P + '-controls { position: absolute; left: 0; right: 0; bottom: 0; z-index: 10; padding: 0; }',
            '#' + CONTAINER_ID + ' .' + P + '-progress-container { position: relative; width: 100%; height: 40px; display: flex; align-items: center; cursor: pointer; padding: 0 12px; }',
            '#' + CONTAINER_ID + ' .' + P + '-progress-bg { position: absolute; left: 12px; right: 12px; bottom: 12px; height: 2px; background: rgba(255,255,255,0.3); border-radius: 2px; transition: height 0.15s ease, bottom 0.15s ease, opacity 0.15s ease; opacity: 0; }',
            '#' + CONTAINER_ID + ' .' + P + '-progress-buffer { position: absolute; left: 12px; bottom: 12px; height: 2px; background: rgba(255,255,255,0.5); border-radius: 2px; width: 0%; transition: height 0.15s ease, bottom 0.15s ease, opacity 0.15s ease; opacity: 0; }',
            '#' + CONTAINER_ID + ' .' + P + '-progress-played { position: absolute; left: 12px; bottom: 12px; height: 2px; background: #fff; border-radius: 2px; width: 0%; transition: height 0.15s ease, bottom 0.15s ease, opacity 0.15s ease; opacity: 0; }',
            '#' + CONTAINER_ID + ' .' + P + '-progress-handle { position: absolute; bottom: 12px; width: 0; height: 0; background: #fff; border-radius: 50%; transform: translateX(-50%) translateY(50%); transition: width 0.15s ease, height 0.15s ease, opacity 0.15s ease; opacity: 0; }',
            // hover/拖拽/触摸时显示并变粗
            '#' + CONTAINER_ID + ' .' + P + '-progress-container:hover .' + P + '-progress-bg, #' + CONTAINER_ID + ' .' + P + '-progress-container:hover .' + P + '-progress-buffer, #' + CONTAINER_ID + ' .' + P + '-progress-container:hover .' + P + '-progress-played, #' + CONTAINER_ID + ' .' + P + '-progress-container:hover .' + P + '-progress-handle, #' + CONTAINER_ID + ' .' + P + '-progress-container.dragging .' + P + '-progress-bg, #' + CONTAINER_ID + ' .' + P + '-progress-container.dragging .' + P + '-progress-buffer, #' + CONTAINER_ID + ' .' + P + '-progress-container.dragging .' + P + '-progress-played, #' + CONTAINER_ID + ' .' + P + '-progress-container.dragging .' + P + '-progress-handle, #' + CONTAINER_ID + ' .' + P + '-progress-container.touch-show .' + P + '-progress-bg, #' + CONTAINER_ID + ' .' + P + '-progress-container.touch-show .' + P + '-progress-buffer, #' + CONTAINER_ID + ' .' + P + '-progress-container.touch-show .' + P + '-progress-played, #' + CONTAINER_ID + ' .' + P + '-progress-container.touch-show .' + P + '-progress-handle { opacity: 1; }',
            '#' + CONTAINER_ID + ' .' + P + '-progress-container:hover .' + P + '-progress-bg, #' + CONTAINER_ID + ' .' + P + '-progress-container:hover .' + P + '-progress-buffer, #' + CONTAINER_ID + ' .' + P + '-progress-container:hover .' + P + '-progress-played, #' + CONTAINER_ID + ' .' + P + '-progress-container.dragging .' + P + '-progress-bg, #' + CONTAINER_ID + ' .' + P + '-progress-container.dragging .' + P + '-progress-buffer, #' + CONTAINER_ID + ' .' + P + '-progress-container.dragging .' + P + '-progress-played, #' + CONTAINER_ID + ' .' + P + '-progress-container.touch-show .' + P + '-progress-bg, #' + CONTAINER_ID + ' .' + P + '-progress-container.touch-show .' + P + '-progress-buffer, #' + CONTAINER_ID + ' .' + P + '-progress-container.touch-show .' + P + '-progress-played { height: 4px; bottom: 10px; }',
            '#' + CONTAINER_ID + ' .' + P + '-progress-container:hover .' + P + '-progress-handle, #' + CONTAINER_ID + ' .' + P + '-progress-container.dragging .' + P + '-progress-handle, #' + CONTAINER_ID + ' .' + P + '-progress-container.touch-show .' + P + '-progress-handle { width: 12px; height: 12px; }',
            // 暂停时始终显示粗进度条
            '#' + CONTAINER_ID + ' .' + P + '-card.paused .' + P + '-progress-bg, #' + CONTAINER_ID + ' .' + P + '-card.paused .' + P + '-progress-buffer, #' + CONTAINER_ID + ' .' + P + '-card.paused .' + P + '-progress-played, #' + CONTAINER_ID + ' .' + P + '-card.paused .' + P + '-progress-handle { opacity: 1; height: 4px; bottom: 10px; }',
            '#' + CONTAINER_ID + ' .' + P + '-card.paused .' + P + '-progress-handle { width: 12px; height: 12px; }',
            // 拖拽时显示的时间
            '#' + CONTAINER_ID + ' .' + P + '-progress-time { position: absolute; top: -10px; left: 50%; transform: translateX(-50%); color: #fff; font-size: 28px; font-weight: 500; font-variant-numeric: tabular-nims; text-shadow: 0 2px 8px rgba(0,0,0,0.6); opacity: 0; transition: opacity 0.15s ease; pointer-events: none; white-space: nowrap; letter-spacing: 1px; }',
            '#' + CONTAINER_ID + ' .' + P + '-progress-container.dragging .' + P + '-progress-time { opacity: 1; top: -36px; }',
            '#' + CONTAINER_ID + ' .' + P + '-controls-bar { display: none; }',
            '#' + CONTAINER_ID + ' .' + P + '-rate-menu { display: none; }',
            '#' + CONTAINER_ID + ' .' + P + '-lucide { width: 20px; height: 20px; stroke-width: 2; stroke-linecap: round; stroke-linejoin: round; fill: none; vertical-align: middle; }',
            // 静音按钮样式：静音状态无背景圆，打开声音有背景圆（抖音风格）
            '#' + CONTAINER_ID + ' .' + P + '-actions .icon.mute-icon { width: 40px; height: 40px; border-radius: 50%; background: rgba(255,255,255,0.15); backdrop-filter: blur(10px); -webkit-backdrop-filter: blur(10px); display: flex; align-items: center; justify-content: center; font-size: 20px; cursor: pointer; transition: transform 0.15s ease, background 0.15s ease; border: 1px solid rgba(255,255,255,0.1); }',
            '#' + CONTAINER_ID + ' .' + P + '-actions .icon.mute-icon.active { background: rgba(255,255,255,0.8); color: #000; }',
            // 覆盖底部元素位置，actions 和 caption 与进度条紧凑排列
            '#' + CONTAINER_ID + ' .' + P + '-controls { bottom: 5px; }',
            '#' + CONTAINER_ID + ' .' + P + '-caption { bottom: 30px; }',
            '#' + CONTAINER_ID + ' .' + P + '-actions { bottom: 30px; }',
            // 底部 hub 栏（抖音风格，80px 高度）
            '#' + CONTAINER_ID + ' .' + P + '-hub { position: absolute; left: 0; right: 0; bottom: 0; height: 80px; display: flex; justify-content: space-around; align-items: center; background: rgba(0,0,0,0.95); backdrop-filter: blur(20px); -webkit-backdrop-filter: blur(20px); z-index: 25; border-top: 1px solid rgba(255,255,255,0.08); padding-bottom: env(safe-area-inset-bottom); }',
            '#' + CONTAINER_ID + ' .' + P + '-hub .hub-btn { flex: 1; display: flex; align-items: center; justify-content: center; color: #fff; font-size: 18px; font-weight: 700; cursor: pointer; padding: 10px 0; opacity: 0.6; transition: opacity 0.15s ease; background: none; border: none; font-family: inherit; }',
            '#' + CONTAINER_ID + ' .' + P + '-hub .hub-btn.active { opacity: 1; }',
            // 收藏列表面板（从右侧弹出）
            '#' + CONTAINER_ID + ' .' + P + '-favorites-panel { position: absolute; top: 0; left: 0; right: 0; bottom: 80px; background: #000; z-index: 30; transform: translateX(100%); transition: transform 0.3s ease; overflow-y: auto; }',
            '#' + CONTAINER_ID + ' .' + P + '-favorites-panel.show { transform: translateX(0); }',
            '#' + CONTAINER_ID + ' .' + P + '-favorites-header { position: sticky; top: 0; height: 56px; display: flex; align-items: center; padding: 0 16px; background: rgba(0,0,0,0.9); backdrop-filter: blur(10px); -webkit-backdrop-filter: blur(10px); border-bottom: 1px solid rgba(255,255,255,0.08); z-index: 10; }',
            '#' + CONTAINER_ID + ' .' + P + '-favorites-back { width: 40px; height: 40px; border-radius: 50%; background: rgba(255,255,255,0.1); display: flex; align-items: center; justify-content: center; cursor: pointer; border: none; color: #fff; }',
            '#' + CONTAINER_ID + ' .' + P + '-favorites-title { flex: 1; text-align: center; font-size: 16px; font-weight: 600; color: #fff; }',
            '#' + CONTAINER_ID + ' .' + P + '-favorites-grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 8px; padding: 16px; }',
            '#' + CONTAINER_ID + ' .' + P + '-favorites-item { aspect-ratio: 9/16; background: #1a1a1a; border-radius: 8px; overflow: hidden; cursor: pointer; position: relative; }',
            '#' + CONTAINER_ID + ' .' + P + '-favorites-item img { width: 100%; height: 100%; object-fit: cover; }',
            '#' + CONTAINER_ID + ' .' + P + '-favorites-item .name { position: absolute; bottom: 0; left: 0; right: 0; padding: 8px; background: linear-gradient(to top, rgba(0,0,0,0.8), transparent); font-size: 12px; color: #fff; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }',
            '#' + CONTAINER_ID + ' .' + P + '-favorites-empty { color: #666; text-align: center; padding: 60px 20px; font-size: 14px; }'
        ].join('\n');

        function buildContainer() {
            var container = document.createElement('div');
            container.id = CONTAINER_ID;
            container.style.cssText = [
                'position: fixed',
                'top: 0', 'left: 0',
                'width: 100%', 'height: 100%',
                'z-index: 9998',
                'background: #000',
                'overflow: hidden',
                'font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif',
                '-webkit-user-select: none', 'user-select: none',
                '-webkit-tap-highlight-color: transparent'
            ].join(';');

            var style = document.createElement('style');
            style.textContent = styles;
            container.appendChild(style);

            var feed = document.createElement('div');
            feed.className = P + '-feed';

            container.appendChild(feed);

            // 底部 hub 栏：首页、刷新、收藏
            var hub = document.createElement('div');
            hub.className = P + '-hub';
            var hubButtons = [
                {
                    label: '首页',
                    action: null
                },
                {
                    label: '刷新',
                    action: null
                },
                {
                    label: '收藏',
                    action: null
                }
            ];
            hubButtons.forEach(function(btn) {
                var el = document.createElement('button');
                el.className = 'hub-btn';
                el.textContent = btn.label;
                hub.appendChild(el);
            });
            container.appendChild(hub);

            // 收藏列表面板
            var favoritesPanel = document.createElement('div');
            favoritesPanel.className = P + '-favorites-panel';
            favoritesPanel.innerHTML =
                '<div class="' + P + '-favorites-header">' +
                    '<button class="' + P + '-favorites-back"><svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="9 18 15 12 9 6"></polyline></svg></button>' +
                    '<div class="' + P + '-favorites-title">收藏</div>' +
                    '<div style="width: 40px;"></div>' +
                '</div>' +
                '<div class="' + P + '-favorites-grid"></div>';
            container.appendChild(favoritesPanel);

            // 暴露引用
            container._hubButtons = hub.querySelectorAll('.hub-btn');
            container._hub = hub;
            container._favoritesPanel = favoritesPanel;

            return container;
        }

        // 内嵌播放器初始化（ShortsModule 独占）
        function initPlayer(container, feed) {
            var BASE = window.location.origin;
            var API_KEY = getToken();
            var globalMuted = true;
            var globalVolume = 1;
            var firstBatchLoaded = false;
            var isLoading = false;

            var transcodeQueue = [];
            var transcodeActive = 0;
            var MAX_CONCURRENT = 2;

            function scheduleTranscode(fn) {
                if (transcodeActive < MAX_CONCURRENT) { transcodeActive++; fn(); }
                else { transcodeQueue.push(fn); }
            }
            function transcodeDone() {
                if (transcodeActive > 0) transcodeActive--;
                if (transcodeQueue.length > 0 && transcodeActive < MAX_CONCURRENT) {
                    var next = transcodeQueue.shift();
                    transcodeActive++;
                    next();
                }
            }

            function apiUrl(path) {
                var url = BASE + path;
                if (API_KEY) {
                    var sep = path.indexOf('?') >= 0 ? '&' : '?';
                    url += sep + 'api_key=' + encodeURIComponent(API_KEY);
                }
                return url;
            }

            function formatTime(s) {
                if (!isFinite(s) || s < 0) return '00:00';
                var m = Math.floor(s / 60), sec = Math.floor(s % 60);
                return (m < 10 ? '0' : '') + m + ':' + (sec < 10 ? '0' : '') + sec;
            }

            function formatLikeCount(n) {
                if (n >= 10000) return (n / 10000).toFixed(1) + 'w';
                if (n >= 1000) return (n / 1000).toFixed(1) + 'k';
                return n.toString();
            }

            function setIcon(el, name) {
                if (!el) return;
                if (window.lucide && lucide.icons && lucide.icons[name]) {
                    el.innerHTML = lucide.icons[name].toSvg({
                        'stroke-width': 2, 'stroke-linecap': 'round', 'stroke-linejoin': 'round', fill: 'none'
                    });
                }
            }

            function ensureApiKey(url, key) {
                if (key && key.length >= 10) {
                    if (url.indexOf('api_key=') >= 0) {
                        return url.replace(/([?&])api_key=[^&]*/, '$1api_key=' + encodeURIComponent(key));
                    }
                    var sep = url.indexOf('?') >= 0 ? '&' : '?';
                    return url + sep + 'api_key=' + encodeURIComponent(key);
                }
                return url;
            }

            function syncMuteAll() {
                feed.querySelectorAll('.' + P + '-card').forEach(function(card) {
                    var v = card._video;
                    var muteBtn = card.querySelector('.mute-icon');
                    if (v) { v.muted = globalMuted; v.volume = globalVolume; }
                    if (muteBtn && muteBtn._setIcon) muteBtn._setIcon(globalMuted ? 'volume-x' : 'volume-2');
                    if (muteBtn) muteBtn.classList.toggle('active', !globalMuted);
                });
            }

            function loadBatch() {
                if (isLoading) return;
                isLoading = true;
                fetch(apiUrl('/ShortVideo/NextBatch'), { credentials: 'include' })
                    .then(function(r) {
                        if (!r.ok) throw new Error('HTTP ' + r.status);
                        return r.json();
                    })
                    .then(function(items) {
                        isLoading = false;
                        if (!items || items.length === 0) {
                            if (!firstBatchLoaded) {
                                feed.innerHTML = '<div class="' + P + '-empty">没有找到短视频。</div>';
                            }
                            return;
                        }
                        var isFirst = !firstBatchLoaded;
                        items.forEach(appendCard);
                        firstBatchLoaded = true;
                        if (isFirst) {
                            setTimeout(function() { playVisible(); }, 200);
                        }
                    })
                    .catch(function(e) {
                        isLoading = false;
                        console.error('[ShortsModule] loadBatch error:', e);
                        if (!firstBatchLoaded) {
                            feed.innerHTML = '<div class="' + P + '-empty">加载失败：' + e.message + '</div>';
                        }
                    });
            }

            function appendCard(item) {
                var card = document.createElement('div');
                card.className = P + '-card';
                card.dataset.id = item.id || item.Id || '';

                var streamUrl = item.streamUrl || item.StreamUrl || '';
                var name = item.name || item.Name || '';
                var duration = item.durationSeconds || item.DurationSeconds || 0;
                var videoCodec = (item.videoCodec || item.VideoCodec || '').toLowerCase();
                var audioCodec = (item.audioCodec || item.AudioCodec || '').toLowerCase();
                var containerFmt = (item.container || item.Container || '').toLowerCase();

                if (!streamUrl) return;

                var src = streamUrl.indexOf('http') === 0 ? streamUrl : BASE + streamUrl;
                src = ensureApiKey(src, API_KEY);

                var transcodeParams = 'VideoCodec=h264&AudioCodec=aac&VideoBitrate=4000000&AudioBitrate=192000';
                var hlsSrc = '';
                var streamMatch = src.match(/\/Videos\/([^/]+)\/stream\?(.*)/);
                if (streamMatch) {
                    var videoId = streamMatch[1];
                    var qs = streamMatch[2]
                        .replace(/(^|&)static=true&?/i, '$1')
                        .replace(/(^|&)api_key=[^&]*/i, '$1')
                        .replace(/^&+/, '').replace(/&+$/, '');
                    hlsSrc = BASE + '/Videos/' + videoId + '/main.m3u8?'
                        + (qs ? qs + '&' : '')
                        + 'api_key=' + encodeURIComponent(API_KEY) + '&'
                        + transcodeParams;
                } else {
                    var idForHls = card.dataset.id;
                    if (idForHls) {
                        hlsSrc = BASE + '/Videos/' + idForHls + '/main.m3u8?api_key='
                            + encodeURIComponent(API_KEY) + '&' + transcodeParams;
                    }
                }

                card.innerHTML =
                    '<div class="' + P + '-top-gradient"></div>' +
                    '<div class="' + P + '-bottom-gradient"></div>' +
                    '<div class="' + P + '-poster"></div>' +
                    '<div class="' + P + '-loader"></div>' +
                    '<video preload="metadata" playsinline webkit-playsinline x-webkit-airplay="deny" disablepictureinpicture controlsList="nodownload noplaybackrate noremoteplayback" loop muted></video>' +
                    '<div class="' + P + '-overlay"></div>' +
                    '<div class="' + P + '-center-anim"></div>' +
                    '<div class="' + P + '-caption"><div class="title"></div><div class="meta"></div></div>' +
                    '<div class="' + P + '-actions">' +
                      '<div class="action-item"><div class="icon like"><svg class="' + P + '-lucide" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z"></path></svg></div></div>' +
                      '<div class="action-item"><div class="icon mute-icon"><svg class="' + P + '-lucide" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polygon points="11 5 6 9 2 9 2 15 6 15 11 19 11 5"></polygon><line x1="23" y1="9" x2="17" y2="15"></line><line x1="17" y1="9" x2="23" y2="15"></line></svg></div></div>' +
                    '</div>' +
                    '<div class="' + P + '-controls">' +
                      '<div class="' + P + '-progress-container">' +
                        '<div class="' + P + '-progress-time">00:00 / 00:00</div>' +
                        '<div class="' + P + '-progress-bg"></div>' +
                        '<div class="' + P + '-progress-buffer"></div>' +
                        '<div class="' + P + '-progress-played"></div>' +
                        '<div class="' + P + '-progress-handle"></div>' +
                      '</div>' +
                    '</div>';

                card.querySelector('.title').textContent = name;
                card.querySelector('.meta').textContent = Math.round(duration) + 's';

                var posterEl = card.querySelector('.' + P + '-poster');
                var loaderEl = card.querySelector('.' + P + '-loader');
                posterEl.style.backgroundImage = 'url("' + BASE + '/Items/' + card.dataset.id + '/Images/Primary?fillHeight=1080&fillWidth=720&quality=80")';

                var v = card.querySelector('video');
                var initialized = false;
                var triedTranscode = false;
                var hls = null;
                var srcLoaded = false;
                var isDragging = false;
                var isLiked = false;

                var likeIcon = card.querySelector('.icon.like');
                var muteIcon = card.querySelector('.mute-icon');
                var overlay = card.querySelector('.' + P + '-overlay');
                var progressContainer = card.querySelector('.' + P + '-progress-container');
                var controls = card.querySelector('.' + P + '-controls');
                var progressPlayed = card.querySelector('.' + P + '-progress-played');
                var progressBuffer = card.querySelector('.' + P + '-progress-buffer');
                var progressHandle = card.querySelector('.' + P + '-progress-handle');
                var progressTime = card.querySelector('.' + P + '-progress-time');
                var centerAnim = card.querySelector('.' + P + '-center-anim');

                

                function showLoading() { loaderEl.classList.add('show'); posterEl.classList.remove('hidden'); }
                function hideLoading() { loaderEl.classList.remove('show'); posterEl.classList.add('hidden'); }

                function isVideoCodecSupported(codec) {
                    if (!codec) return null;
                    var supported = ['h264', 'avc', 'avc1', 'mpeg4', 'mp4v'];
                    if (codec === 'vp8' || codec === 'vp9') {
                        try { return v.canPlayType('video/webm; codecs=' + codec) !== ''; } catch(e) { return false; }
                    }
                    if (codec === 'hevc' || codec === 'h265') {
                        try { return v.canPlayType('video/mp4; codecs=hev1') !== '' || v.canPlayType('video/mp4; codecs=hvc1') !== ''; } catch(e) { return false; }
                    }
                    if (codec === 'av1' || codec === 'av01') {
                        try { return v.canPlayType('video/mp4; codecs=av01.0.05M.08') !== ''; } catch(e) { return false; }
                    }
                    if (codec.indexOf('mpeg2') >= 0 || codec.indexOf('msmpeg4') >= 0 || codec === 'wmv3' || codec === 'wmv2' || codec === 'vc1') return false;
                    return supported.indexOf(codec) >= 0;
                }

                function isAudioCodecSupported(codec) {
                    if (!codec) return null;
                    var supported = ['aac', 'mp3', 'mp2', 'opus', 'vorbis', 'flac', 'pcm'];
                    if (codec === 'ac3' || codec === 'eac3' || codec === 'dts' || codec === 'truehd') return false;
                    return supported.indexOf(codec) >= 0;
                }

                function shouldDirectStream() {
                    var videoOk = isVideoCodecSupported(videoCodec);
                    var audioOk = isAudioCodecSupported(audioCodec);
                    if (videoOk === false || audioOk === false) return false;
                    var unsupported = ['avi', 'mkv', 'mov', 'wmv', 'flv', 'rmvb', 'rm', 'ts', 'm2ts'];
                    if (containerFmt && unsupported.indexOf(containerFmt) >= 0) return false;
                    return true;
                }

                var useTranscode = !shouldDirectStream();

                function initPlayer() {
                    if (initialized) return;
                    initialized = true;
                    showLoading();
                    if (useTranscode && hlsSrc) {
                        if (hls) { v.play().catch(function(){}); }
                        else { triedTranscode = true; fallbackToHls(); }
                    } else {
                        if (srcLoaded) { v.play().catch(function(){}); }
                        else { v.src = src; v.load(); }
                    }
                }

                v.addEventListener('playing', function() { hideLoading(); card.classList.remove('paused'); });
                v.addEventListener('pause', function() { card.classList.add('paused'); });
                v.addEventListener('waiting', function() { showLoading(); });

                v.addEventListener('timeupdate', function() {
                    if (isDragging) return;
                    var pct = v.duration ? (v.currentTime / v.duration) * 100 : 0;
                    progressPlayed.style.width = pct + '%';
                    progressHandle.style.left = pct + '%';
                    progressTime.textContent = formatTime(v.currentTime) + ' / ' + formatTime(v.duration);
                });

                v.addEventListener('loadedmetadata', function() {
                    progressTime.textContent = '00:00 / ' + formatTime(v.duration);
                    if (triedTranscode) return;
                    var w = v.videoWidth || 0, h = v.videoHeight || 0;
                    if (w === 0 || h === 0) {
                        triedTranscode = true;
                        console.warn('[ShortsModule] no video track, falling back to HLS');
                        fallbackToHls();
                    }
                });

                v.addEventListener('progress', function() {
                    if (v.buffered && v.buffered.length > 0 && v.duration) {
                        progressBuffer.style.width = ((v.buffered.end(v.buffered.length - 1) / v.duration) * 100) + '%';
                    }
                });

                function destroyPlayer() {
                    if (hls) {
                        try { hls.stopLoad(); hls.detachMedia(); hls.destroy(); } catch(e) {}
                        hls = null;
                    }
                    v.pause();
                    v.removeAttribute('src');
                    v.load();
                    initialized = false;
                    triedTranscode = false;
                    srcLoaded = false;
                    card._prefetched = false;
                    if (card._prefetchSlotTaken) {
                        card._prefetchSlotTaken = false;
                        transcodeDone();
                    }
                    progressPlayed.style.width = '0%';
                    progressBuffer.style.width = '0%';
                    progressHandle.style.left = '0%';
                    progressTime.textContent = '00:00 / 00:00';
                    showLoading();
                }

                v.addEventListener('error', function() {
                    if (!triedTranscode && hlsSrc && hlsSrc !== src) {
                        triedTranscode = true;
                        fallbackToHls();
                    }
                });

                v.addEventListener('play', function() {
                    if (triedTranscode) return;
                    setTimeout(function() {
                        if (triedTranscode) return;
                        if ((v.videoWidth || 0) === 0 || (v.videoHeight || 0) === 0) {
                            triedTranscode = true;
                            fallbackToHls();
                        }
                    }, 2000);
                });

                function fallbackToHls() {
                    if (hls) { v.play().catch(function(){}); return; }
                    v.pause();
                    v.removeAttribute('src');
                    if (v.canPlayType('application/vnd.apple.mpegurl')) {
                        v.src = hlsSrc; v.load(); v.play().catch(function(){}); return;
                    }
                    if (window.Hls && Hls.isSupported()) {
                        var retries = 0;
                        hls = new Hls({
                            enableWorker: true, lowLatencyMode: false,
                            backBufferLength: 30, maxBufferLength: 15, maxMaxBufferLength: 30, startFragPrefetch: true
                        });
                        hls.loadSource(hlsSrc);
                        hls.attachMedia(v);
                        hls.on(Hls.Events.MANIFEST_PARSED, function() { v.play().catch(function(){}); });
                        hls.on(Hls.Events.ERROR, function(event, data) {
                            if (data.fatal) {
                                if (data.type === Hls.ErrorTypes.NETWORK_ERROR && retries < 2) {
                                    retries++;
                                    setTimeout(function() { hls.startLoad(); }, 1000 * retries);
                                }
                            }
                        });
                        hls.on(Hls.Events.FRAG_BUFFERED, function() {
                            if (v.buffered && v.buffered.length > 0 && v.duration) {
                                progressBuffer.style.width = ((v.buffered.end(v.buffered.length - 1) / v.duration) * 100) + '%';
                            }
                        });
                    }
                }

                function showCenterAnim(playState) {
                    setIcon(centerAnim, playState ? 'play' : 'pause');
                    centerAnim.classList.add('show');
                    setTimeout(function() { centerAnim.classList.remove('show'); }, 300);
                }

                function togglePlay() {
                    if (v.paused) { v.play().catch(function(){}); showCenterAnim(true); }
                    else { v.pause(); showCenterAnim(false); }
                }

                var lastClickTime = 0;
                card.addEventListener('click', function(e) {
                    if (e.target.closest('.' + P + '-actions')) return;
                    if (e.target.closest('.' + P + '-controls')) return;
                    var now = Date.now();
                    if (now - lastClickTime < 300) {
                        e.preventDefault();
                        handleDoubleTap(e);
                        lastClickTime = 0;
                        return;
                    }
                    lastClickTime = now;
                    setTimeout(function() {
                        if (Date.now() - lastClickTime >= 290 && lastClickTime !== 0) {
                            togglePlay();
                            lastClickTime = 0;
                        }
                    }, 300);
                });

                function handleDoubleTap(e) {
                    toggleLike();
                    var heart = document.createElement('div');
                    heart.className = P + '-heart';
                    heart.innerHTML = '<svg width="80" height="80" viewBox="0 0 24 24" fill="#ff4757" stroke="#ff4757" stroke-width="2"><path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z"></path></svg>';
                    var rect = card.getBoundingClientRect();
                    heart.style.left = (e.clientX - rect.left) + 'px';
                    heart.style.top = (e.clientY - rect.top) + 'px';
                    heart.style.transform = 'translate(-50%, -50%)';
                    card.appendChild(heart);
                    setTimeout(function() { heart.remove(); }, 800);
                }

                function toggleLike() {
                    var newLiked = !isLiked;
                    // 先更新 UI，再调 API（乐观更新）
                    isLiked = newLiked;
                    if (isLiked) { likeIcon.classList.add('liked'); }
                    else { likeIcon.classList.remove('liked'); }

                    // 调用 Jellyfin 收藏 API
                    toggleJellyfinFavorite(card.dataset.id, newLiked);
                }

                // 调用 Jellyfin 收藏/取消收藏 API
                function toggleJellyfinFavorite(itemId, favorite) {
                    if (!itemId) return;
                    var userId = getUserId();
                    if (!userId) {
                        console.warn('[ShortsModule] 无法获取 userId，跳过收藏 API');
                        return;
                    }

                    var url = BASE + '/Users/' + userId + '/FavoriteItems/' + itemId + '?api_key=' + encodeURIComponent(API_KEY);

                    fetch(url, {
                        method: favorite ? 'POST' : 'DELETE',
                        headers: { 'Content-Type': 'application/json' }
                    }).then(function(r) {
                        if (r.ok) {
                            console.log('[ShortsModule] 收藏' + (favorite ? '成功' : '已取消') + ':', itemId);
                        } else {
                            console.error('[ShortsModule] 收藏 API 失败:', r.status);
                            // 回滚 UI
                            isLiked = !favorite;
                            if (isLiked) { likeIcon.classList.add('liked'); }
                            else { likeIcon.classList.remove('liked'); }
                        }
                    }).catch(function(e) {
                        console.error('[ShortsModule] 收藏 API 异常:', e);
                    });
                }

                // 查询初始收藏状态
                function checkFavoriteStatus(itemId) {
                    if (!itemId) return;
                    var userId = getUserId();
                    if (!userId) return;

                    var url = BASE + '/Users/' + userId + '/Items/' + itemId + '?api_key=' + encodeURIComponent(API_KEY);

                    fetch(url)
                        .then(function(r) { return r.json(); })
                        .then(function(data) {
                            if (data && data.UserData && data.UserData.IsFavorite) {
                                isLiked = true;
                                likeIcon.classList.add('liked');
                            }
                        })
                        .catch(function(e) {});
                }

                // 异步查询收藏状态
                checkFavoriteStatus(card.dataset.id);

                likeIcon.addEventListener('click', function(e) { e.stopPropagation(); toggleLike(); });
                muteIcon.addEventListener('click', function(e) {
                    e.stopPropagation();
                    globalMuted = !globalMuted;
                    syncMuteAll();
                });

                progressContainer.addEventListener('mousedown', function(e) { e.stopPropagation(); startDrag(e); });
                progressContainer.addEventListener('touchstart', function(e) { e.stopPropagation(); startDrag(e.touches[0]); }, { passive: false });

                // 触摸进度条时显示
                var hideTouchTimer = null;
                progressContainer.addEventListener('touchstart', function(e) {
                    clearTimeout(hideTouchTimer);
                    progressContainer.classList.add('touch-show');
                }, { passive: true });
                progressContainer.addEventListener('touchend', function() {
                    hideTouchTimer = setTimeout(function() { progressContainer.classList.remove('touch-show'); }, 1500);
                }, { passive: true });
                progressContainer.addEventListener('touchcancel', function() {
                    hideTouchTimer = setTimeout(function() { progressContainer.classList.remove('touch-show'); }, 1500);
                });

                function startDrag(e) {
                    isDragging = true;
                    progressContainer.classList.add('dragging');
                    updateProgressFromEvent(e);
                    document.addEventListener('mousemove', onDragMove);
                    document.addEventListener('mouseup', onDragEnd);
                    document.addEventListener('touchmove', onTouchMove, { passive: false });
                    document.addEventListener('touchend', onTouchEnd);
                    // 禁止页面上下滚动
                    var feedEl = feed;
                    if (feedEl) feedEl.style.overflowY = 'hidden';
                }
                function onDragMove(e) { if (isDragging) updateProgressFromEvent(e); }
                function onTouchMove(e) { if (isDragging) { e.preventDefault(); updateProgressFromEvent(e.touches[0]); } }
                function onDragEnd(e) { if (isDragging) { updateProgressFromEvent(e); finishDrag(); } }
                function onTouchEnd(e) { if (isDragging) finishDrag(); }
                function finishDrag() {
                    isDragging = false;
                    progressContainer.classList.remove('dragging');
                    document.removeEventListener('mousemove', onDragMove);
                    document.removeEventListener('mouseup', onDragEnd);
                    document.removeEventListener('touchmove', onTouchMove);
                    document.removeEventListener('touchend', onTouchEnd);
                    // 恢复页面滚动
                    var feedEl = feed;
                    if (feedEl) feedEl.style.overflowY = '';
                }
                function updateProgressFromEvent(e) {
                    var rect = progressContainer.getBoundingClientRect();
                    var pct = Math.max(0, Math.min(1, (e.clientX - rect.left) / rect.width));
                    progressPlayed.style.width = (pct * 100) + '%';
                    progressHandle.style.left = (pct * 100) + '%';
                    if (v.duration) {
                        var t = pct * v.duration;
                        v.currentTime = t;
                        progressTime.textContent = formatTime(t) + ' / ' + formatTime(v.duration);
                    }
                }

                card._initPlayer = initPlayer;
                card._destroyPlayer = destroyPlayer;
                card._video = v;
                card._prefetched = false;
                card._prefetchSlotTaken = false;

                card._prefetch = function() {
                    if (card._prefetched) return;
                    card._prefetched = true;
                    if (useTranscode && hlsSrc) {
                        var doPrefetch = function() {
                            if (!card._prefetched) { transcodeDone(); return; }
                            card._prefetchSlotTaken = true;
                            if (window.Hls && Hls.isSupported()) {
                                var retries = 0;
                                hls = new Hls({
                                    enableWorker: true, lowLatencyMode: false,
                                    backBufferLength: 30, maxBufferLength: 15, maxMaxBufferLength: 30, startFragPrefetch: true
                                });
                                hls.loadSource(hlsSrc);
                                hls.attachMedia(v);
                                triedTranscode = true;
                                hls.on(Hls.Events.MANIFEST_PARSED, function() {
                                    card._prefetchSlotTaken = false;
                                    transcodeDone();
                                });
                                hls.on(Hls.Events.ERROR, function(event, data) {
                                    if (data.fatal) {
                                        if (data.type === Hls.ErrorTypes.NETWORK_ERROR && retries < 2) {
                                            retries++;
                                            setTimeout(function() { hls.startLoad(); }, 1000 * retries);
                                        } else {
                                            try { hls.destroy(); } catch(e) {}
                                            hls = null;
                                            card._prefetched = false;
                                            card._prefetchSlotTaken = false;
                                            triedTranscode = false;
                                            transcodeDone();
                                        }
                                    }
                                });
                            } else if (v.canPlayType('application/vnd.apple.mpegurl')) {
                                v.src = hlsSrc; v.preload = 'auto'; v.load();
                                triedTranscode = true;
                                card._prefetchSlotTaken = false;
                                transcodeDone();
                            } else {
                                card._prefetchSlotTaken = false;
                                transcodeDone();
                            }
                        };
                        scheduleTranscode(doPrefetch);
                    } else {
                        v.src = src; v.preload = 'auto'; v.load();
                        srcLoaded = true;
                    }
                };

                v.muted = globalMuted;
                v.volume = globalVolume;
                muteIcon._setIcon = function(name) { setIcon(muteIcon, name); };

                feed.appendChild(card);
            }

            var snapTimer;
            function onScroll() {
                clearTimeout(snapTimer);
                snapTimer = setTimeout(function() {
                    playVisible();
                    if (feed.scrollTop + feed.clientHeight > feed.scrollHeight - feed.clientHeight) {
                        loadBatch();
                    }
                }, 120);
            }
            feed.addEventListener('scroll', onScroll);

            function playVisible() {
                var cards = feed.querySelectorAll('.' + P + '-card');
                var center = feed.scrollTop + feed.clientHeight / 2;
                var visibleIndex = -1;
                for (var i = 0; i < cards.length; i++) {
                    var top = cards[i].offsetTop, bottom = top + cards[i].offsetHeight;
                    if (center >= top && center < bottom) { visibleIndex = i; break; }
                }
                cards.forEach(function(card, i) {
                    var v = card._video;
                    var isVisible = i === visibleIndex;
                    if (v) { v.muted = globalMuted; v.volume = globalVolume; }
                    if (isVisible) {
                        if (card._initPlayer) card._initPlayer();
                        v.play().catch(function() {});
                    } else {
                        v.pause();
                        if (Math.abs(i - visibleIndex) > 3 && card._destroyPlayer) card._destroyPlayer();
                    }
                });
                if (visibleIndex >= 0) {
                    for (var j = 1; j <= 3; j++) {
                        var nextIdx = visibleIndex + j;
                        if (nextIdx < cards.length && cards[nextIdx]._prefetch) cards[nextIdx]._prefetch();
                    }
                    var remaining = cards.length - visibleIndex - 1;
                    if (remaining <= 3 && !isLoading) loadBatch();
                }
            }

            function onKeydown(e) {
                if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
                    e.preventDefault();
                    feed.scrollBy({ top: e.key === 'ArrowDown' ? feed.clientHeight : -feed.clientHeight, behavior: 'smooth' });
                }
            }
            document.addEventListener('keydown', onKeydown);

            var state = {
                feed: feed,
                onScroll: onScroll,
                onKeydown: onKeydown,
                destroy: function() {
                    feed.querySelectorAll('.' + P + '-card').forEach(function(card) {
                        if (card._destroyPlayer) card._destroyPlayer();
                    });
                    feed.removeEventListener('scroll', onScroll);
                    document.removeEventListener('keydown', onKeydown);
                }
            };

            // 获取当前可见卡片
            function getCurrentCard() {
                var cards = feed.querySelectorAll('.' + P + '-card');
                var center = feed.scrollTop + feed.clientHeight / 2;
                for (var i = 0; i < cards.length; i++) {
                    var top = cards[i].offsetTop, bottom = top + cards[i].offsetHeight;
                    if (center >= top && center < bottom) return cards[i];
                }
                return null;
            }

            function forceRefreshFeed() {
                feed.innerHTML = '';
                firstBatchLoaded = false;
                isLoading = false;
                loadBatch();
            }

            // 为 hub 的按钮注入动作
            var hubButtons = container._hubButtons;
            if (hubButtons && hubButtons.length >= 2) {
                // 首页按钮（索引 0）- 双击强制刷新视频流
                var homeBtn = hubButtons[0];
                var homeLastClickTime = 0;
                homeBtn.addEventListener('click', function(e) {
                    e.preventDefault();
                    var now = Date.now();
                    if (now - homeLastClickTime < 300) {
                        forceRefreshFeed();
                        homeLastClickTime = 0;
                    } else {
                        homeLastClickTime = now;
                    }
                });

                // 刷新按钮（索引 1）- 刷新当前视频
                hubButtons[1].addEventListener('click', function(e) {
                    e.preventDefault();
                    var card = getCurrentCard();
                    if (card && card._destroyPlayer) {
                        card._destroyPlayer();
                        setTimeout(function() {
                            if (card._initPlayer) card._initPlayer();
                            var v = card._video;
                            if (v) v.play().catch(function() {});
                        }, 100);
                    }
                });

                // 收藏按钮（索引 2）- 打开收藏列表
                if (hubButtons.length >= 3) {
                    hubButtons[2].addEventListener('click', function(e) {
                        e.preventDefault();
                        showFavoritesPanel();
                    });
                }
            }

            var favoritesPanel = container._favoritesPanel;

            function toggleFavoritesPanel(show) {
                if (favoritesPanel) {
                    favoritesPanel.classList.toggle('show', show);
                }
            }

            function showFavoritesPanel() {
                toggleFavoritesPanel(true);
                loadFavorites();
            }

            function hideFavoritesPanel() {
                toggleFavoritesPanel(false);
            }

            function loadFavorites() {
                var userId = getUserId();
                if (!userId) {
                    favoritesPanel.querySelector('.' + P + '-favorites-grid').innerHTML =
                        '<div class="' + P + '-favorites-empty">无法获取用户信息</div>';
                    return;
                }

                var url = BASE + '/Users/' + userId + '/Items?IsFavorite=true&Recursive=true&IncludeItemTypes=Video&Fields=BasicSyncInfo,PrimaryImageAspectRatio&api_key=' + encodeURIComponent(API_KEY);

                fetch(url)
                    .then(function(r) { return r.json(); })
                    .then(function(data) {
                        var items = data.Items || [];
                        var grid = favoritesPanel.querySelector('.' + P + '-favorites-grid');

                        if (items.length === 0) {
                            grid.innerHTML = '<div class="' + P + '-favorites-empty">暂无收藏内容</div>';
                            return;
                        }

                        grid.innerHTML = '';
                        items.forEach(function(item) {
                            var el = document.createElement('div');
                            el.className = P + '-favorites-item';
                            el.innerHTML =
                                '<img src="' + BASE + '/Items/' + item.Id + '/Images/Primary?fillHeight=320&fillWidth=180&quality=80" alt="' + (item.Name || '') + '">' +
                                '<div class="name">' + (item.Name || '') + '</div>';
                            el.addEventListener('click', function() {
                                hideFavoritesPanel();
                                playFavoriteItem(item);
                            });
                            grid.appendChild(el);
                        });
                    })
                    .catch(function(e) {
                        console.error('[ShortsModule] 加载收藏列表失败:', e);
                        favoritesPanel.querySelector('.' + P + '-favorites-grid').innerHTML =
                            '<div class="' + P + '-favorites-empty">加载失败</div>';
                    });
            }

            function playFavoriteItem(item) {
                feed.innerHTML = '';
                firstBatchLoaded = false;
                isLoading = false;
                appendCard({
                    id: item.Id,
                    name: item.Name,
                    streamUrl: '/Videos/' + item.Id + '/stream?static=true',
                    durationSeconds: item.RunTimeTicks ? item.RunTimeTicks / 10000000 : 0,
                    videoCodec: '',
                    audioCodec: '',
                    container: ''
                });
                firstBatchLoaded = true;
                setTimeout(function() { playVisible(); }, 200);
            }

            // 收藏面板返回按钮
            var backBtn = favoritesPanel.querySelector('.' + P + '-favorites-back');
            if (backBtn) {
                backBtn.addEventListener('click', function(e) {
                    e.preventDefault();
                    hideFavoritesPanel();
                });
            }

            var scriptsToLoad = [];
            if (!window.Hls) {
                scriptsToLoad.push('https://cdn.jsdelivr.net/npm/hls.js@1.5.13/dist/hls.min.js');
            }
            if (!window.lucide) {
                scriptsToLoad.push('https://unpkg.com/lucide@latest/dist/umd/lucide.js');
            }

            function loadScripts(urls, callback) {
                if (urls.length === 0) { callback(); return; }
                var url = urls.shift();
                var s = document.createElement('script');
                s.src = url;
                s.onload = function() { loadScripts(urls, callback); };
                s.onerror = function() { console.warn('[ShortsModule] failed to load:', url); loadScripts(urls, callback); };
                document.head.appendChild(s);
            }

            loadScripts(scriptsToLoad, function() {
                console.log('[ShortsModule] libraries loaded, starting feed');
                loadBatch();
            });

            return state;
        }

        // 对外暴露
        return {
            buildContainer: buildContainer,
            initPlayer: initPlayer
        };
    })();
