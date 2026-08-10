window.continueWatching = {
  attach: (videoEl, mediaType, mediaId, baseUrl = '', bearerToken = '') => {
    if (!videoEl) return;
    window.continueWatching.detach(videoEl);

    const post = (pos, dur) => {
      const positionSeconds = Math.max(0, Math.floor(pos || 0));
      const durationSeconds = Number.isFinite(dur) ? Math.max(0, Math.floor(dur || 0)) : 0;
      const headers = { 'Content-Type': 'application/json' };
      if (bearerToken) headers['Authorization'] = `Bearer ${bearerToken}`;
      return fetch(`${baseUrl}/api/continue-watching/progress`, {
        method: 'POST',
        headers,
        body: JSON.stringify({
          mediaType,
          mediaId,
          positionSeconds,
          durationSeconds
        })
      }).catch(() => {});
    };

    let lastSent = 0;
    const sendProgress = (force = false) => {
      const pos = videoEl.currentTime || 0;
      const dur = videoEl.duration || 0;
      if (force || pos - lastSent >= 3) {
        lastSent = pos;
        return post(pos, dur);
      }
      return undefined;
    };
    const onTimeUpdate = () => sendProgress();
    const onPause = () => sendProgress(true);
    const onEnded = () => post(videoEl.duration || 0, videoEl.duration || 0);

    videoEl.addEventListener('timeupdate', onTimeUpdate);
    videoEl.addEventListener('pause', onPause);
    videoEl.addEventListener('ended', onEnded);
    videoEl._continueWatchingCleanup = () => {
      sendProgress(true);
      videoEl.removeEventListener('timeupdate', onTimeUpdate);
      videoEl.removeEventListener('pause', onPause);
      videoEl.removeEventListener('ended', onEnded);
      delete videoEl._continueWatchingCleanup;
    };
  },
  detach: (videoEl) => {
    if (videoEl && videoEl._continueWatchingCleanup)
      videoEl._continueWatchingCleanup();
  }
};
