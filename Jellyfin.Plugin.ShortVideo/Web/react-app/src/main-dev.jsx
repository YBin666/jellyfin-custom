import React, { useState, useEffect } from 'react';
import { createRoot } from 'react-dom/client';
import ShortsPage from './shorts/ShortsPage';

const DEV_STORAGE_KEY = 'jellyfin_dev_credentials';
const TOOLBAR_COLLAPSED_KEY = 'jellyfin_dev_toolbar_collapsed';

function loadToolbarCollapsed() {
  return localStorage.getItem(TOOLBAR_COLLAPSED_KEY) === 'true';
}

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
    const tokenInput = document.getElementById('tokenInput');
    const userIdInput = document.getElementById('userIdInput');
    const saveBtn = document.getElementById('saveBtn');
    const reloadBtn = document.getElementById('reloadBtn');
    const statusEl = document.getElementById('status');
    const devToolbar = document.getElementById('devToolbar');
    const devRoot = document.getElementById('devRoot');
    const collapseToolbarBtn = document.getElementById('collapseToolbarBtn');
    const expandToolbarBtn = document.getElementById('expandToolbarBtn');

    if (!tokenInput || !userIdInput || !saveBtn || !reloadBtn) return;

    tokenInput.value = creds.token || '';
    userIdInput.value = creds.userId || '';

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

    saveBtn.addEventListener('click', onSave);
    reloadBtn.addEventListener('click', onReload);

    let toolbarCollapsed = loadToolbarCollapsed();
    const applyToolbarState = () => {
      if (toolbarCollapsed) {
        devToolbar.classList.add('collapsed');
        devRoot.classList.add('fullscreen');
        expandToolbarBtn.classList.add('visible');
      } else {
        devToolbar.classList.remove('collapsed');
        devRoot.classList.remove('fullscreen');
        expandToolbarBtn.classList.remove('visible');
      }
    };
    const onCollapseToolbar = () => {
      toolbarCollapsed = true;
      localStorage.setItem(TOOLBAR_COLLAPSED_KEY, 'true');
      applyToolbarState();
    };
    const onExpandToolbar = () => {
      toolbarCollapsed = false;
      localStorage.setItem(TOOLBAR_COLLAPSED_KEY, 'false');
      applyToolbarState();
    };
    if (devToolbar && devRoot && collapseToolbarBtn && expandToolbarBtn) {
      applyToolbarState();
      collapseToolbarBtn.addEventListener('click', onCollapseToolbar);
      expandToolbarBtn.addEventListener('click', onExpandToolbar);
    }

    const updateStatus = () => {
      statusEl.textContent = status;
      statusEl.className = 'dev-status ' + (creds.token && creds.userId ? 'ok' : 'err');
    };
    updateStatus();

    return () => {
      saveBtn.removeEventListener('click', onSave);
      reloadBtn.removeEventListener('click', onReload);
      if (collapseToolbarBtn) collapseToolbarBtn.removeEventListener('click', onCollapseToolbar);
      if (expandToolbarBtn) expandToolbarBtn.removeEventListener('click', onExpandToolbar);
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
      <ShortsPage />
    </React.Fragment>
  );
}

const root = createRoot(document.getElementById('devRoot'));
root.render(<DevApp />);
