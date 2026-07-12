export function formatTime(s) {
  if (!isFinite(s) || s < 0) return '00:00';
  const m = Math.floor(s / 60);
  const sec = Math.floor(s % 60);
  return (m < 10 ? '0' : '') + m + ':' + (sec < 10 ? '0' : '') + sec;
}

export function isVideoCodecSupported(codec) {
  if (!codec) return null;
  const supported = ['h264', 'avc', 'avc1', 'mpeg4', 'mp4v'];
  if (codec === 'vp8' || codec === 'vp9') {
    try {
      const v = document.createElement('video');
      return v.canPlayType('video/webm; codecs=' + codec) !== '';
    } catch (e) { return false; }
  }
  if (codec === 'hevc' || codec === 'h265') {
    try {
      const v = document.createElement('video');
      return v.canPlayType('video/mp4; codecs=hev1') !== '' || v.canPlayType('video/mp4; codecs=hvc1') !== '';
    } catch (e) { return false; }
  }
  if (codec === 'av1' || codec === 'av01') {
    try {
      const v = document.createElement('video');
      return v.canPlayType('video/mp4; codecs=av01.0.05M.08') !== '';
    } catch (e) { return false; }
  }
  if (codec.indexOf('mpeg2') >= 0 || codec.indexOf('msmpeg4') >= 0 || codec === 'wmv3' || codec === 'wmv2' || codec === 'vc1') return false;
  return supported.indexOf(codec) >= 0;
}

export function isAudioCodecSupported(codec) {
  if (!codec) return null;
  const supported = ['aac', 'mp3', 'mp2', 'opus', 'vorbis', 'flac', 'pcm'];
  if (codec === 'ac3' || codec === 'eac3' || codec === 'dts' || codec === 'truehd') return false;
  return supported.indexOf(codec) >= 0;
}

export function shouldDirectStream(videoCodec, audioCodec, container) {
  const videoOk = isVideoCodecSupported(videoCodec);
  const audioOk = isAudioCodecSupported(audioCodec);
  if (videoOk === false || audioOk === false) return false;
  const unsupported = ['avi', 'mkv', 'mov', 'wmv', 'flv', 'rmvb', 'rm', 'ts', 'm2ts'];
  if (container && unsupported.indexOf(container) >= 0) return false;
  return true;
}

export function buildHlsSrc(streamUrl, id, apiKey, baseUrl) {
  const transcodeParams = 'VideoCodec=h264&AudioCodec=aac&VideoBitrate=4000000&AudioBitrate=192000';
  const src = streamUrl.indexOf('http') === 0 ? streamUrl : baseUrl + streamUrl;
  const srcWithKey = apiKey ? src : src;

  const streamMatch = src.match(/\/Videos\/([^/]+)\/stream\?(.*)/);
  if (streamMatch) {
    const videoId = streamMatch[1];
    const qs = streamMatch[2]
      .replace(/(^|&)static=true&?/i, '$1')
      .replace(/(^|&)api_key=[^&]*/i, '$1')
      .replace(/^&+/, '').replace(/&+$/, '');
    return baseUrl + '/Videos/' + videoId + '/main.m3u8?'
      + (qs ? qs + '&' : '')
      + 'api_key=' + encodeURIComponent(apiKey) + '&'
      + transcodeParams;
  } else if (id) {
    return baseUrl + '/Videos/' + id + '/main.m3u8?api_key='
      + encodeURIComponent(apiKey) + '&' + transcodeParams;
  }
  return '';
}
