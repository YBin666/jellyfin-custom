import React from 'react';
import { createRoot } from 'react-dom/client';
import ShortsPage from './shorts/ShortsPage';
import DiyPage from './diy/DiyPage';
import { registerRoute, initInfrastructure, goBackFromCustomRoute } from './common/infrastructure';

function mountShorts() {
  const container = document.createElement('div');
  container.id = 'shortvideo-react-root';
  container.style.cssText = 'position:fixed;top:0;left:0;width:100%;height:100vh;height:100dvh;z-index:9998;background:#000;overflow:hidden;';
  document.body.appendChild(container);

  const reactRoot = document.getElementById('reactRoot');
  if (reactRoot) reactRoot.style.display = 'none';

  document.body.style.paddingTop = '0';
  document.body.style.paddingBottom = '0';
  document.body.style.margin = '0';

  const root = createRoot(container);
  root.render(<ShortsPage />);

  return {
    container: container,
    state: {
      destroy: () => {
        root.unmount();
      }
    }
  };
}

function mountDiy() {
  const container = document.createElement('div');
  container.id = 'diy-react-root';
  container.style.cssText = 'position:fixed;top:0;left:0;width:100%;height:100%;z-index:9998;background:#1a1a1a;';
  document.body.appendChild(container);

  const reactRoot = document.getElementById('reactRoot');
  if (reactRoot) reactRoot.style.display = 'none';

  const root = createRoot(container);
  root.render(<DiyPage onBack={goBackFromCustomRoute} />);

  return {
    container: container,
    state: {
      destroy: () => {
        root.unmount();
      }
    }
  };
}

function initApp() {
  registerRoute({
    name: 'shorts',
    title: '短视频 - Jellyfin',
    show: () => mountShorts()
  });

  registerRoute({
    name: 'diy',
    title: 'DIY - Jellyfin',
    show: () => mountDiy()
  });

  initInfrastructure();
}

if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', initApp);
} else {
  initApp();
}
