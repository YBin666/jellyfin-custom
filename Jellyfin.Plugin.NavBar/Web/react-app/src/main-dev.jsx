import React from 'react';
import { createRoot } from 'react-dom/client';
import NavBar from './components/NavBar';
import HomePage from './components/HomePage';
import SettingsPage from './components/SettingsPage';
import { registerRoute, initInfrastructure } from './common/infrastructure';

function mountHome() {
  const container = document.createElement('div');
  container.id = 'navbar-home-root';
  container.style.cssText = 'position:fixed;top:0;left:0;width:100%;height:100vh;height:100dvh;z-index:9997;overflow:hidden;';
  document.body.appendChild(container);

  const reactRoot = document.getElementById('reactRoot');
  if (reactRoot) reactRoot.style.display = 'none';

  document.body.style.paddingTop = '0';
  document.body.style.paddingBottom = '0';
  document.body.style.margin = '0';

  const root = createRoot(container);
  root.render(<HomePage />);

  return {
    container: container,
    state: {
      destroy: () => {
        root.unmount();
      }
    }
  };
}

function mountSettings() {
  const container = document.createElement('div');
  container.id = 'navbar-settings-root';
  container.style.cssText = 'position:fixed;top:0;left:0;width:100%;height:100vh;height:100dvh;z-index:9997;overflow:hidden;';
  document.body.appendChild(container);

  const reactRoot = document.getElementById('reactRoot');
  if (reactRoot) reactRoot.style.display = 'none';

  document.body.style.paddingTop = '0';
  document.body.style.paddingBottom = '0';
  document.body.style.margin = '0';

  const root = createRoot(container);
  root.render(<SettingsPage />);

  return {
    container: container,
    state: {
      destroy: () => {
        root.unmount();
      }
    }
  };
}

function createNavBar(container) {
  const root = createRoot(container);
  root.render(<NavBar />);
}

function initApp() {
  registerRoute({
    name: 'home',
    title: '主页 - Jellyfin',
    show: () => mountHome()
  });

  registerRoute({
    name: 'nav-settings',
    title: '设置 - Jellyfin',
    show: () => mountSettings()
  });

  initInfrastructure(createNavBar);
}

if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', initApp);
} else {
  initApp();
}