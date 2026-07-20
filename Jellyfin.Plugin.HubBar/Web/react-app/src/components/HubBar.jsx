import { useState, useEffect } from 'react';

const navItems = [
  { id: 'home', icon: '🏠', label: '主页' },
  { id: 'shorts', icon: '▶', label: '短视频' },
  { id: 'settings', icon: '⚙️', label: '设置' }
];

export default function HubBar() {
  const [activeTab, setActiveTab] = useState('home');
  const [showAnimation, setShowAnimation] = useState(false);

  useEffect(() => {
    const handleHashChange = () => {
      const hash = location.hash || '#/home';
      if (hash.startsWith('#/shorts')) {
        setActiveTab('shorts');
      } else if (hash.startsWith('#/hub-settings')) {
        setActiveTab('settings');
      } else {
        setActiveTab('home');
      }
    };

    handleHashChange();
    window.addEventListener('hashchange', handleHashChange);
    return () => window.removeEventListener('hashchange', handleHashChange);
  }, []);

  useEffect(() => {
    setShowAnimation(true);
  }, []);

  const handleTabClick = (tabId) => {
    setActiveTab(tabId);
    switch (tabId) {
      case 'home':
        location.hash = '#/home';
        break;
      case 'shorts':
        location.hash = '#/shorts';
        break;
      case 'settings':
        location.hash = '#/hub-settings';
        break;
    }
  };

  return (
    <div
      className="hubbar-container"
      style={{
        transform: showAnimation ? 'translateY(0)' : 'translateY(100%)',
        opacity: showAnimation ? 1 : 0
      }}
    >
      <div className="hubbar-wrapper">
        {navItems.map((item) => (
          <button
            key={item.id}
            className={`hubbar-item ${activeTab === item.id ? 'active' : ''}`}
            onClick={() => handleTabClick(item.id)}
          >
            <span className="hubbar-icon">{item.icon}</span>
            <span className="hubbar-label">{item.label}</span>
            {activeTab === item.id && (
              <div className="hubbar-indicator" />
            )}
          </button>
        ))}
      </div>
      <style>{`
        .hubbar-container {
          position: fixed;
          bottom: 0;
          left: 0;
          right: 0;
          z-index: 9999;
          padding-bottom: env(safe-area-inset-bottom);
          transition: transform 0.3s cubic-bezier(0.4, 0, 0.2, 1), opacity 0.3s ease;
        }

        .hubbar-wrapper {
          display: flex;
          justify-content: space-around;
          align-items: center;
          height: 60px;
          margin: 0 16px 12px;
          padding: 0 8px;
          background: rgba(30, 30, 30, 0.75);
          backdrop-filter: blur(20px) saturate(180%);
          -webkit-backdrop-filter: blur(20px) saturate(180%);
          border-radius: 24px;
          border: 1px solid rgba(255, 255, 255, 0.1);
          box-shadow: 
            0 8px 32px rgba(0, 0, 0, 0.3),
            0 2px 8px rgba(0, 0, 0, 0.15),
            inset 0 1px 0 rgba(255, 255, 255, 0.05);
        }

        .hubbar-item {
          position: relative;
          display: flex;
          flex-direction: column;
          align-items: center;
          justify-content: center;
          flex: 1;
          height: 100%;
          background: transparent;
          border: none;
          cursor: pointer;
          transition: all 0.2s ease;
          outline: none;
        }

        .hubbar-item:hover {
          transform: translateY(-2px);
        }

        .hubbar-item:active {
          transform: translateY(0);
        }

        .hubbar-icon {
          font-size: 22px;
          line-height: 1;
          color: rgba(255, 255, 255, 0.55);
          transition: all 0.2s ease;
        }

        .hubbar-item.active .hubbar-icon {
          color: rgba(255, 255, 255, 0.95);
          transform: scale(1.15);
        }

        .hubbar-label {
          font-size: 11px;
          color: rgba(255, 255, 255, 0.4);
          margin-top: 2px;
          font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
          transition: all 0.2s ease;
        }

        .hubbar-item.active .hubbar-label {
          color: rgba(255, 255, 255, 0.85);
          font-weight: 500;
        }

        .hubbar-indicator {
          position: absolute;
          bottom: 8px;
          width: 4px;
          height: 4px;
          background: rgba(255, 255, 255, 0.8);
          border-radius: 50%;
          animation: indicatorPulse 2s ease-in-out infinite;
        }

        @keyframes indicatorPulse {
          0%, 100% {
            opacity: 0.8;
            transform: scale(1);
          }
          50% {
            opacity: 0.4;
            transform: scale(0.8);
          }
        }

        @media (max-width: 768px) {
          .hubbar-wrapper {
            margin: 0 12px 8px;
            height: 56px;
            border-radius: 20px;
          }

          .hubbar-icon {
            font-size: 20px;
          }

          .hubbar-label {
            font-size: 10px;
          }
        }
      `}</style>
    </div>
  );
}