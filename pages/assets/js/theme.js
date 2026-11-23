// Theme-Umschalter: manuelle Auswahl mit localStorage-Persistenz,
// Systemeinstellung (prefers-color-scheme) als Standard, solange keine
// manuelle Wahl gespeichert ist.
(function () {
  'use strict';

  var STORAGE_KEY = 'vwp-theme';
  var root = document.documentElement;
  var toggle = document.getElementById('theme-toggle');
  var mediaQuery = window.matchMedia ? window.matchMedia('(prefers-color-scheme: light)') : null;

  function getStoredTheme() {
    try {
      return localStorage.getItem(STORAGE_KEY);
    } catch (e) {
      return null;
    }
  }

  function setStoredTheme(value) {
    try {
      if (value === null) {
        localStorage.removeItem(STORAGE_KEY);
      } else {
        localStorage.setItem(STORAGE_KEY, value);
      }
    } catch (e) {
      // localStorage nicht verfuegbar — Theme wechselt dann nur sitzungsbezogen.
    }
  }

  function currentTheme() {
    return root.getAttribute('data-theme') === 'light' ? 'light' : 'dark';
  }

  function applyTheme(theme) {
    root.setAttribute('data-theme', theme);
  }

  if (toggle) {
    toggle.addEventListener('click', function () {
      var next = currentTheme() === 'dark' ? 'light' : 'dark';
      applyTheme(next);
      setStoredTheme(next);
    });
  }

  // Ohne gespeicherte manuelle Wahl folgt die Seite Systemwechseln live nach.
  if (mediaQuery && typeof mediaQuery.addEventListener === 'function') {
    mediaQuery.addEventListener('change', function (event) {
      if (getStoredTheme() === null) {
        applyTheme(event.matches ? 'light' : 'dark');
      }
    });
  }
})();
