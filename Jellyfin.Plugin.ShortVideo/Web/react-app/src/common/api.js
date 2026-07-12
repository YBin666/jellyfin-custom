/**
 * 统一 API 层 —— 集中管理所有后端接口调用。
 * 所有 fetch 请求都通过此模块发起，禁止在组件中直接拼 URL 调用 fetch。
 */
import { getToken, getUserId, BASE_URL, apiUrl, ensureApiKey } from './auth';
import { buildHlsSrc } from '../utils/videoUtils';

// ==================== 内部工具 ====================

/** 带认证的 fetch 封装 */
function authFetch(path, options = {}) {
  return fetch(apiUrl(path), { credentials: 'include', ...options });
}

/** Jellyfin 原生 API 的 fetch（需要手动拼 userId / api_key） */
function jellyfinFetch(path, options = {}) {
  return fetch(BASE_URL + path, { credentials: 'include', ...options });
}

// ==================== 短视频插件接口 ====================

/**
 * 获取下一批短视频（随机分页抽取，所有视频机会均等）。
 * @returns {Promise<Array>} ShortVideoItem 数组
 */
export function getNextBatch() {
  return authFetch('/ShortVideo/NextBatch')
    .then(r => {
      if (!r.ok) throw new Error('HTTP ' + r.status);
      return r.json();
    });
}

/**
 * 强制刷新候选池。
 * @returns {Promise<Object>}
 */
export function reloadFeed() {
  return authFetch('/ShortVideo/Reload', { method: 'POST' })
    .then(r => r.json());
}

// ==================== Jellyfin 用户行为接口 ====================

/**
 * 上报播放开始（Jellyfin 会话管理）。
 * @param {string} itemId - 媒体项 ID
 * @param {number} positionTicks - 起始位置（ticks）
 */
export function reportPlaybackStart(itemId, positionTicks = 0) {
  const userId = getUserId();
  const token = getToken();
  if (!itemId || !userId) return Promise.resolve();
  let path = '/Users/' + userId + '/PlayingItems/' + itemId;
  const params = ['api_key=' + encodeURIComponent(token)];
  if (positionTicks > 0) params.push('PositionTicks=' + positionTicks);
  params.push('PlayMethod=DirectStream');
  path += '?' + params.join('&');
  return jellyfinFetch(path, { method: 'POST' }).catch(() => {});
}

/**
 * 上报播放停止。Jellyfin 会根据 positionTicks 判断是否标记为已播放。
 * @param {string} itemId - 媒体项 ID
 * @param {number} positionTicks - 停止位置（ticks）
 */
export function reportPlaybackStop(itemId, positionTicks = 0) {
  const userId = getUserId();
  const token = getToken();
  if (!itemId || !userId) return Promise.resolve();
  let path = '/Users/' + userId + '/PlayingItems/' + itemId;
  const params = ['api_key=' + encodeURIComponent(token)];
  if (positionTicks > 0) params.push('PositionTicks=' + positionTicks);
  params.push('PlayMethod=DirectStream');
  path += '?' + params.join('&');
  return jellyfinFetch(path, { method: 'DELETE' }).catch(() => {});
}

// ==================== Jellyfin 收藏接口 ====================

/**
 * 获取用户收藏的视频列表。
 * @returns {Promise<Object>} { Items, TotalRecordCount }
 */
export function getFavorites() {
  const userId = getUserId();
  const token = getToken();
  if (!userId) return Promise.resolve({ Items: [], TotalRecordCount: 0 });
  const path = '/Users/' + userId + '/Items?IsFavorite=true&Recursive=true&IncludeItemTypes=Video&Fields=BasicSyncInfo,PrimaryImageAspectRatio&api_key=' + encodeURIComponent(token);
  return jellyfinFetch(path)
    .then(r => r.json())
    .catch(() => ({ Items: [], TotalRecordCount: 0 }));
}

/**
 * 切换收藏状态。
 * @param {string} itemId - 媒体项 ID
 * @param {boolean} isFavoriting - true=收藏, false=取消收藏
 * @returns {Promise<boolean>} 是否成功
 */
export function toggleFavorite(itemId, isFavoriting) {
  const userId = getUserId();
  const token = getToken();
  if (!itemId || !userId) return Promise.resolve(false);
  const path = '/Users/' + userId + '/FavoriteItems/' + itemId + '?api_key=' + encodeURIComponent(token);
  return jellyfinFetch(path, {
    method: isFavoriting ? 'POST' : 'DELETE',
    headers: { 'Content-Type': 'application/json' }
  }).then(r => r.ok).catch(() => false);
}

/**
 * 获取单个媒体项的用户数据（收藏状态、播放次数等）。
 * @param {string} itemId - 媒体项 ID
 * @returns {Promise<Object|null>} UserData 对象
 */
export function getItemUserData(itemId) {
  const userId = getUserId();
  const token = getToken();
  if (!itemId || !userId) return Promise.resolve(null);
  const path = '/Users/' + userId + '/Items/' + itemId + '?api_key=' + encodeURIComponent(token);
  return jellyfinFetch(path)
    .then(r => r.json())
    .catch(() => null);
}

// ==================== Jellyfin 媒体资源接口 ====================

/**
 * 获取视频封面图 URL。
 * @param {string} itemId - 媒体项 ID
 * @param {number} width - 宽度
 * @param {number} height - 高度
 * @returns {string} 图片 URL
 */
export function getImageUrl(itemId, width = 720, height = 1080) {
  return BASE_URL + '/Items/' + itemId + '/Images/Primary?fillHeight=' + height + '&fillWidth=' + width + '&quality=80';
}

/**
 * 获取缩略图 URL（用于收藏面板列表项）。
 * @param {string} itemId - 媒体项 ID
 * @returns {string} 图片 URL
 */
export function getThumbnailUrl(itemId) {
  return BASE_URL + '/Items/' + itemId + '/Images/Primary?fillHeight=320&fillWidth=180&quality=80';
}

/**
 * 构造静态直链播放 URL。
 * @param {string} itemId - 媒体项 ID
 * @returns {string} 播放 URL
 */
export function getStreamUrl(itemId) {
  return '/Videos/' + itemId + '/stream?static=true';
}

/**
 * 构造 HLS 转码播放 URL。
 * @param {string} streamUrl - 原始 stream URL
 * @param {string} itemId - 媒体项 ID
 * @returns {string} HLS URL
 */
export function getHlsUrl(streamUrl, itemId) {
  const apiKey = getToken();
  return buildHlsSrc(streamUrl, itemId, apiKey, BASE_URL);
}

/**
 * 确保播放 URL 包含 api_key。
 * @param {string} url - 原始 URL
 * @returns {string} 带 api_key 的 URL
 */
export function ensureStreamKey(url) {
  return ensureApiKey(url, getToken());
}
