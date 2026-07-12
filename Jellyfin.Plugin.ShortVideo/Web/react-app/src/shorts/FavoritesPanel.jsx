import { ChevronRight } from 'lucide-react';
import { useState, useEffect } from 'react';
import { getToken, getUserId, BASE_URL, apiUrl } from '../common/auth';

export default function FavoritesPanel({ show, onBack, onPlayItem }) {
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (show) {
      loadFavorites();
    }
  }, [show]);

  const loadFavorites = () => {
    const userId = getUserId();
    if (!userId) return;

    setLoading(true);
    const url = BASE_URL + '/Users/' + userId + '/Items?IsFavorite=true&Recursive=true&IncludeItemTypes=Video&Fields=BasicSyncInfo,PrimaryImageAspectRatio&api_key=' + encodeURIComponent(getToken());

    fetch(url)
      .then(r => r.json())
      .then(data => {
        setItems(data.Items || []);
        setLoading(false);
      })
      .catch(() => {
        setLoading(false);
      });
  };

  return (
    <div className={`sv-favorites-panel ${show ? 'show' : ''}`}>
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
                src={BASE_URL + '/Items/' + item.Id + '/Images/Primary?fillHeight=320&fillWidth=180&quality=80'}
                alt={item.Name || ''}
              />
              <div className="name">{item.Name || ''}</div>
            </div>
          ))
        )}
      </div>
    </div>
  );
}
