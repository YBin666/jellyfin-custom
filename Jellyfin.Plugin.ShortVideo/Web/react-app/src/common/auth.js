const DEV_STORAGE_KEY = 'jellyfin_dev_credentials';

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

function getCredentials() {
  const devCreds = loadDevCreds();
  if (devCreds && devCreds.token) {
    return { token: devCreds.token, userId: devCreds.userId || '' };
  }

  try {
    const credStr = localStorage.getItem('jellyfin_credentials');
    if (credStr) {
      const cred = JSON.parse(credStr);
      if (cred && cred.Servers && cred.Servers.length > 0) {
        return {
          token: cred.Servers[0].AccessToken || '',
          userId: cred.Servers[0].UserId || ''
        };
      }
    }
  } catch (e) {}
  return { token: '', userId: '' };
}

export function getToken() {
  return getCredentials().token;
}

export function getUserId() {
  return getCredentials().userId;
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

/**
 * 确保 URL 包含 api_key 参数。如果已有则不重复添加。
 */
export function ensureApiKey(url, apiKey) {
  if (!apiKey) return url;
  if (url.indexOf('api_key=') >= 0) return url;
  const sep = url.indexOf('?') >= 0 ? '&' : '?';
  return url + sep + 'api_key=' + encodeURIComponent(apiKey);
}
