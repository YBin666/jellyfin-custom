import { ChevronRight } from 'lucide-react';
import { useState, useEffect, useRef } from 'react';
import { getFavorites, getThumbnailUrl } from '../common/api';

export default function FavoritesPanel({ show, onBack, onPlayItem }) {
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(false);
  const panelRef = useRef(null);
  const touchStartX = useRef(0);
  const touchDelta = useRef(0);

  useEffect(() => {
    if (show) {
      loadFavorites();
    }
  }, [show]);

  const loadFavorites = () => {
    setLoading(true);
    getFavorites().then(data => {
      setItems(data.Items || []);
      setLoading(false);
    });
  };

  const handleTouchStart = (e) => {
    touchStartX.current = e.touches[0].clientX;
    touchDelta.current = 0;
  };

  const handleTouchMove = (e) => {
    const delta = e.touches[0].clientX - touchStartX.current;
    if (delta > 0 && panelRef.current) {
      touchDelta.current = delta;
      panelRef.current.style.transition = 'none';
      panelRef.current.style.transform = `translateX(${delta}px)`;
    }
  };

  const handleTouchEnd = () => {
    if (panelRef.current) {
      panelRef.current.style.transition = '';
      panelRef.current.style.transform = '';
    }
    if (touchDelta.current > 80) {
      onBack();
    }
    touchDelta.current = 0;
  };

  return (
    <>
      <div
        className={`sv-favorites-backdrop ${show ? 'show' : ''}`}
        onClick={onBack}
        onTouchStart={(e) => { if (show) { e.preventDefault(); onBack(); } }}
      />
      <div
        ref={panelRef}
        className={`sv-favorites-panel ${show ? 'show' : ''}`}
        onTouchStart={handleTouchStart}
        onTouchMove={handleTouchMove}
        onTouchEnd={handleTouchEnd}
      >
        <div className="sv-favorites-header">
          <button className="sv-favorites-back" onClick={onBack}>
            <ChevronRight className="sv-lucide" />
          </button>
          <div className="sv-favorites-title">收藏</div>
          <div style={{ width: '40px' }}></div>
        </div>
        <div className="sv-favorites-grid">
          {loading ? (
            <div className="sv-favorites-empty">加载中...</div>
          ) : items.length === 0 ? (
            <div className="sv-favorites-empty">暂无收藏内容</div>
          ) : (
            items.map(item => (
              <div
                key={item.Id}
                className="sv-favorites-item"
                onClick={() => onPlayItem(item)}
              >
                <img
                  src={getThumbnailUrl(item.Id)}
                  alt={item.Name || ''}
                />
                <div className="name">{item.Name || ''}</div>
              </div>
            ))
          )}
        </div>
      </div>
    </>
  );
}
