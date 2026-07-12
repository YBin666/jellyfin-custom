import React from 'react';
import { createRoot } from 'react-dom/client';
import ShortsPage from './shorts/ShortsPage';
import { registerRoute, initInfrastructure, goBackFromCustomRoute } from './common/infrastructure';

function mountShorts() {
  const container = document.createElement('div');
  container.id = 'shortvideo-react-root';
  document.body.appendChild(container);

  const reactRoot = document.getElementById('reactRoot');
  if (reactRoot) reactRoot.style.display = 'none';

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

function initShortVideo() {
  registerRoute({
    name: 'shorts',
    title: '短视频 - Jellyfin',
    show: function() {
      return mountShorts();
    }
  });
  initInfrastructure();
}

if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', initShortVideo);
} else {
  initShortVideo();
}
