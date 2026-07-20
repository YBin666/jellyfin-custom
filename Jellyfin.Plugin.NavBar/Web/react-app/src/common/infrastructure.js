const customRoutes = [];
let originalTitle = document.title;
let previousHash = null;
let isNavigatingBack = false;
let navBarInstance = null;

function matchRoute(name) {
  return new RegExp('^#/' + name + '([/?]|$)').test(location.hash || '');
}

function isCustomRoute() {
  return customRoutes.some(r => matchRoute(r.name));
}

function isAllowedRoute() {
  return /^#\/(home|list|shorts|nav-settings)([\/?]|$)/.test(location.hash || '');
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
      console.log('[NavBar] 已退出自定义路由，当前:', location.hash);
    }
  }
  updateNavBarVisibility();
}

function updateNavBarVisibility() {
  if (!navBarInstance) return;
  navBarInstance.style.display = isAllowedRoute() ? 'flex' : 'none';
}

export function injectNavBar(createNavBar) {
  if (navBarInstance) return;
  if (document.getElementById('navbar-root')) return;
  if (!document.body) return;

  navBarInstance = document.createElement('div');
  navBarInstance.id = 'navbar-root';
  document.body.appendChild(navBarInstance);

  createNavBar(navBarInstance);
  handleRouteChange();
}

export function initInfrastructure(createNavBar) {
  const IS_DEV = typeof import.meta !== 'undefined' && import.meta.env && import.meta.env.DEV;
  if (IS_DEV) {
    console.log('[NavBar] 开发模式，跳过Jellyfin DOM注入');
    return;
  }

  if (document.body) {
    injectNavBar(createNavBar);
  } else {
    document.addEventListener('DOMContentLoaded', () => {
      injectNavBar(createNavBar);
    });
  }

  window.addEventListener('hashchange', () => {
    if (!navBarInstance) {
      injectNavBar(createNavBar);
    } else {
      handleRouteChange();
    }
  });

  setInterval(() => {
    if (!navBarInstance) {
      injectNavBar(createNavBar);
    } else {
      handleRouteChange();
    }
  }, 2000);
}

window.__hbRegisterRoute = registerRoute;
window.__hbGoBack = goBackFromCustomRoute;
window.__hbInit = initInfrastructure;