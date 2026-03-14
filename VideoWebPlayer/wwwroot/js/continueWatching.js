window.continueWatching = {
  attach: (videoEl, mediaType, mediaId, baseUrl = '', bearerToken = '') => {
    if (!videoEl) return;
    const post = (pos, dur) => {
      const headers = { 'Content-Type': 'application/json' };
      if (bearerToken) headers['Authorization'] = `Bearer ${bearerToken}`;
      return fetch(`${baseUrl}/api/continue-watching/progress`, {
        method: 'POST',
        headers,
        body: JSON.stringify({
          mediaType,
          mediaId,
          positionSeconds: Math.floor(pos || 0),
          durationSeconds: Math.floor(dur || 0)
        })
      });
    };

    let lastSent = 0;
    const onTimeUpdate = () => {
      const pos = videoEl.currentTime || 0;
      const dur = videoEl.duration || 0;
      if (pos - lastSent >= 3) {
        lastSent = pos;
        post(pos, dur);
      }
    };
    const onEnded = () => post(videoEl.duration || 0, videoEl.duration || 0);

    videoEl.addEventListener('timeupdate', onTimeUpdate);
    videoEl.addEventListener('ended', onEnded);
  }
};