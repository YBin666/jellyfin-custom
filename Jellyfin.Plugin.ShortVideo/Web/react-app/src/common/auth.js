const DEV_STORAGE_KEY = 'jellyfin_dev_credentials';

// 开发模式检测：Vite注入的import.meta.env.DEV
const IS_DEV = typeof import.meta !== 'undefined' && import.meta.env && import.meta.env.DEV;

function loadDevCreds() {
  try {
    const raw = localStorage.getItem(DEV_STORAGE_KEY);
    if (raw) {
      const c = JSON.parse(raw);
      if (c && c.token) return c;
    }
  } catch (e) {}
  return null;
}

export function getToken() {
  // 优先从开发模式存储读取
  const devCreds = loadDevCreds();
  if (devCreds && devCreds.token) {
    return devCreds.token;
  }

  // 从 Jellyfin 标准 localStorage 读取
  try {
    const credStr = localStorage.getItem('jellyfin_credentials');
    if (credStr) {
      const cred = JSON.parse(credStr);
      if (cred && cred.Servers && cred.Servers.length > 0 && cred.Servers[0].AccessToken) {
        return cred.Servers[0].AccessToken;
      }
    }
  } catch (e) {}
  return '';
}

export function getUserId() {
  // 优先从开发模式存储读取
  const devCreds = loadDevCreds();
  if (devCreds && devCreds.userId) {
    return devCreds.userId;
  }

  // 从 Jellyfin 标准 localStorage 读取
  try {
    const credStr = localStorage.getItem('jellyfin_credentials');
    if (credStr) {
      const cred = JSON.parse(credStr);
      if (cred && cred.Servers && cred.Servers.length > 0 && cred.Servers[0].UserId) {
        return cred.Servers[0].UserId;
      }
    }
  } catch (e) {}
  return '';
}

export const BASE_URL = typeof window !== 'undefined'
  ? window.location.origin
  : '';

export function apiUrl(path) {
  const token = getToken();
  let url = BASE_URL + path;
  if (token) {
    const sep = path.indexOf('?') >= 0 ? '&' : '?';
    url += sep + 'api_key=' + encodeURIComponent(token);
  }
  return url;
}

export function ensureApiKey(url, key) {
  if (key && key.length >= 10) {
    if (url.indexOf('api_key=') >= 0) {
      return url.replace(/([?&])api_key=[^&]*/, '$1api_key=' + encodeURIComponent(key));
    }
    const sep = url.indexOf('?') >= 0 ? '&' : '?';
    return url + sep + 'api_key=' + encodeURIComponent(key);
  }
  return url;
}
