import { useRef, useEffect, useState, useCallback } from 'react';
import { Heart, Volume2, VolumeX } from 'lucide-react';
import { formatTime, shouldDirectStream, buildHlsSrc } from '../utils/videoUtils';
import { getToken, getUserId, BASE_URL, apiUrl, ensureApiKey } from '../common/auth';

export default function VideoCard({ item, globalMuted, onMuteToggle, onLikeChange }) {
  const videoRef = useRef(null);
  const cardRef = useRef(null);
  const hlsRef = useRef(null);
  const initializedRef = useRef(false);
  const triedTranscodeRef = useRef(false);
  const isDraggingRef = useRef(false);
  const lastClickTimeRef = useRef(0);
  const hideTouchTimerRef = useRef(null);
  const centerAnimTimeoutRef = useRef(null);

  const [isLiked, setIsLiked] = useState(false);
  const [showCenterAnim, setShowCenterAnim] = useState(false);
  const [centerAnimIcon, setCenterAnimIcon] = useState('play');
  const [progress, setProgress] = useState(0);
  const [buffer, setBuffer] = useState(0);
  const [timeDisplay, setTimeDisplay] = useState('00:00 / 00:00');
  const [isLoading, setIsLoading] = useState(true);
  const [isPaused, setIsPaused] = useState(false);
  const [isDragging, setIsDragging] = useState(false);
  const [touchShow, setTouchShow] = useState(false);
  const [showHeart, setShowHeart] = useState(false);
  const [heartPos, setHeartPos] = useState({ x: 0, y: 0 });

  const API_KEY = getToken();
  const userId = getUserId();

  const streamUrl = item.streamUrl || item.StreamUrl || '';
  const name = item.name || item.Name || '';
  const duration = item.durationSeconds || item.DurationSeconds || 0;
  const videoCodec = (item.videoCodec || item.VideoCodec || '').toLowerCase();
  const audioCodec = (item.audioCodec || item.AudioCodec || '').toLowerCase();
  const container = (item.container || item.Container || '').toLowerCase();

  const useTranscode = !shouldDirectStream(videoCodec, audioCodec, container);
  const src = streamUrl.indexOf('http') === 0 ? streamUrl : BASE_URL + streamUrl;
  const srcWithKey = ensureApiKey(src, API_KEY);
  const hlsSrc = buildHlsSrc(streamUrl, item.id || item.Id || '', API_KEY, BASE_URL);

  const posterUrl = BASE_URL + '/Items/' + (item.id || item.Id || '') + '/Images/Primary?fillHeight=1080&fillWidth=720&quality=80';

  const initPlayer = useCallback(() => {
    if (initializedRef.current) return;
    initializedRef.current = true;
    setIsLoading(true);
    const v = videoRef.current;
    if (!v) return;

    if (useTranscode && hlsSrc) {
      fallbackToHls();
    } else {
      v.src = srcWithKey;
      v.load();
    }
  }, [useTranscode, hlsSrc, srcWithKey]);

  const destroyPlayer = useCallback(() => {
    const v = videoRef.current;
    if (hlsRef.current) {
      try {
        hlsRef.current.stopLoad();
        hlsRef.current.detachMedia();
        hlsRef.current.destroy();
      } catch (e) {}
      hlsRef.current = null;
    }
    if (v) {
      v.pause();
      v.removeAttribute('src');
      v.load();
    }
    initializedRef.current = false;
    triedTranscodeRef.current = false;
    setIsLoading(true);
    setProgress(0);
    setBuffer(0);
    setTimeDisplay('00:00 / 00:00');
  }, []);

  const fallbackToHls = useCallback(() => {
    if (!hlsSrc) return;
    const v = videoRef.current;
    if (!v) return;
    v.pause();
    v.removeAttribute('src');

    if (v.canPlayType('application/vnd.apple.mpegurl')) {
      v.src = hlsSrc;
      v.load();
      v.play().catch(() => {});
      return;
    }

    if (window.Hls && Hls.isSupported()) {
      let retries = 0;
      const hls = new Hls({
        enableWorker: true,
        lowLatencyMode: false,
        backBufferLength: 30,
        maxBufferLength: 15,
        maxMaxBufferLength: 30,
        startFragPrefetch: true
      });
      hlsRef.current = hls;
      hls.loadSource(hlsSrc);
      hls.attachMedia(v);
      hls.on(Hls.Events.MANIFEST_PARSED, () => {
        v.play().catch(() => {});
      });
      hls.on(Hls.Events.ERROR, (event, data) => {
        if (data.fatal) {
          if (data.type === Hls.ErrorTypes.NETWORK_ERROR && retries < 2) {
            retries++;
            setTimeout(() => { hls.startLoad(); }, 1000 * retries);
          }
        }
      });
      hls.on(Hls.Events.FRAG_BUFFERED, () => {
        if (v.buffered && v.buffered.length > 0 && v.duration) {
          setBuffer((v.buffered.end(v.buffered.length - 1) / v.duration) * 100);
        }
      });
    }
  }, [hlsSrc]);

  const togglePlay = useCallback(() => {
    const v = videoRef.current;
    if (!v) return;
    if (v.paused) {
      v.play().catch(() => {});
      setCenterAnimIcon('play');
    } else {
      v.pause();
      setCenterAnimIcon('pause');
    }
    setShowCenterAnim(true);
    if (centerAnimTimeoutRef.current) clearTimeout(centerAnimTimeoutRef.current);
    centerAnimTimeoutRef.current = setTimeout(() => setShowCenterAnim(false), 300);
  }, []);

  const toggleLike = useCallback(() => {
    const newLiked = !isLiked;
    setIsLiked(newLiked);

    const itemId = item.id || item.Id || '';
    if (itemId && userId) {
      const url = BASE_URL + '/Users/' + userId + '/FavoriteItems/' + itemId + '?api_key=' + encodeURIComponent(API_KEY);
      fetch(url, {
        method: newLiked ? 'POST' : 'DELETE',
        headers: { 'Content-Type': 'application/json' }
      }).then(r => {
        if (!r.ok) {
          setIsLiked(!newLiked);
        }
      }).catch(() => {
        setIsLiked(!newLiked);
      });
    }
  }, [isLiked, item.id, item.Id, userId, API_KEY]);

  const handleDoubleTap = useCallback((e) => {
    toggleLike();
    const rect = cardRef.current.getBoundingClientRect();
    setHeartPos({ x: e.clientX - rect.left, y: e.clientY - rect.top });
    setShowHeart(true);
    setTimeout(() => setShowHeart(false), 800);
  }, [toggleLike]);

  const handleCardClick = useCallback((e) => {
    if (e.target.closest('.sv-actions')) return;
    if (e.target.closest('.sv-controls')) return;

    const now = Date.now();
    if (now - lastClickTimeRef.current < 300) {
      e.preventDefault();
      handleDoubleTap(e);
      lastClickTimeRef.current = 0;
      return;
    }
    lastClickTimeRef.current = now;
    setTimeout(() => {
      if (Date.now() - lastClickTimeRef.current >= 290 && lastClickTimeRef.current !== 0) {
        togglePlay();
        lastClickTimeRef.current = 0;
      }
    }, 300);
  }, [handleDoubleTap, togglePlay]);

  const handleProgressDown = useCallback((e) => {
    e.stopPropagation();
    isDraggingRef.current = true;
    setIsDragging(true);
    updateProgressFromEvent(e);
    document.addEventListener('mousemove', handleDragMove);
    document.addEventListener('mouseup', handleDragEnd);
    document.addEventListener('touchmove', handleTouchMove, { passive: false });
    document.addEventListener('touchend', handleTouchEnd);
    const feed = cardRef.current?.closest('.sv-feed');
    if (feed) feed.style.overflowY = 'hidden';
  }, []);

  const updateProgressFromEvent = useCallback((e) => {
    const progressContainer = cardRef.current?.querySelector('.sv-progress-container');
    if (!progressContainer) return;
    const rect = progressContainer.getBoundingClientRect();
    const pct = Math.max(0, Math.min(1, (e.clientX - rect.left) / rect.width));
    setProgress(pct * 100);
    const v = videoRef.current;
    if (v && v.duration) {
      const t = pct * v.duration;
      v.currentTime = t;
      setTimeDisplay(formatTime(t) + ' / ' + formatTime(v.duration));
    }
  }, []);

  const handleDragMove = useCallback((e) => {
    if (isDraggingRef.current) updateProgressFromEvent(e);
  }, [updateProgressFromEvent]);

  const handleTouchMove = useCallback((e) => {
    if (isDraggingRef.current) {
      e.preventDefault();
      updateProgressFromEvent(e.touches[0]);
    }
  }, [updateProgressFromEvent]);

  const handleDragEnd = useCallback((e) => {
    if (isDraggingRef.current) {
      updateProgressFromEvent(e);
      finishDrag();
    }
  }, [updateProgressFromEvent]);

  const handleTouchEnd = useCallback(() => {
    if (isDraggingRef.current) finishDrag();
  }, []);

  const finishDrag = useCallback(() => {
    isDraggingRef.current = false;
    setIsDragging(false);
    document.removeEventListener('mousemove', handleDragMove);
    document.removeEventListener('mouseup', handleDragEnd);
    document.removeEventListener('touchmove', handleTouchMove);
    document.removeEventListener('touchend', handleTouchEnd);
    const feed = cardRef.current?.closest('.sv-feed');
    if (feed) feed.style.overflowY = '';
  }, [handleDragMove, handleDragEnd, handleTouchMove, handleTouchEnd]);

  const handleTouchStart = useCallback(() => {
    if (hideTouchTimerRef.current) clearTimeout(hideTouchTimerRef.current);
    setTouchShow(true);
  }, []);

  const handleTouchEndProgress = useCallback(() => {
    hideTouchTimerRef.current = setTimeout(() => setTouchShow(false), 1500);
  }, []);

  useEffect(() => {
    const v = videoRef.current;
    if (!v) return;

    const onPlaying = () => {
      setIsLoading(false);
      setIsPaused(false);
    };
    const onPause = () => setIsPaused(true);
    const onWaiting = () => setIsLoading(true);
    const onTimeUpdate = () => {
      if (isDraggingRef.current) return;
      const pct = v.duration ? (v.currentTime / v.duration) * 100 : 0;
      setProgress(pct);
      setTimeDisplay(formatTime(v.currentTime) + ' / ' + formatTime(v.duration));
    };
    const onLoadedMetadata = () => {
      setTimeDisplay('00:00 / ' + formatTime(v.duration));
      if (triedTranscodeRef.current) return;
      const w = v.videoWidth || 0, h = v.videoHeight || 0;
      if (w === 0 || h === 0) {
        triedTranscodeRef.current = true;
        fallbackToHls();
      }
    };
    const onProgress = () => {
      if (v.buffered && v.buffered.length > 0 && v.duration) {
        setBuffer((v.buffered.end(v.buffered.length - 1) / v.duration) * 100);
      }
    };
    const onError = () => {
      if (!triedTranscodeRef.current && hlsSrc && hlsSrc !== srcWithKey) {
        triedTranscodeRef.current = true;
        fallbackToHls();
      }
    };
    const onPlay = () => {
      if (triedTranscodeRef.current) return;
      setTimeout(() => {
        if (triedTranscodeRef.current) return;
        if ((v.videoWidth || 0) === 0 || (v.videoHeight || 0) === 0) {
          triedTranscodeRef.current = true;
          fallbackToHls();
        }
      }, 2000);
    };

    v.addEventListener('playing', onPlaying);
    v.addEventListener('pause', onPause);
    v.addEventListener('waiting', onWaiting);
    v.addEventListener('timeupdate', onTimeUpdate);
    v.addEventListener('loadedmetadata', onLoadedMetadata);
    v.addEventListener('progress', onProgress);
    v.addEventListener('error', onError);
    v.addEventListener('play', onPlay);

    return () => {
      v.removeEventListener('playing', onPlaying);
      v.removeEventListener('pause', onPause);
      v.removeEventListener('waiting', onWaiting);
      v.removeEventListener('timeupdate', onTimeUpdate);
      v.removeEventListener('loadedmetadata', onLoadedMetadata);
      v.removeEventListener('progress', onProgress);
      v.removeEventListener('error', onError);
      v.removeEventListener('play', onPlay);
    };
  }, [hlsSrc, srcWithKey, fallbackToHls]);

  useEffect(() => {
    const itemId = item.id || item.Id || '';
    if (!itemId || !userId) return;

    const url = BASE_URL + '/Users/' + userId + '/Items/' + itemId + '?api_key=' + encodeURIComponent(API_KEY);
    fetch(url)
      .then(r => r.json())
      .then(data => {
        if (data && data.UserData && data.UserData.IsFavorite) {
          setIsLiked(true);
        }
      })
      .catch(() => {});
  }, [item.id, item.Id, userId, API_KEY]);

  useEffect(() => {
    const v = videoRef.current;
    if (v) {
      v.muted = globalMuted;
    }
  }, [globalMuted]);

  useEffect(() => {
    const card = cardRef.current;
    if (!card) return;
    card._initPlayer = initPlayer;
    card._destroyPlayer = destroyPlayer;
    card._video = videoRef.current;
    card._prefetched = false;
    card._prefetchSlotTaken = false;
    card._prefetch = function() {
      if (card._prefetched) return;
      card._prefetched = true;
      const v = videoRef.current;
      if (useTranscode && hlsSrc) {
        if (window.Hls && Hls.isSupported()) {
          const hls = new Hls({
            enableWorker: true,
            lowLatencyMode: false,
            backBufferLength: 30,
            maxBufferLength: 15,
            maxMaxBufferLength: 30,
            startFragPrefetch: true
          });
          hlsRef.current = hls;
          hls.loadSource(hlsSrc);
          hls.attachMedia(v);
          triedTranscodeRef.current = true;
        } else if (v.canPlayType('application/vnd.apple.mpegurl')) {
          v.src = hlsSrc;
          v.preload = 'auto';
          v.load();
          triedTranscodeRef.current = true;
        }
      } else {
        v.src = srcWithKey;
        v.preload = 'auto';
        v.load();
      }
    };
  }, [initPlayer, destroyPlayer, useTranscode, hlsSrc, srcWithKey]);

  return (
    <div
      ref={cardRef}
      className={`sv-card ${isPaused ? 'paused' : ''}`}
      onClick={handleCardClick}
    >
      <div className="sv-top-gradient"></div>
      <div className="sv-bottom-gradient"></div>
      <div
        className={`sv-poster ${isLoading ? '' : 'hidden'}`}
        style={{ backgroundImage: `url("${posterUrl}")` }}
      ></div>
      <div className={`sv-loader ${isLoading ? 'show' : ''}`}></div>
      <video
        ref={videoRef}
        preload="metadata"
        playsInline
        webkit-playsinline="true"
        x-webkit-airplay="deny"
        disablePictureInPicture
        controlsList="nodownload noplaybackrate noremoteplayback"
        loop
        muted={globalMuted}
      />
      <div className="sv-overlay"></div>

      <div className={`sv-center-anim ${showCenterAnim ? 'show' : ''}`}>
        {centerAnimIcon === 'play' ? (
          <svg className="sv-lucide" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><polygon points="5 3 19 12 5 21 5 3"></polygon></svg>
        ) : (
          <svg className="sv-lucide" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><rect x="6" y="4" width="4" height="16"></rect><rect x="14" y="4" width="4" height="16"></rect></svg>
        )}
      </div>

      {showHeart && (
        <div
          className="sv-heart"
          style={{ left: heartPos.x + 'px', top: heartPos.y + 'px' }}
        >
          <svg width="80" height="80" viewBox="0 0 24 24" fill="#ff4757" stroke="#ff4757" strokeWidth="2">
            <path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z"></path>
          </svg>
        </div>
      )}

      <div className="sv-caption">
        <div className="title">{name}</div>
        <div className="meta">{Math.round(duration)}s</div>
      </div>

      <div className="sv-actions">
        <div className="action-item">
          <div
            className={`icon like ${isLiked ? 'liked' : ''}`}
            onClick={(e) => { e.stopPropagation(); toggleLike(); }}
          >
            <Heart className="sv-lucide" fill={isLiked ? 'currentColor' : 'none'} />
          </div>
        </div>
        <div className="action-item">
          <div
            className={`icon mute-icon ${!globalMuted ? 'active' : ''}`}
            onClick={(e) => { e.stopPropagation(); onMuteToggle(); }}
          >
            {globalMuted ? <VolumeX className="sv-lucide" /> : <Volume2 className="sv-lucide" />}
          </div>
        </div>
      </div>

      <div className="sv-controls">
        <div
          className={`sv-progress-container ${isDragging ? 'dragging' : ''} ${touchShow ? 'touch-show' : ''}`}
          onMouseDown={handleProgressDown}
          onTouchStart={(e) => {
            handleTouchStart();
            handleProgressDown(e.touches[0]);
          }}
          onTouchEnd={handleTouchEndProgress}
          onTouchCancel={handleTouchEndProgress}
        >
          <div className="sv-progress-time">{timeDisplay}</div>
          <div className="sv-progress-bg"></div>
          <div className="sv-progress-buffer" style={{ width: buffer + '%' }}></div>
          <div className="sv-progress-played" style={{ width: progress + '%' }}></div>
          <div className="sv-progress-handle" style={{ left: progress + '%' }}></div>
        </div>
      </div>
    </div>
  );
}
