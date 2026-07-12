import { useState, useRef, useEffect, useCallback } from 'react';
import VideoCard from './VideoCard';
import FavoritesPanel from './FavoritesPanel';
import { getNextBatch, getStreamUrl } from '../common/api';
import './shorts.css';

export default function ShortsPage() {
  const feedRef = useRef(null);
  const [items, setItems] = useState([]);
  const [firstBatchLoaded, setFirstBatchLoaded] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [globalMuted, setGlobalMuted] = useState(true);
  const [showFavorites, setShowFavorites] = useState(false);
  const [activeTab, setActiveTab] = useState('home');
  const refreshLastClickRef = useRef(0);
  const loadBatchRef = useRef(null);
  const playVisibleRef = useRef(null);

  const loadBatch = useCallback(() => {
    if (isLoading) return;
    setIsLoading(true);
    getNextBatch()
      .then(newItems => {
        setIsLoading(false);
        if (!newItems || newItems.length === 0) return;
        setItems(prev => [...prev, ...newItems]);
        if (!firstBatchLoaded) {
          setFirstBatchLoaded(true);
          setTimeout(() => playVisibleRef.current?.(), 200);
        }
      })
      .catch(e => {
        setIsLoading(false);
        console.error('[ShortsPage] loadBatch error:', e);
      });
  }, [isLoading, firstBatchLoaded]);

  const playVisible = useCallback(() => {
    const feed = feedRef.current;
    if (!feed) return;
    const cards = feed.querySelectorAll('.sv-card');
    const center = feed.scrollTop + feed.clientHeight / 2;
    let visibleIndex = -1;
    for (let i = 0; i < cards.length; i++) {
      const top = cards[i].offsetTop;
      const bottom = top + cards[i].offsetHeight;
      if (center >= top && center < bottom) {
        visibleIndex = i;
        break;
      }
    }
    cards.forEach((card, i) => {
      const v = card._video;
      const isVisible = i === visibleIndex;
      if (isVisible) {
        if (card._initPlayer) card._initPlayer();
        if (v) v.play().catch(() => {});
      } else {
        if (v) v.pause();
        if (Math.abs(i - visibleIndex) > 3 && card._destroyPlayer) {
          card._destroyPlayer();
        }
      }
    });
    if (visibleIndex >= 0) {
      for (let j = 1; j <= 3; j++) {
        const nextIdx = visibleIndex + j;
        if (nextIdx < cards.length && cards[nextIdx]._prefetch) {
          cards[nextIdx]._prefetch();
        }
      }
      const remaining = cards.length - visibleIndex - 1;
      if (remaining <= 3 && !isLoading) {
        loadBatchRef.current?.();
      }
    }
  }, [isLoading]);

  const forceRefresh = useCallback(() => {
    setItems([]);
    setFirstBatchLoaded(false);
    setIsLoading(false);
    if (feedRef.current) {
      feedRef.current.scrollTop = 0;
    }
    setTimeout(() => loadBatchRef.current?.(), 50);
  }, []);

  loadBatchRef.current = loadBatch;
  playVisibleRef.current = playVisible;

  const handleScroll = useCallback(() => {
    let snapTimer;
    clearTimeout(snapTimer);
    snapTimer = setTimeout(() => {
      playVisible();
      const feed = feedRef.current;
      if (feed && feed.scrollTop + feed.clientHeight > feed.scrollHeight - feed.clientHeight) {
        loadBatch();
      }
    }, 120);
  }, [playVisible, loadBatch]);

  const handleHomeClick = useCallback((e) => {
    e.preventDefault();
    window.location.hash = '#/home';
  }, []);

  const handleRefreshClick = useCallback((e) => {
    e.preventDefault();
    const now = Date.now();
    if (now - refreshLastClickRef.current < 300) {
      forceRefresh();
      refreshLastClickRef.current = 0;
      return;
    }
    refreshLastClickRef.current = now;
    const feed = feedRef.current;
    if (!feed) return;
    const cards = feed.querySelectorAll('.sv-card');
    const center = feed.scrollTop + feed.clientHeight / 2;
    for (let i = 0; i < cards.length; i++) {
      const top = cards[i].offsetTop;
      const bottom = top + cards[i].offsetHeight;
      if (center >= top && center < bottom) {
        if (cards[i]._destroyPlayer) {
          cards[i]._destroyPlayer();
          setTimeout(() => {
            if (cards[i]._initPlayer) cards[i]._initPlayer();
            const v = cards[i]._video;
            if (v) v.play().catch(() => {});
          }, 100);
        }
        break;
      }
    }
  }, [forceRefresh]);

  const handleFavoritesClick = useCallback((e) => {
    e.preventDefault();
    setShowFavorites(prev => {
      if (prev) {
        setActiveTab('home');
      } else {
        setActiveTab('favorites');
      }
      return !prev;
    });
  }, []);

  const handleMuteToggle = useCallback(() => {
    setGlobalMuted(prev => !prev);
  }, []);

  const handlePlayFavoriteItem = useCallback((item) => {
    setShowFavorites(false);
    setActiveTab('home');
    const newItem = {
      id: item.Id,
      name: item.Name,
      streamUrl: getStreamUrl(item.Id),
      durationSeconds: item.RunTimeTicks ? item.RunTimeTicks / 10000000 : 0,
      videoCodec: '',
      audioCodec: '',
      container: ''
    };
    setItems([newItem]);
    setFirstBatchLoaded(true);
    if (feedRef.current) {
      feedRef.current.scrollTop = 0;
    }
    setTimeout(() => playVisible(), 200);
  }, [playVisible]);

  useEffect(() => {
    loadBatch();
  }, []);

  useEffect(() => {
    const handleKeydown = (e) => {
      if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
        e.preventDefault();
        if (feedRef.current) {
          feedRef.current.scrollBy({
            top: e.key === 'ArrowDown' ? feedRef.current.clientHeight : -feedRef.current.clientHeight,
            behavior: 'smooth'
          });
        }
      }
    };
    document.addEventListener('keydown', handleKeydown);
    return () => document.removeEventListener('keydown', handleKeydown);
  }, []);

  return (
    <div className="sv-shorts-container">
      <div
        ref={feedRef}
        className="sv-feed"
        onScroll={handleScroll}
      >
        {!firstBatchLoaded && items.length === 0 && (
          <div className="sv-empty">加载中...</div>
        )}
        {items.map(item => (
          <VideoCard
            key={item.id || item.Id}
            item={item}
            globalMuted={globalMuted}
            onMuteToggle={handleMuteToggle}
          />
        ))}
      </div>

      <div className="sv-hub">
        <button
          className={`hub-btn ${activeTab === 'home' ? 'active' : ''}`}
          onClick={handleHomeClick}
        >
          首页
        </button>
        <button
          className={`hub-btn ${activeTab === 'refresh' ? 'active' : ''}`}
          onClick={handleRefreshClick}
        >
          刷新
        </button>
        <button
          className={`hub-btn ${activeTab === 'favorites' ? 'active' : ''}`}
          onClick={handleFavoritesClick}
        >
          收藏
        </button>
      </div>

      <FavoritesPanel
        show={showFavorites}
        onBack={() => { setShowFavorites(false); setActiveTab('home'); }}
        onPlayItem={handlePlayFavoriteItem}
      />
    </div>
  );
}
