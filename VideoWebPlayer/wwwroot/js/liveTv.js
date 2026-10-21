// Live-TV-Wiedergabe des automatisierten TV-Senders (HLS).
//
// Strategie: native HLS-Wiedergabe wo der Browser sie kann (Safari), sonst hls.js ueber
// Media Source Extensions (Chrome, Firefox, Edge). hls.js wird dabei erst bei Bedarf
// nachgeladen (Lazy-Load ueber dynamisches <script>-Tag mit der von StaticAssetVersioner
// fingerabdruckten URL), damit das ~600 kB-Asset nicht auf jeder Seite geladen wird.
//
// Rueckmeldung an Blazor ueber drei DotNet-Callbacks:
//   NotifyStreamPlaying()      - die Wiedergabe laeuft (video 'playing'-Event)
//   NotifyStreamError(kind)    - fataler Ladefehler (m3u8/Netzwerk/Element-Error)
//   NotifyStreamUnsupported()  - weder natives HLS noch MSE verfuegbar
//
// Bei fatalen Fehlern versucht das Modul selbsttaetig in kurzen Abstaenden erneut zu laden
// (Re-Attach-Timer), bis detach() gerufen wird - die Seite zeigt in der Zwischenzeit die
// verstaendliche "momentan nicht verfuegbar"-Meldung.
window.liveTv = (() => {
    const RETRY_MS = 5000;
    const HLS_MIME = 'application/vnd.apple.mpegurl';

    const states = new WeakMap(); // videoEl -> { hls, dotNetRef, retryTimer, errorHandler, playingHandler, scriptUrl }

    function clearRetry(state) {
        if (state.retryTimer) {
            clearTimeout(state.retryTimer);
            state.retryTimer = null;
        }
    }

    function notifyError(state, kind) {
        if (state.errorNotified) return;
        state.errorNotified = true;
        try { state.dotNetRef.invokeMethodAsync('NotifyStreamError', kind); } catch { /* Seite evtl. schon weg */ }
    }

    function scheduleRetry(state, reload) {
        clearRetry(state);
        state.retryTimer = setTimeout(() => {
            if (states.get(videoElOf(state)) !== state) return; // detached inzwischen
            reload();
        }, RETRY_MS);
    }

    function videoElOf(state) { return state.videoEl; }

    function attachHls(state, videoEl, url) {
        const hls = new Hls({ enableWorker: true });
        state.hls = hls;
        hls.on(Hls.Events.ERROR, (_evt, data) => {
            if (!data || !data.fatal) return;
            // Fataler Fehler: Blazor benachrichtigen und selbst wieder ansetzen.
            notifyError(state, data.type || 'hls');
            scheduleRetry(state, () => {
                try { hls.startLoad(); } catch { /* faellt beim naechsten Tick erneut auf */ }
            });
        });
        hls.on(Hls.Events.FRAG_BUFFERED, () => {
            state.errorNotified = false;
            try { state.dotNetRef.invokeMethodAsync('NotifyStreamPlaying'); } catch { }
        });
        hls.loadSource(url);
        hls.attachMedia(videoEl);
    }

    function loadHlsScript(scriptUrl) {
        if (window.Hls) return Promise.resolve();
        if (window.liveTv._hlsScriptPromise) return window.liveTv._hlsScriptPromise;
        window.liveTv._hlsScriptPromise = new Promise((resolve, reject) => {
            const tag = document.createElement('script');
            tag.src = scriptUrl;
            tag.onload = () => resolve();
            tag.onerror = () => reject(new Error('hls.min.js konnte nicht geladen werden'));
            document.head.appendChild(tag);
        });
        return window.liveTv._hlsScriptPromise;
    }

    return {
        attach: (videoEl, streamUrl, hlsScriptUrl, dotNetRef) => {
            if (!videoEl) return;
            window.liveTv.detach(videoEl);

            const state = {
                videoEl,
                hls: null,
                dotNetRef,
                retryTimer: null,
                errorNotified: false,
                errorHandler: null,
                playingHandler: null,
            };
            states.set(videoEl, state);

            state.playingHandler = () => {
                state.errorNotified = false;
                clearRetry(state);
                try { state.dotNetRef.invokeMethodAsync('NotifyStreamPlaying'); } catch { }
            };
            videoEl.addEventListener('playing', state.playingHandler);

            if (videoEl.canPlayType(HLS_MIME)) {
                // Natives HLS (z. B. Safari): src direkt setzen; Ladefehler meldet das Element.
                state.errorHandler = () => notifyError(state, 'native');
                videoEl.addEventListener('error', state.errorHandler);
                videoEl.src = streamUrl;
                videoEl.load();
                return;
            }

            // hls.js-Fallback ueber MSE, Skript wird erst jetzt geladen.
            loadHlsScript(hlsScriptUrl)
                .then(() => {
                    if (states.get(videoEl) !== state) return; // detached waehrend des Ladens
                    if (!Hls.isSupported()) {
                        try { state.dotNetRef.invokeMethodAsync('NotifyStreamUnsupported'); } catch { }
                        return;
                    }
                    attachHls(state, videoEl, streamUrl);
                })
                .catch(() => {
                    if (states.get(videoEl) === state) notifyError(state, 'script');
                });
        },

        detach: (videoEl) => {
            if (!videoEl) return;
            const state = states.get(videoEl);
            if (!state) return;
            states.delete(videoEl);
            clearRetry(state);
            if (state.errorHandler) videoEl.removeEventListener('error', state.errorHandler);
            if (state.playingHandler) videoEl.removeEventListener('playing', state.playingHandler);
            if (state.hls) { try { state.hls.destroy(); } catch { } state.hls = null; }
            videoEl.removeAttribute('src');
            try { videoEl.load(); } catch { }
        }
    };
})();
