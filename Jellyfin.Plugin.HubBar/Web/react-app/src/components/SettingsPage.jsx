import { useState } from 'react';
import { getToken, getUserId, BASE_URL } from '../common/auth';

export default function SettingsPage() {
  const [settings, setSettings] = useState({
    enableHubBar: true,
    enableHomeButton: true,
    enableShortVideoButton: true,
    enableSettingsButton: true,
    hubBarColor: 'dark'
  });
  const [isSaving, setIsSaving] = useState(false);
  const [saveMessage, setSaveMessage] = useState('');

  const toggleSetting = (key) => {
    setSettings(prev => ({ ...prev, [key]: !prev[key] }));
  };

  const handleColorChange = (color) => {
    setSettings(prev => ({ ...prev, hubBarColor: color }));
  };

  const saveSettings = async () => {
    setIsSaving(true);
    try {
      const token = getToken();
      await fetch(`${BASE_URL}/HubBar/Config?api_key=${encodeURIComponent(token)}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(settings)
      });
      setSaveMessage('设置已保存');
      setTimeout(() => setSaveMessage(''), 2000);
    } catch (e) {
      setSaveMessage('保存失败');
      setTimeout(() => setSaveMessage(''), 2000);
    } finally {
      setIsSaving(false);
    }
  };

  const resetSettings = () => {
    setSettings({
      enableHubBar: true,
      enableHomeButton: true,
      enableShortVideoButton: true,
      enableSettingsButton: true,
      hubBarColor: 'dark'
    });
  };

  return (
    <div className="settingspage-container">
      <div className="settingspage-header">
        <button className="back-btn" onClick={() => location.hash = '#/home'}>
          ←
        </button>
        <h1>设置</h1>
        <div className="header-spacer" />
      </div>

      <div className="settingspage-content">
        <div className="save-status">{saveMessage}</div>

        <section className="setting-section">
          <h2 className="section-title">HubBar 导航栏</h2>
          
          <div className="setting-item">
            <div className="setting-info">
              <span className="setting-label">启用 HubBar</span>
              <span className="setting-desc">在页面底部显示 iOS 风格导航栏</span>
            </div>
            <button
              className={`toggle ${settings.enableHubBar ? 'active' : ''}`}
              onClick={() => toggleSetting('enableHubBar')}
            >
              <div className="toggle-thumb" />
            </button>
          </div>

          <div className="setting-item">
            <div className="setting-info">
              <span className="setting-label">主页按钮</span>
              <span className="setting-desc">在导航栏显示主页入口</span>
            </div>
            <button
              className={`toggle ${settings.enableHomeButton ? 'active' : ''}`}
              onClick={() => toggleSetting('enableHomeButton')}
            >
              <div className="toggle-thumb" />
            </button>
          </div>

          <div className="setting-item">
            <div className="setting-info">
              <span className="setting-label">短视频按钮</span>
              <span className="setting-desc">在导航栏显示短视频入口</span>
            </div>
            <button
              className={`toggle ${settings.enableShortVideoButton ? 'active' : ''}`}
              onClick={() => toggleSetting('enableShortVideoButton')}
            >
              <div className="toggle-thumb" />
            </button>
          </div>

          <div className="setting-item">
            <div className="setting-info">
              <span className="setting-label">设置按钮</span>
              <span className="setting-desc">在导航栏显示设置入口</span>
            </div>
            <button
              className={`toggle ${settings.enableSettingsButton ? 'active' : ''}`}
              onClick={() => toggleSetting('enableSettingsButton')}
            >
              <div className="toggle-thumb" />
            </button>
          </div>
        </section>

        <section className="setting-section">
          <h2 className="section-title">主题颜色</h2>
          
          <div className="color-options">
            <button
              className={`color-option ${settings.hubBarColor === 'dark' ? 'active' : ''}`}
              onClick={() => handleColorChange('dark')}
            >
              <div className="color-preview dark" />
              <span className="color-label">深色</span>
            </button>
            <button
              className={`color-option ${settings.hubBarColor === 'light' ? 'active' : ''}`}
              onClick={() => handleColorChange('light')}
            >
              <div className="color-preview light" />
              <span className="color-label">浅色</span>
            </button>
            <button
              className={`color-option ${settings.hubBarColor === 'auto' ? 'active' : ''}`}
              onClick={() => handleColorChange('auto')}
            >
              <div className="color-preview auto" />
              <span className="color-label">跟随系统</span>
            </button>
          </div>
        </section>

        <section className="setting-section">
          <h2 className="section-title">关于</h2>
          
          <div className="about-card">
            <div className="about-icon">🎨</div>
            <div className="about-info">
              <h3>HubBar 插件</h3>
              <p className="about-version">版本 1.0.0</p>
              <p className="about-desc">为 Jellyfin 添加 iOS 风格的毛玻璃底部导航栏</p>
            </div>
          </div>
        </section>

        <div className="action-buttons">
          <button className="btn-secondary" onClick={resetSettings}>
            重置为默认
          </button>
          <button className="btn-primary" onClick={saveSettings} disabled={isSaving}>
            {isSaving ? '保存中...' : '保存设置'}
          </button>
        </div>
      </div>

      <style>{`
        .settingspage-container {
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

        .settingspage-header {
          display: flex;
          align-items: center;
          justify-content: space-between;
          padding: 60px 24px 16px;
          background: linear-gradient(180deg, rgba(255,255,255,0.05) 0%, transparent 100%);
          position: sticky;
          top: 0;
          z-index: 10;
        }

        .back-btn {
          width: 40px;
          height: 40px;
          background: rgba(255, 255, 255, 0.1);
          backdrop-filter: blur(10px);
          border: none;
          border-radius: 12px;
          color: #fff;
          font-size: 20px;
          cursor: pointer;
          display: flex;
          align-items: center;
          justify-content: center;
          transition: all 0.2s;
        }

        .back-btn:hover {
          background: rgba(255, 255, 255, 0.2);
        }

        .settingspage-header h1 {
          font-size: 20px;
          font-weight: 600;
          color: #fff;
          margin: 0;
          font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
        }

        .header-spacer {
          width: 40px;
        }

        .settingspage-content {
          padding: 16px 24px;
        }

        .save-status {
          text-align: center;
          padding: 12px;
          margin-bottom: 16px;
          background: rgba(76, 175, 80, 0.15);
          color: #4caf50;
          border-radius: 12px;
          font-size: 14px;
          opacity: 0;
          transition: opacity 0.2s;
        }

        .save-status:not(:empty) {
          opacity: 1;
        }

        .setting-section {
          background: rgba(255, 255, 255, 0.05);
          backdrop-filter: blur(10px);
          border-radius: 16px;
          padding: 20px;
          margin-bottom: 16px;
          border: 1px solid rgba(255, 255, 255, 0.05);
        }

        .section-title {
          font-size: 13px;
          font-weight: 600;
          color: rgba(255, 255, 255, 0.4);
          margin: 0 0 16px;
          text-transform: uppercase;
          letter-spacing: 0.5px;
          font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
        }

        .setting-item {
          display: flex;
          align-items: center;
          justify-content: space-between;
          padding: 16px 0;
          border-bottom: 1px solid rgba(255, 255, 255, 0.05);
        }

        .setting-item:last-child {
          border-bottom: none;
        }

        .setting-info {
          display: flex;
          flex-direction: column;
        }

        .setting-label {
          font-size: 16px;
          font-weight: 500;
          color: #fff;
          font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
        }

        .setting-desc {
          font-size: 13px;
          color: rgba(255, 255, 255, 0.4);
          margin-top: 2px;
        }

        .toggle {
          width: 52px;
          height: 32px;
          background: rgba(255, 255, 255, 0.2);
          border-radius: 16px;
          border: none;
          cursor: pointer;
          position: relative;
          transition: all 0.2s;
        }

        .toggle.active {
          background: #4caf50;
        }

        .toggle-thumb {
          position: absolute;
          top: 3px;
          left: 3px;
          width: 26px;
          height: 26px;
          background: #fff;
          border-radius: 50%;
          transition: all 0.2s;
          box-shadow: 0 2px 4px rgba(0, 0, 0, 0.2);
        }

        .toggle.active .toggle-thumb {
          left: 23px;
        }

        .color-options {
          display: flex;
          gap: 16px;
        }

        .color-option {
          flex: 1;
          display: flex;
          flex-direction: column;
          align-items: center;
          padding: 16px;
          background: rgba(255, 255, 255, 0.05);
          border-radius: 12px;
          border: 2px solid transparent;
          cursor: pointer;
          transition: all 0.2s;
        }

        .color-option.active {
          border-color: #4caf50;
          background: rgba(76, 175, 80, 0.1);
        }

        .color-option:hover {
          background: rgba(255, 255, 255, 0.1);
        }

        .color-preview {
          width: 40px;
          height: 40px;
          border-radius: 50%;
          margin-bottom: 8px;
        }

        .color-preview.dark {
          background: linear-gradient(135deg, #333, #1a1a1a);
        }

        .color-preview.light {
          background: linear-gradient(135deg, #f5f5f5, #e0e0e0);
        }

        .color-preview.auto {
          background: linear-gradient(135deg, #333 50%, #f5f5f5 50%);
        }

        .color-label {
          font-size: 12px;
          color: rgba(255, 255, 255, 0.7);
          font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
        }

        .about-card {
          display: flex;
          align-items: center;
          padding: 16px;
          background: rgba(255, 255, 255, 0.05);
          border-radius: 12px;
        }

        .about-icon {
          font-size: 32px;
          margin-right: 16px;
        }

        .about-info {
          display: flex;
          flex-direction: column;
        }

        .about-info h3 {
          font-size: 16px;
          font-weight: 600;
          color: #fff;
          margin: 0;
          font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
        }

        .about-version {
          font-size: 12px;
          color: rgba(255, 255, 255, 0.4);
          margin: 4px 0 2px;
        }

        .about-desc {
          font-size: 13px;
          color: rgba(255, 255, 255, 0.5);
          margin: 0;
        }

        .action-buttons {
          display: flex;
          gap: 16px;
          margin-top: 24px;
          padding-bottom: 24px;
        }

        .btn-secondary,
        .btn-primary {
          flex: 1;
          padding: 14px 24px;
          border-radius: 12px;
          font-size: 15px;
          font-weight: 500;
          cursor: pointer;
          transition: all 0.2s;
          font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
        }

        .btn-secondary {
          background: rgba(255, 255, 255, 0.1);
          border: 1px solid rgba(255, 255, 255, 0.1);
          color: #fff;
        }

        .btn-secondary:hover {
          background: rgba(255, 255, 255, 0.15);
        }

        .btn-primary {
          background: #4caf50;
          border: none;
          color: #fff;
        }

        .btn-primary:hover {
          background: #43a047;
        }

        .btn-primary:disabled {
          opacity: 0.5;
          cursor: not-allowed;
        }

        @media (max-width: 768px) {
          .settingspage-header {
            padding: 48px 16px 12px;
          }

          .settingspage-content {
            padding: 12px 16px;
          }

          .setting-section {
            padding: 16px;
          }

          .action-buttons {
            flex-direction: column;
          }
        }
      `}</style>
    </div>
  );
}