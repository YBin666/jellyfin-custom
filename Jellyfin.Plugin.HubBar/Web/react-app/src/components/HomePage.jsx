import { useState, useEffect } from 'react';
import { getToken, getUserId, BASE_URL } from '../common/auth';

export default function HomePage() {
  const [userName, setUserName] = useState('');
  const [recentItems, setRecentItems] = useState([]);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    fetchUserData();
    fetchRecentItems();
  }, []);

  const fetchUserData = async () => {
    const token = getToken();
    const userId = getUserId();
    if (!token || !userId) return;

    try {
      const response = await fetch(`${BASE_URL}/Users/${userId}?api_key=${encodeURIComponent(token)}`);
      if (response.ok) {
        const user = await response.json();
        setUserName(user.Name || '用户');
      }
    } catch (e) {
      console.error('获取用户信息失败:', e);
    }
  };

  const fetchRecentItems = async () => {
    const token = getToken();
    const userId = getUserId();
    if (!token || !userId) return;

    try {
      const response = await fetch(`${BASE_URL}/Users/${userId}/Items?Recursive=true&Limit=6&SortBy=DateCreated&SortOrder=Descending&IncludeItemTypes=Movie,Series,Video&Fields=BasicSyncInfo,PrimaryImageAspectRatio&api_key=${encodeURIComponent(token)}`);
      if (response.ok) {
        const data = await response.json();
        setRecentItems(data.Items || []);
      }
    } catch (e) {
      console.error('获取最近项目失败:', e);
    } finally {
      setIsLoading(false);
    }
  };

  const getImageUrl = (itemId, width = 300, height = 450) => {
    return `${BASE_URL}/Items/${itemId}/Images/Primary?fillHeight=${height}&fillWidth=${width}&quality=80`;
  };

  return (
    <div className="homepage-container">
      <div className="homepage-header">
        <div className="greeting">
          <h1>欢迎回来，{userName}</h1>
          <p className="subtitle">今天想看点什么？</p>
        </div>
      </div>

      <div className="homepage-content">
        <section className="section">
          <div className="section-header">
            <h2>最近添加</h2>
            <button className="see-all">查看全部</button>
          </div>
          
          {isLoading ? (
            <div className="loading-grid">
              {[1, 2, 3, 4, 5, 6].map((i) => (
                <div key={i} className="loading-card" />
              ))}
            </div>
          ) : recentItems.length > 0 ? (
            <div className="items-grid">
              {recentItems.map((item) => (
                <div key={item.Id} className="item-card">
                  <div className="item-poster">
                    <img
                      src={getImageUrl(item.Id)}
                      alt={item.Name}
                      className="poster-image"
                      loading="lazy"
                    />
                    {item.UserData?.Played && (
                      <div className="watched-badge">✓</div>
                    )}
                  </div>
                  <h3 className="item-title">{item.Name}</h3>
                  <p className="item-type">{item.Type}</p>
                </div>
              ))}
            </div>
          ) : (
            <div className="empty-state">
              <div className="empty-icon">📺</div>
              <p>暂无媒体内容</p>
            </div>
          )}
        </section>

        <section className="section">
          <div className="section-header">
            <h2>快捷入口</h2>
          </div>
          <div className="quick-actions">
            <button className="action-btn" onClick={() => location.hash = '#/shorts'}>
              <span className="action-icon">▶</span>
              <span className="action-text">刷短视频</span>
            </button>
            <button className="action-btn" onClick={() => location.hash = '#/hub-settings'}>
              <span className="action-icon">⚙️</span>
              <span className="action-text">设置</span>
            </button>
          </div>
        </section>
      </div>

      <style>{`
        .homepage-container {
          position: fixed;
          top: 0;
          left: 0;
          width: 100%;
          height: 100vh;
          height: 100dvh;
          background: linear-gradient(180deg, #1a1a2e 0%, #16213e 50%, #0f0f23 100%);
          overflow-y: auto;
          padding-bottom: 80px;
          z-index: 9997;
        }

        .homepage-header {
          padding: 60px 24px 32px;
          background: linear-gradient(180deg, rgba(255,255,255,0.05) 0%, transparent 100%);
        }

        .greeting h1 {
          font-size: 28px;
          font-weight: 600;
          color: #fff;
          margin: 0 0 8px;
          font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
        }

        .subtitle {
          font-size: 16px;
          color: rgba(255, 255, 255, 0.5);
          margin: 0;
        }

        .homepage-content {
          padding: 0 24px;
        }

        .section {
          margin-bottom: 40px;
        }

        .section-header {
          display: flex;
          justify-content: space-between;
          align-items: center;
          margin-bottom: 16px;
        }

        .section-header h2 {
          font-size: 20px;
          font-weight: 600;
          color: #fff;
          margin: 0;
          font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
        }

        .see-all {
          background: transparent;
          border: none;
          color: rgba(255, 255, 255, 0.6);
          font-size: 14px;
          cursor: pointer;
          padding: 4px 8px;
          border-radius: 8px;
          transition: all 0.2s;
        }

        .see-all:hover {
          background: rgba(255, 255, 255, 0.1);
          color: #fff;
        }

        .items-grid {
          display: grid;
          grid-template-columns: repeat(auto-fill, minmax(140px, 1fr));
          gap: 16px;
        }

        .item-card {
          cursor: pointer;
          transition: transform 0.2s, box-shadow 0.2s;
        }

        .item-card:hover {
          transform: translateY(-4px);
        }

        .item-poster {
          position: relative;
          width: 100%;
          padding-bottom: 150%;
          border-radius: 12px;
          overflow: hidden;
          background: rgba(255, 255, 255, 0.05);
          box-shadow: 0 4px 12px rgba(0, 0, 0, 0.3);
        }

        .poster-image {
          position: absolute;
          top: 0;
          left: 0;
          width: 100%;
          height: 100%;
          object-fit: cover;
          transition: transform 0.3s;
        }

        .item-card:hover .poster-image {
          transform: scale(1.05);
        }

        .watched-badge {
          position: absolute;
          top: 8px;
          right: 8px;
          width: 24px;
          height: 24px;
          background: rgba(0, 0, 0, 0.6);
          backdrop-filter: blur(4px);
          border-radius: 50%;
          display: flex;
          align-items: center;
          justify-content: center;
          color: #fff;
          font-size: 12px;
          font-weight: bold;
        }

        .item-title {
          font-size: 13px;
          font-weight: 500;
          color: #fff;
          margin: 8px 0 2px;
          overflow: hidden;
          text-overflow: ellipsis;
          white-space: nowrap;
          font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
        }

        .item-type {
          font-size: 12px;
          color: rgba(255, 255, 255, 0.4);
          margin: 0;
        }

        .loading-grid {
          display: grid;
          grid-template-columns: repeat(auto-fill, minmax(140px, 1fr));
          gap: 16px;
        }

        .loading-card {
          width: 100%;
          padding-bottom: 150%;
          border-radius: 12px;
          background: linear-gradient(90deg, rgba(255,255,255,0.05) 25%, rgba(255,255,255,0.1) 50%, rgba(255,255,255,0.05) 75%);
          background-size: 200% 100%;
          animation: loading 1.5s ease-in-out infinite;
        }

        @keyframes loading {
          0% { background-position: 200% 0; }
          100% { background-position: -200% 0; }
        }

        .empty-state {
          text-align: center;
          padding: 40px;
        }

        .empty-icon {
          font-size: 48px;
          margin-bottom: 16px;
        }

        .empty-state p {
          color: rgba(255, 255, 255, 0.5);
          font-size: 16px;
          margin: 0;
        }

        .quick-actions {
          display: flex;
          gap: 16px;
        }

        .action-btn {
          flex: 1;
          display: flex;
          flex-direction: column;
          align-items: center;
          justify-content: center;
          padding: 24px;
          background: rgba(255, 255, 255, 0.08);
          backdrop-filter: blur(10px);
          border-radius: 16px;
          border: 1px solid rgba(255, 255, 255, 0.1);
          cursor: pointer;
          transition: all 0.2s;
        }

        .action-btn:hover {
          background: rgba(255, 255, 255, 0.15);
          transform: translateY(-2px);
        }

        .action-icon {
          font-size: 28px;
          margin-bottom: 8px;
        }

        .action-text {
          font-size: 14px;
          color: #fff;
          font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
        }

        @media (max-width: 768px) {
          .homepage-header {
            padding: 48px 16px 24px;
          }

          .greeting h1 {
            font-size: 24px;
          }

          .homepage-content {
            padding: 0 16px;
          }

          .items-grid {
            grid-template-columns: repeat(3, 1fr);
            gap: 12px;
          }
        }
      `}</style>
    </div>
  );
}