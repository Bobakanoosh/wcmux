/**
 * terminal-host.js
 *
 * Hosted xterm.js surface for wcmux panes. Communicates with the native
 * WinUI host through WebView2's chrome.webview.hostObjects interface.
 *
 * The native side calls the global functions defined here:
 *   - writeOutput(base64Data)   Write VT output to the terminal
 *   - resize()                  Trigger a fit/resize measurement
 *   - getCellSize()             Return { width, height } of one cell
 *   - focus()                   Focus the terminal
 *   - dispose()                 Tear down the terminal
 */

(function () {
  "use strict";

  const container = document.getElementById("terminal-container");

  const term = new Terminal({
    cursorBlink: true,
    cursorStyle: "block",
    fontFamily: "'Cascadia Code', 'Cascadia Mono', Consolas, 'Courier New', monospace",
    fontSize: 14,
    theme: {
      background: "#1e1e1e",
      foreground: "#cccccc",
      cursor: "#aeafad",
      selectionBackground: "#264f78",
    },
    allowProposedApi: true,
    scrollback: 10000,
    convertEol: false,
  });

  const fitAddon = new FitAddon.FitAddon();
  const webLinksAddon = new WebLinksAddon.WebLinksAddon();

  term.loadAddon(fitAddon);
  term.loadAddon(webLinksAddon);
  term.open(container);
  fitAddon.fit();

  // --- Input routing ---
  // Send user keystrokes and paste data to the native host
  term.onData(function (data) {
    try {
      // Encode as base64 to handle binary-safe transport
      const encoded = btoa(unescape(encodeURIComponent(data)));
      window.chrome.webview.postMessage(JSON.stringify({
        type: "input",
        data: encoded,
      }));
    } catch (e) {
      // Swallow errors if host is not attached yet
    }
  });

  // Binary data handler for pasted content that may contain non-UTF8 bytes
  term.onBinary(function (data) {
    try {
      const encoded = btoa(data);
      window.chrome.webview.postMessage(JSON.stringify({
        type: "input",
        data: encoded,
      }));
    } catch (e) {
      // Swallow
    }
  });

  // Notify the host when terminal dimensions change after a fit
  function notifyResize() {
    try {
      window.chrome.webview.postMessage(JSON.stringify({
        type: "resize",
        cols: term.cols,
        rows: term.rows,
      }));
    } catch (e) {
      // Swallow
    }
  }

  // --- Global API for native host ---

  /**
   * Write VT output data (base64-encoded) to the terminal.
   */
  window.writeOutput = function (base64Data) {
    try {
      const decoded = decodeURIComponent(escape(atob(base64Data)));
      term.write(decoded);
    } catch (e) {
      // If decoding fails, try raw atob
      try {
        term.write(atob(base64Data));
      } catch (_) {
        // Last resort - drop the data
      }
    }
  };

  /**
   * Trigger a fit/resize of the terminal to fill its container.
   * Returns the new cols and rows.
   */
  window.resize = function () {
    fitAddon.fit();
    notifyResize();
    return JSON.stringify({ cols: term.cols, rows: term.rows });
  };

  /**
   * Get the measured cell size in pixels.
   */
  window.getCellSize = function () {
    var cellWidth = 0;
    var cellHeight = 0;
    // xterm.js exposes cell dimensions through internal renderer metrics
    try {
      var dims = fitAddon.proposeDimensions();
      if (dims) {
        // Calculate cell size from container and proposed dimensions
        var rect = container.getBoundingClientRect();
        cellWidth = rect.width / (dims.cols || 1);
        cellHeight = rect.height / (dims.rows || 1);
      }
    } catch (e) {
      // Fallback: estimate from font size
      cellWidth = 8.4;
      cellHeight = 17;
    }
    return JSON.stringify({ width: cellWidth, height: cellHeight });
  };

  /**
   * Focus the terminal.
   */
  window.focus = function () {
    term.focus();
  };

  /**
   * Dispose of the terminal instance.
   */
  window.dispose = function () {
    term.dispose();
  };

  // Observe container resizes and refit
  var resizeObserver = new ResizeObserver(function () {
    fitAddon.fit();
    notifyResize();
  });
  resizeObserver.observe(container);

  // Notify host that the terminal surface is ready
  try {
    window.chrome.webview.postMessage(JSON.stringify({ type: "ready" }));
  } catch (e) {
    // Not in WebView2 context (e.g., testing in browser)
  }
})();
