const customRoutes = [];
let originalTitle = document.title;
let previousHash = null;
let isNavigatingBack = false;
let hubBarInstance = null;

function matchRoute(name) {
  return new RegExp('^#/' + name + '([/?]|$)').test(location.hash || '');
}

function isCustomRoute() {
  return customRoutes.some(r => matchRoute(r.name));
}

function isAllowedRoute() {
  return /^#\/(home|list|shorts|hub-settings)([\/?]|$)/.test(location.hash || '');
}

export function registerRoute(cfg) {
  customRoutes.push({
    name: cfg.name,
    title: cfg.title,
    show: cfg.show,
    hide: cfg.hide || null,
    state: null,
    container: null
  });
}

function getActiveRoute() {
  for (let i = 0; i < customRoutes.length; i++) {
    if (matchRoute(customRoutes[i].name)) return customRoutes[i];
  }
  return null;
}

function silentHideRoute(r) {
  if (!r || !r.container) return;
  if (r.hide) {
    r.hide(r);
  } else {
    if (r.state && r.state.destroy) {
      try { r.state.destroy(); } catch (e) {}
    }
    r.state = null;
    r.container.remove();
    r.container = null;
  }
}

export function cleanupCustomPages() {
  customRoutes.forEach(r => {
    if (r.container) {
      if (r.state && r.state.destroy) {
        try { r.state.destroy(); } catch (e) {}
      }
      r.state = null;
      r.container.remove();
      r.container = null;
    }
  });
  const reactRoot = document.getElementById('reactRoot');
  if (reactRoot) reactRoot.style.display = '';
  document.title = originalTitle;
}

function activateRoute(route) {
  if (route.container) return;
  if (!previousHash && !isCustomRoute()) {
    previousHash = location.hash || '#/home';
  }
  const result = route.show();
  route.container = result.container;
  route.state = result.state;
  document.title = route.title;
}

export function goBackFromCustomRoute() {
  cleanupCustomPages();
  isNavigatingBack = true;
  if (previousHash) {
    location.hash = previousHash;
  } else {
    location.hash = '#/home';
  }
  setTimeout(() => { isNavigatingBack = false; }, 100);
}

function handleRouteChange() {
  if (isNavigatingBack) return;
  const active = getActiveRoute();
  if (active) {
    customRoutes.forEach(r => {
      if (r !== active && r.container) {
        silentHideRoute(r);
      }
    });
    activateRoute(active);
  } else {
    const wasCustom = customRoutes.some(r => !!r.container);
    cleanupCustomPages();
    previousHash = null;
    if (wasCustom) {
      console.log('[HubBar] 已退出自定义路由，当前:', location.hash);
    }
  }
  updateHubBarVisibility();
}

function updateHubBarVisibility() {
  if (!hubBarInstance) return;
  hubBarInstance.style.display = isAllowedRoute() ? 'flex' : 'none';
}

export function injectHubBar(createHubBar) {
  if (hubBarInstance) return;
  if (document.getElementById('hubbar-root')) return;
  if (!document.body) return;

  hubBarInstance = document.createElement('div');
  hubBarInstance.id = 'hubbar-root';
  document.body.appendChild(hubBarInstance);

  createHubBar(hubBarInstance);
  handleRouteChange();
}

export function initInfrastructure(createHubBar) {
  const IS_DEV = typeof import.meta !== 'undefined' && import.meta.env && import.meta.env.DEV;
  if (IS_DEV) {
    console.log('[HubBar] 开发模式，跳过Jellyfin DOM注入');
    return;
  }

  if (document.body) {
    injectHubBar(createHubBar);
  } else {
    document.addEventListener('DOMContentLoaded', () => {
      injectHubBar(createHubBar);
    });
  }

  window.addEventListener('hashchange', () => {
    if (!hubBarInstance) {
      injectHubBar(createHubBar);
    } else {
      handleRouteChange();
    }
  });

  setInterval(() => {
    if (!hubBarInstance) {
      injectHubBar(createHubBar);
    } else {
      handleRouteChange();
    }
  }, 2000);
}

window.__hbRegisterRoute = registerRoute;
window.__hbGoBack = goBackFromCustomRoute;
window.__hbInit = initInfrastructure;