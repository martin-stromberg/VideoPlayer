window.backupUpload = (() => {
  const DEFAULT_CHUNK_SIZE = 128 * 1024 * 1024;
  const CHUNK_URL = 'admin/backups/api/upload/chunk';
  const UPLOAD_URL = 'admin/backups/api/upload';
  const MAX_NETWORK_ATTEMPTS = 5;
  const MAX_RESTARTS = 3;
  const STORAGE_PREFIX = 'vwp-backup-upload:';
  const RESUME_HINT = 'Der Upload kann fortgesetzt werden: dieselbe Datei erneut auswählen und auf "Backup hochladen" klicken.';

  let activeRun = null;

  const getChunkSize = () => {
    const override = Number(window.backupUploadChunkSizeBytes);
    return Number.isFinite(override) && override > 0 ? Math.floor(override) : DEFAULT_CHUNK_SIZE;
  };

  const storageKey = (file) => `${STORAGE_PREFIX}${file.name}:${file.size}:${file.lastModified}`;

  const formatBytes = (bytes) => {
    const units = ['B', 'KB', 'MB', 'GB'];
    let value = bytes;
    let unit = 0;
    while (value >= 1024 && unit < units.length - 1) {
      value /= 1024;
      unit++;
    }
    return `${value.toFixed(value >= 10 || unit === 0 ? 0 : 1)} ${units[unit]}`;
  };

  const delay = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

  const showMessage = (container, message) => {
    container.innerHTML = '';
    const alert = document.createElement('div');
    alert.className = 'alert alert-danger';
    alert.textContent = message;
    container.appendChild(alert);
  };

  const createUi = (container) => {
    container.innerHTML = '';

    const wrapper = document.createElement('div');
    wrapper.className = 'backup-upload-status mt-2';

    const bar = document.createElement('div');
    bar.className = 'progress';
    bar.setAttribute('role', 'progressbar');
    const fill = document.createElement('div');
    fill.className = 'progress-bar progress-bar-striped progress-bar-animated';
    fill.style.width = '0%';
    fill.setAttribute('aria-valuemin', '0');
    fill.setAttribute('aria-valuemax', '100');
    fill.setAttribute('aria-valuenow', '0');
    bar.appendChild(fill);

    const text = document.createElement('div');
    text.className = 'backup-upload-progress-text form-text mt-1';

    const abortButton = document.createElement('button');
    abortButton.type = 'button';
    abortButton.className = 'btn btn-outline-secondary btn-sm mt-2';
    abortButton.textContent = 'Abbrechen';

    wrapper.appendChild(bar);
    wrapper.appendChild(text);
    wrapper.appendChild(abortButton);
    container.appendChild(wrapper);
    return { fill, text, abortButton };
  };

  const updateProgress = (ui, sent, total, note) => {
    const percent = total > 0 ? Math.min(100, Math.floor((sent / total) * 100)) : 0;
    ui.fill.style.width = `${percent}%`;
    ui.fill.setAttribute('aria-valuenow', String(percent));
    ui.text.textContent = `Übertragen: ${formatBytes(sent)} von ${formatBytes(total)} (${percent} %)${note ? ` – ${note}` : ''}`;
  };

  const finishWithStatus = (run, key, message) => {
    localStorage.removeItem(key);
    if (run.containerEl.isConnected)
      window.location.assign(`/admin/backups?backupStatus=${encodeURIComponent(message)}`);
  };

  const finishWithError = (run, key, message, keepEntry) => {
    if (!keepEntry)
      localStorage.removeItem(key);
    if (run.containerEl.isConnected)
      window.location.assign(`/admin/backups?backupError=${encodeURIComponent(message)}`);
  };

  const queryServerOffset = async (uploadId, signal) => {
    const response = await fetch(`${UPLOAD_URL}/${uploadId}`, { method: 'GET', signal });
    if (response.status === 404)
      return null;
    if (!response.ok)
      throw new Error(`Status ${response.status}`);
    const headerOffset = response.headers.get('Upload-Offset');
    if (headerOffset !== null)
      return Number(headerOffset);
    const data = await response.json();
    return Number(data.uploadOffset);
  };

  // XMLHttpRequest is used instead of fetch because fetch cannot report
  // upload progress; xhr.upload.onprogress updates the bar within a chunk.
  const postChunk = (headers, body, signal, onProgress) => new Promise((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    xhr.open('POST', CHUNK_URL);
    Object.keys(headers).forEach((name) => xhr.setRequestHeader(name, headers[name]));

    if (xhr.upload) {
      xhr.upload.onprogress = (event) => {
        if (event.lengthComputable)
          onProgress(event.loaded);
      };
    }

    const abort = () => xhr.abort();
    const cleanup = () => {
      if (signal)
        signal.removeEventListener('abort', abort);
    };

    if (signal) {
      if (signal.aborted) {
        reject(new DOMException('Aborted', 'AbortError'));
        return;
      }
      signal.addEventListener('abort', abort, { once: true });
    }

    xhr.onload = () => {
      cleanup();
      resolve({
        status: xhr.status,
        getHeader: (name) => xhr.getResponseHeader(name),
        text: () => xhr.responseText
      });
    };
    xhr.onerror = () => {
      cleanup();
      reject(new Error('NetworkError'));
    };
    xhr.onabort = () => {
      cleanup();
      reject(new DOMException('Aborted', 'AbortError'));
    };

    xhr.send(body);
  });

  const describeHttpError = (status) => {
    if (status === 401 || status === 403)
      return 'Ihre Anmeldung ist abgelaufen oder es fehlen Berechtigungen. Bitte melden Sie sich erneut an.';
    if (status === 404)
      return 'Die Upload-Session wurde nicht gefunden.';
    if (status === 409)
      return 'Die Upload-Daten stimmen nicht mit der bestehenden Session überein.';
    if (status === 413)
      return 'Die Datei ist zu groß oder wurde vom Serverlimit abgelehnt.';
    if (status === 415)
      return 'Das Dateiformat wird vom Server nicht unterstützt.';
    if (status >= 500)
      return 'Der Server meldet einen internen Fehler. Bitte versuchen Sie es später erneut.';
    return `Der Server hat die Anfrage abgelehnt (Status ${status}).`;
  };

  const readErrorMessage = (response) => {
    try {
      const data = JSON.parse(response.text());
      if (data && data.error)
        return data.error;
    } catch {
    }
    return `Upload fehlgeschlagen: ${describeHttpError(response.status)}`;
  };

  const getResumableFileName = () => {
    try {
      for (let i = 0; i < localStorage.length; i++) {
        const key = localStorage.key(i);
        const match = key && key.match(/^vwp-backup-upload:(.*):(\d+):(\d+)$/);
        if (match)
          return match[1];
      }
    } catch {
    }
    return null;
  };

  const discardResumable = async (antiforgeryToken) => {
    const keys = [];
    try {
      for (let i = 0; i < localStorage.length; i++) {
        const key = localStorage.key(i);
        if (key && key.startsWith(STORAGE_PREFIX))
          keys.push(key);
      }
    } catch {
    }

    for (const key of keys) {
      const uploadId = localStorage.getItem(key);
      localStorage.removeItem(key);
      if (!uploadId || !antiforgeryToken)
        continue;
      try {
        await fetch(`${UPLOAD_URL}/${encodeURIComponent(uploadId)}`, {
          method: 'DELETE',
          headers: { 'RequestVerificationToken': antiforgeryToken }
        });
      } catch {
      }
    }
  };

  const setControlsDisabled = (inputEl, buttonEl, disabled) => {
    if (inputEl)
      inputEl.disabled = disabled;
    if (buttonEl)
      buttonEl.disabled = disabled;
  };

  const start = async (inputEl, containerEl, antiforgeryToken, buttonEl) => {
    if (!containerEl)
      return;

    if (activeRun) {
      if (activeRun.containerEl.isConnected) {
        showMessage(containerEl, 'Es läuft bereits ein Upload. Bitte warten Sie, bis er abgeschlossen ist.');
        return;
      }

      // Orphaned run from a previous page: enhanced navigation does not reload
      // this module, so abort the stale upload and wait until it has stopped.
      activeRun.controller.abort();
      await activeRun.done;
    }

    const file = inputEl && inputEl.files && inputEl.files.length > 0 ? inputEl.files[0] : null;
    if (!file) {
      showMessage(containerEl, 'Bitte eine Backupdatei auswählen.');
      return;
    }
    if (!antiforgeryToken) {
      showMessage(containerEl, 'Sicherheitstoken fehlt. Bitte die Seite neu laden.');
      return;
    }

    const key = storageKey(file);
    const ui = createUi(containerEl);
    const controller = new AbortController();
    const run = { containerEl, controller, done: null, resolveDone: null };
    run.done = new Promise((resolve) => { run.resolveDone = resolve; });
    activeRun = run;

    ui.abortButton.addEventListener('click', () => controller.abort());
    const observer = new MutationObserver(() => {
      if (!containerEl.isConnected)
        controller.abort();
    });
    observer.observe(document.documentElement, { childList: true, subtree: true });
    setControlsDisabled(inputEl, buttonEl, true);

    try {
      let uploadId = localStorage.getItem(key);
      let offset = 0;
      let restarts = 0;
      let attempts = 0;

      if (uploadId) {
        try {
          const resumed = await queryServerOffset(uploadId, controller.signal);
          if (resumed === null) {
            localStorage.removeItem(key);
            uploadId = null;
          } else {
            offset = resumed;
          }
        } catch {
          if (controller.signal.aborted) {
            finishAborted(ui);
            return;
          }
        }
      }

      updateProgress(ui, offset, file.size);

      while (offset < file.size) {
        if (controller.signal.aborted || !containerEl.isConnected)
          break;

        const end = Math.min(offset + getChunkSize(), file.size);
        const headers = {
          'Content-Type': 'application/octet-stream',
          'Upload-Name': encodeURIComponent(file.name),
          'Upload-Length': String(file.size),
          'Upload-Offset': String(offset),
          'RequestVerificationToken': antiforgeryToken
        };
        if (uploadId)
          headers['Upload-Id'] = uploadId;

        let response;
        try {
          response = await postChunk(headers, file.slice(offset, end), controller.signal,
            (loaded) => updateProgress(ui, offset + loaded, file.size));
        } catch {
          if (controller.signal.aborted)
            break;
          attempts++;
          if (attempts > MAX_NETWORK_ATTEMPTS) {
            finishWithError(run, key, `Upload fehlgeschlagen: Netzwerkfehler. ${RESUME_HINT}`, true);
            return;
          }
          updateProgress(ui, offset, file.size, 'unterbrochen – wird fortgesetzt');
          await delay(500 * attempts);
          if (uploadId) {
            try {
              const serverOffset = await queryServerOffset(uploadId, controller.signal);
              if (serverOffset === null) {
                localStorage.removeItem(key);
                uploadId = null;
                offset = 0;
              } else {
                offset = serverOffset;
              }
            } catch {
              if (controller.signal.aborted)
                break;
            }
          }
          continue;
        }

        attempts = 0;

        if (response.status === 204) {
          uploadId = response.getHeader('Upload-Id') || uploadId;
          if (uploadId)
            localStorage.setItem(key, uploadId);
          const nextOffset = Number(response.getHeader('Upload-Offset'));
          offset = Number.isFinite(nextOffset) && nextOffset >= 0 ? nextOffset : end;
          updateProgress(ui, offset, file.size);
          continue;
        }

        if (response.status === 308) {
          const resumeOffset = Number(response.getHeader('Upload-Offset'));
          offset = Number.isFinite(resumeOffset) && resumeOffset >= 0 ? resumeOffset : offset;
          updateProgress(ui, offset, file.size, 'wird fortgesetzt');
          continue;
        }

        if (response.status === 404) {
          restarts++;
          if (restarts > MAX_RESTARTS) {
            finishWithError(run, key, 'Upload fehlgeschlagen: Die Upload-Session wurde nicht gefunden.', false);
            return;
          }
          localStorage.removeItem(key);
          uploadId = null;
          offset = 0;
          continue;
        }

        if (response.status === 200) {
          let message = 'Backup wurde importiert.';
          try {
            const data = JSON.parse(response.text());
            if (data && data.message)
              message = data.message;
          } catch {
          }
          finishWithStatus(run, key, message);
          return;
        }

        const message = readErrorMessage(response);

        if (response.status >= 500) {
          attempts++;
          if (attempts <= MAX_NETWORK_ATTEMPTS) {
            await delay(500 * attempts);
            continue;
          }

          finishWithError(run, key, `${message} ${RESUME_HINT}`, true);
          return;
        }

        const resumable = response.status === 401 || response.status === 403;
        finishWithError(run, key, resumable ? `${message} ${RESUME_HINT}` : message, resumable);
        return;
      }

      if (controller.signal.aborted && containerEl.isConnected)
        finishAborted(ui);
    }
    finally {
      observer.disconnect();
      if (activeRun === run)
        activeRun = null;
      run.resolveDone();
      setControlsDisabled(inputEl, buttonEl, false);
    }
  };

  const finishAborted = (ui) => {
    ui.text.textContent = 'Upload abgebrochen. Um ihn fortzusetzen, wählen Sie dieselbe Datei erneut aus und klicken Sie auf "Backup hochladen".';
    ui.abortButton.disabled = true;
  };

  return { start, getResumableFileName, discardResumable };
})();
