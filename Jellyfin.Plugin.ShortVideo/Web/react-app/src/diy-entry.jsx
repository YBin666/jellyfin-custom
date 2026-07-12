import React from 'react';
import { createRoot } from 'react-dom/client';
import DiyPage from './diy/DiyPage';
import { registerRoute, goBackFromCustomRoute } from './common/infrastructure';

function mountDiy() {
  const container = document.createElement('div');
  container.id = 'diy-react-root';
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

function initDiy() {
  if (!window.__svRegisterRoute) {
    setTimeout(initDiy, 100);
    return;
  }
  registerRoute({
    name: 'diy',
    title: 'DIY - Jellyfin',
    show: function() {
      return mountDiy();
    }
  });
}

if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', initDiy);
} else {
  initDiy();
}
