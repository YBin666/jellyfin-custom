import React, { useState, useEffect, useRef } from 'react';
import { createRoot } from 'react-dom/client';
import ShortsPage from './shorts/ShortsPage';
import DiyPage from './diy/DiyPage';
import { goBackFromCustomRoute } from './common/infrastructure';

const DEV_STORAGE_KEY = 'jellyfin_dev_credentials';

function loadDevCredentials() {
  try {
    const raw = localStorage.getItem(DEV_STORAGE_KEY);
    if (raw) return JSON.parse(raw);
  } catch (e) {}
  return { token: '', userId: '' };
}

function saveDevCredentials(creds) {
  localStorage.setItem(DEV_STORAGE_KEY, JSON.stringify(creds));
}

function DevApp() {
  const [page, setPage] = useState('shorts');
  const [creds, setCreds] = useState(loadDevCredentials());
  const [status, setStatus] = useState('未连接');
  const [reloadKey, setReloadKey] = useState(0);
  const [ready, setReady] = useState(false);

  useEffect(() => {
    if (creds.token && creds.userId) {
      setStatus('已连接');
      setReady(true);
    } else {
      setStatus('请输入凭据');
      setReady(false);
    }
  }, [creds]);

  useEffect(() => {
    const pageSelect = document.getElementById('pageSelect');
    const tokenInput = document.getElementById('tokenInput');
    const userIdInput = document.getElementById('userIdInput');
    const saveBtn = document.getElementById('saveBtn');
    const reloadBtn = document.getElementById('reloadBtn');
    const statusEl = document.getElementById('status');

    if (!pageSelect || !tokenInput || !userIdInput || !saveBtn || !reloadBtn) return;

    tokenInput.value = creds.token || '';
    userIdInput.value = creds.userId || '';

    const onPageChange = (e) => setPage(e.target.value);
    const onSave = () => {
      const newCreds = {
        token: tokenInput.value.trim(),
        userId: userIdInput.value.trim()
      };
      saveDevCredentials(newCreds);
      setCreds(newCreds);
      setReloadKey(k => k + 1);
    };
    const onReload = () => setReloadKey(k => k + 1);

    pageSelect.addEventListener('change', onPageChange);
    saveBtn.addEventListener('click', onSave);
    reloadBtn.addEventListener('click', onReload);

    const updateStatus = () => {
      statusEl.textContent = status;
      statusEl.className = 'dev-status ' + (creds.token && creds.userId ? 'ok' : 'err');
    };
    updateStatus();

    return () => {
      pageSelect.removeEventListener('change', onPageChange);
      saveBtn.removeEventListener('click', onSave);
      reloadBtn.removeEventListener('click', onReload);
    };
  }, [status, creds]);

  if (!ready) {
    return (
      <div style={{
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        height: '100%', color: '#888', fontSize: 14, textAlign: 'center',
        padding: 20
      }}>
        <div>
          <div style={{ marginBottom: 12 }}>请先在顶部工具栏输入 Token 和 UserId，然后点击"保存"</div>
          <div style={{ opacity: 0.6, fontSize: 12 }}>
            Token 获取方法：浏览器登录 Jellyfin 后，F12 打开开发者工具<br/>
            Application → Local Storage → jellyfin_credentials → Servers[0].AccessToken
          </div>
        </div>
      </div>
    );
  }

  return (
    <React.Fragment key={reloadKey}>
      {page === 'shorts' ? <ShortsPage /> : <DiyPage onBack={goBackFromCustomRoute} />}
    </React.Fragment>
  );
}

const root = createRoot(document.getElementById('devRoot'));
root.render(<DevApp />);
