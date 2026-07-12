(function() {
    'use strict';

    var link = null;
    var originalTitle = document.title;
    var previousHash = null;
    var isNavigatingBack = false;

    function getToken() {
        try {
            var credStr = localStorage.getItem('jellyfin_credentials');
            if (credStr) {
                var cred = JSON.parse(credStr);
                if (cred && cred.Servers && cred.Servers.length > 0 && cred.Servers[0].AccessToken) {
                    return cred.Servers[0].AccessToken;
                }
            }
        } catch(e) {}
        return '';
    }

    function getUserId() {
        try {
            var credStr = localStorage.getItem('jellyfin_credentials');
            if (credStr) {
                var cred = JSON.parse(credStr);
                if (cred && cred.Servers && cred.Servers.length > 0 && cred.Servers[0].UserId) {
                    return cred.Servers[0].UserId;
                }
            }
        } catch(e) {}
        return '';
    }

    window.__svGetToken = getToken;
    window.__svGetUserId = getUserId;

    function isAllowedRoute() {
        return /^#\/(home|list)([\/?]|$)/.test(location.hash || '');
    }

    function matchRoute(name) {
        return new RegExp('^#/' + name + '([/?]|$)').test(location.hash || '');
    }

    function isCustomRoute() {
        return customRoutes.some(function(r) { return matchRoute(r.name); });
    }

    var customRoutes = [];

    function registerRoute(cfg) {
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
        for (var i = 0; i < customRoutes.length; i++) {
            if (matchRoute(customRoutes[i].name)) return customRoutes[i];
        }
        return null;
    }

    function getRoute(name) {
        for (var i = 0; i < customRoutes.length; i++) {
            if (customRoutes[i].name === name) return customRoutes[i];
        }
        return null;
    }

    function goBackFromCustomRoute() {
        cleanupCustomPages();
        isNavigatingBack = true;
        if (previousHash) {
            location.hash = previousHash;
        } else {
            location.hash = '#/home';
        }
        setTimeout(function() { isNavigatingBack = false; }, 100);
    }

    function cleanupCustomPages() {
        customRoutes.forEach(function(r) {
            if (r.container) {
                if (r.state && r.state.destroy) {
                    try { r.state.destroy(); } catch(e) {}
                }
                r.state = null;
                r.container.remove();
                r.container = null;
            }
        });

        var reactRoot = document.getElementById('reactRoot');
        if (reactRoot) reactRoot.style.display = '';
        document.title = originalTitle;
    }

    function silentHideRoute(r) {
        if (!r || !r.container) return;
        if (r.hide) {
            r.hide(r);
        } else {
            if (r.state && r.state.destroy) {
                try { r.state.destroy(); } catch(e) {}
            }
            r.state = null;
            r.container.remove();
            r.container = null;
        }
    }

    function updateVisibility() {
        if (!link) return;
        link.style.display = isAllowedRoute() ? 'inline-flex' : 'none';
    }

    function activateRoute(route) {
        if (route.container) return;

        if (!previousHash && !isCustomRoute()) {
            previousHash = location.hash || '#/home';
        }

        var result = route.show();
        route.container = result.container;
        route.state = result.state;
        document.title = route.title;
    }

    function handleRouteChange() {
        if (isNavigatingBack) return;

        var active = getActiveRoute();
        if (active) {
            customRoutes.forEach(function(r) {
                if (r !== active && r.container) {
                    silentHideRoute(r);
                }
            });
            activateRoute(active);
        } else {
            var wasCustom = customRoutes.some(function(r) { return !!r.container; });
            cleanupCustomPages();
            previousHash = null;
            if (wasCustom) {
                console.log('[ShortVideo] 已退出自定义路由，当前:', location.hash);
            }
        }
        updateVisibility();
    }

    function injectButton() {
        if (link) return;
        if (document.getElementById('shortvideo-fab')) return;
        if (!document.body) return;

        link = document.createElement('a');
        link.id = 'shortvideo-fab';
        link.href = '#/shorts';
        link.textContent = '短视频';
        link.style.cssText = [
            'position: fixed',
            'right: 24px',
            'bottom: 24px',
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

        var icon = document.createElement('span');
        icon.textContent = '▶';
        icon.style.cssText = 'font-size: 12px; opacity: 0.8;';
        link.insertBefore(icon, link.firstChild);

        link.addEventListener('mouseenter', function() {
            link.style.transform = 'translateY(-2px)';
            link.style.boxShadow = '0 8px 32px rgba(0,0,0,0.16), 0 2px 8px rgba(0,0,0,0.1)';
            link.style.background = 'rgba(255,255,255,0.8)';
        });
        link.addEventListener('mouseleave', function() {
            link.style.transform = 'translateY(0)';
            link.style.boxShadow = '0 4px 24px rgba(0,0,0,0.12), 0 1px 4px rgba(0,0,0,0.08)';
            link.style.background = 'rgba(255,255,255,0.65)';
        });

        document.body.appendChild(link);
        handleRouteChange();
        console.log('[ShortVideo] 悬浮按钮已注入, 当前路由:', location.hash);
    }

    var drawerObserver = null;

    function createDrawerItem(id, href, iconChar, label) {
        var item = document.createElement('a');
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
        item.addEventListener('mouseenter', function() { item.style.background = 'rgba(255,255,255,0.08)'; });
        item.addEventListener('mouseleave', function() { item.style.background = ''; });

        var iconEl = document.createElement('span');
        iconEl.textContent = iconChar;
        iconEl.style.cssText = 'width: 1.6em; text-align: center; opacity: 0.8;';

        var labelEl = document.createElement('span');
        labelEl.textContent = label;

        item.appendChild(iconEl);
        item.appendChild(labelEl);
        return item;
    }

    function injectDrawerMenuItem() {
        if (document.getElementById('shortvideo-drawer-item')) return;

        var drawer = document.querySelector('.mainDrawer');
        if (!drawer) {
            drawer = document.querySelector('.mainDrawerMenu');
            if (!drawer) return;
        }

        var menuItems = drawer.querySelectorAll('a.itemAction, a.emby-button, button.itemAction, .navMenuOption, .listItem');
        if (!menuItems || menuItems.length === 0) return;

        var homeItem = null;
        for (var i = 0; i < menuItems.length; i++) {
            var text = menuItems[i].textContent.trim().toLowerCase();
            var href = menuItems[i].getAttribute('href') || '';
            if (text === '首页' || text === 'home' || href.indexOf('#/home') >= 0) {
                homeItem = menuItems[i];
                break;
            }
        }

        var shortsItem = createDrawerItem('shortvideo-drawer-item', '#/shorts', '\u25B6', '短视频');
        var diyItem = createDrawerItem('shortvideo-diy-drawer-item', '#/diy', '\u2728', 'DIY');

        if (homeItem && homeItem.parentNode) {
            homeItem.parentNode.insertBefore(diyItem, homeItem.nextSibling);
            homeItem.parentNode.insertBefore(shortsItem, diyItem);
            console.log('[ShortVideo] 短视频+DIY 已插入到「首页」下方');
        } else {
            if (menuItems[0] && menuItems[0].parentNode) {
                menuItems[0].parentNode.insertBefore(diyItem, menuItems[0].nextSibling);
                menuItems[0].parentNode.insertBefore(shortsItem, diyItem);
                console.log('[ShortVideo] 短视频+DIY 已插入到第一个菜单项后面');
            }
        }
    }

    function observeDrawer() {
        if (drawerObserver) return;
        drawerObserver = new MutationObserver(function() {
            injectDrawerMenuItem();
        });
        drawerObserver.observe(document.body, { childList: true, subtree: true });
    }

    window.__svInit = function() {
        if (document.body) {
            injectButton();
            injectDrawerMenuItem();
            observeDrawer();
        } else {
            document.addEventListener('DOMContentLoaded', function() {
                injectButton();
                injectDrawerMenuItem();
                observeDrawer();
            });
        }

        window.addEventListener('hashchange', function() {
            if (!link) {
                injectButton();
            } else {
                handleRouteChange();
            }
        });

        setInterval(function() {
            if (!link) {
                injectButton();
            } else {
                handleRouteChange();
            }
            injectDrawerMenuItem();
        }, 2000);
    };

    window.__svRegisterRoute = registerRoute;
    window.__svGoBack = goBackFromCustomRoute;
})();
