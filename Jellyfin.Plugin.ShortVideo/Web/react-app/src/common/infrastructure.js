const customRoutes = [];
let originalTitle = document.title;
let previousHash = null;
let isNavigatingBack = false;
let fabLink = null;
let drawerObserver = null;

function matchRoute(name) {
  return new RegExp('^#/' + name + '([/?]|$)').test(location.hash || '');
}

function isCustomRoute() {
  return customRoutes.some(r => matchRoute(r.name));
}

function isAllowedRoute() {
  return /^#\/(home|list)([\/?]|$)/.test(location.hash || '');
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

function cleanupCustomPages() {
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
      console.log('[ShortVideo] 已退出自定义路由，当前:', location.hash);
    }
  }
  updateFabVisibility();
}

function createDrawerItem(id, href, iconChar, label) {
  const item = document.createElement('a');
  item.id = id;
  item.href = href;
  item.className = 'navMenuOption emby-button';
  item.style.cssText = [
    'display: flex',
    'align-items: center',
    'gap: 12px',
    'padding: 12px 1.5em',
    'color: inherit',
    'text-decoration: none',
    'font-size: inherit',
    'font-weight: 400',
    'cursor: pointer',
    'width: 100%',
    'box-sizing: border-box'
  ].join(';');
  item.addEventListener('mouseenter', () => { item.style.background = 'rgba(255,255,255,0.08)'; });
  item.addEventListener('mouseleave', () => { item.style.background = ''; });

  const iconEl = document.createElement('span');
  iconEl.textContent = iconChar;
  iconEl.style.cssText = 'width: 1.6em; text-align: center; opacity: 0.8;';

  const labelEl = document.createElement('span');
  labelEl.textContent = label;

  item.appendChild(iconEl);
  item.appendChild(labelEl);
  return item;
}

function injectDrawerMenuItem() {
  if (document.getElementById('shortvideo-drawer-item') && document.getElementById('shortvideo-diy-drawer-item')) return;

  let drawer = document.querySelector('.mainDrawer');
  if (!drawer) {
    drawer = document.querySelector('.mainDrawerMenu');
    if (!drawer) return;
  }

  const menuItems = drawer.querySelectorAll('a.itemAction, a.emby-button, button.itemAction, .navMenuOption, .listItem');
  if (!menuItems || menuItems.length === 0) return;

  let homeItem = null;
  for (let i = 0; i < menuItems.length; i++) {
    const text = menuItems[i].textContent.trim().toLowerCase();
    const href = menuItems[i].getAttribute('href') || '';
    if (text === '首页' || text === 'home' || href.indexOf('#/home') >= 0) {
      homeItem = menuItems[i];
      break;
    }
  }

  const shortsItem = createDrawerItem('shortvideo-drawer-item', '#/shorts', '\u25B6', '短视频');
  const diyItem = createDrawerItem('shortvideo-diy-drawer-item', '#/diy', '\u2728', 'DIY');

  if (homeItem && homeItem.parentNode) {
    homeItem.parentNode.insertBefore(diyItem, homeItem.nextSibling);
    homeItem.parentNode.insertBefore(shortsItem, diyItem);
  } else if (menuItems[0] && menuItems[0].parentNode) {
    menuItems[0].parentNode.insertBefore(diyItem, menuItems[0].nextSibling);
    menuItems[0].parentNode.insertBefore(shortsItem, diyItem);
  }
}

function observeDrawer() {
  if (drawerObserver) return;
  drawerObserver = new MutationObserver(() => {
    injectDrawerMenuItem();
  });
  drawerObserver.observe(document.body, { childList: true, subtree: true });
}

function updateFabVisibility() {
  if (!fabLink) return;
  fabLink.style.display = isAllowedRoute() ? 'inline-flex' : 'none';
}

function injectFabButton() {
  if (fabLink) return;
  if (document.getElementById('shortvideo-fab')) return;
  if (!document.body) return;

  fabLink = document.createElement('a');
  fabLink.id = 'shortvideo-fab';
  fabLink.href = '#/shorts';
  fabLink.textContent = '刷视频';
  fabLink.style.cssText = [
    'position: fixed',
    'left: 50%',
    'bottom: 24px',
    'transform: translateX(-50%)',
    'padding: 12px 24px',
    'border-radius: 999px',
    'background: rgba(255,255,255,0.65)',
    'backdrop-filter: blur(20px) saturate(180%)',
    '-webkit-backdrop-filter: blur(20px) saturate(180%)',
    'color: #000',
    'font-size: 14px',
    'font-weight: 500',
    'font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif',
    'text-decoration: none',
    'box-shadow: 0 4px 24px rgba(0,0,0,0.12), 0 1px 4px rgba(0,0,0,0.08)',
    'border: 1px solid rgba(255,255,255,0.8)',
    'z-index: 9999',
    'transition: transform 0.2s ease, box-shadow 0.2s ease, background 0.2s ease',
    'cursor: pointer',
    'user-select: none',
    'display: inline-flex',
    'align-items: center',
    'gap: 8px'
  ].join(';');

  const icon = document.createElement('span');
  icon.textContent = '▶';
  icon.style.cssText = 'font-size: 12px; opacity: 0.8;';
  fabLink.insertBefore(icon, fabLink.firstChild);

  fabLink.addEventListener('mouseenter', () => {
    fabLink.style.transform = 'translateX(-50%) translateY(-2px)';
    fabLink.style.boxShadow = '0 8px 32px rgba(0,0,0,0.16), 0 2px 8px rgba(0,0,0,0.1)';
    fabLink.style.background = 'rgba(255,255,255,0.8)';
  });
  fabLink.addEventListener('mouseleave', () => {
    fabLink.style.transform = 'translateX(-50%) translateY(0)';
    fabLink.style.boxShadow = '0 4px 24px rgba(0,0,0,0.12), 0 1px 4px rgba(0,0,0,0.08)';
    fabLink.style.background = 'rgba(255,255,255,0.65)';
  });

  document.body.appendChild(fabLink);
  handleRouteChange();
}

export function initInfrastructure() {
  // 开发模式下跳过Jellyfin DOM操作（抽屉菜单、悬浮按钮）
  const IS_DEV = typeof import.meta !== 'undefined' && import.meta.env && import.meta.env.DEV;
  if (IS_DEV) {
    console.log('[ShortVideo] 开发模式，跳过Jellyfin DOM注入');
    return;
  }

  if (document.body) {
    injectFabButton();
    injectDrawerMenuItem();
    observeDrawer();
  } else {
    document.addEventListener('DOMContentLoaded', () => {
      injectFabButton();
      injectDrawerMenuItem();
      observeDrawer();
    });
  }

  window.addEventListener('hashchange', () => {
    if (!fabLink) {
      injectFabButton();
    } else {
      handleRouteChange();
    }
  });

  setInterval(() => {
    if (!fabLink) {
      injectFabButton();
    } else {
      handleRouteChange();
    }
    injectDrawerMenuItem();
  }, 2000);
}

window.__svRegisterRoute = registerRoute;
window.__svGoBack = goBackFromCustomRoute;
window.__svInit = initInfrastructure;
