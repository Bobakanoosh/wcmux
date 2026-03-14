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

  // --- Pane command interception ---
  // Intercept key combos that should route to the native pane manager
  // rather than being consumed by xterm.js.
  term.attachCustomKeyEventHandler(function (e) {
    if (e.type !== "keydown") return true;

    var key = e.key;
    var ctrl = e.ctrlKey;
    var shift = e.shiftKey;
    var alt = e.altKey;

    // Ctrl+Shift combos: split, close, focus, new tab, prev tab
    if (ctrl && shift && !alt) {
      var cmd = null;
      switch (key) {
        case "H": cmd = "split-horizontal"; break;
        case "V": cmd = "split-vertical"; break;
        case "W": cmd = "close-pane"; break;
        case "T": cmd = "new-tab"; break;
        case "Tab": cmd = "prev-tab"; break;
        case "ArrowLeft": cmd = "focus-left"; break;
        case "ArrowRight": cmd = "focus-right"; break;
        case "ArrowUp": cmd = "focus-up"; break;
        case "ArrowDown": cmd = "focus-down"; break;
      }
      if (cmd) {
        e.preventDefault();
        window.chrome.webview.postMessage(JSON.stringify({ type: "command", command: cmd }));
        return false;
      }
    }

    // Ctrl-only combos: next tab, tab index switching
    if (ctrl && !shift && !alt) {
      var cmd = null;
      switch (key) {
        case "Tab": cmd = "next-tab"; break;
        case "1": cmd = "tab-1"; break;
        case "2": cmd = "tab-2"; break;
        case "3": cmd = "tab-3"; break;
        case "4": cmd = "tab-4"; break;
        case "5": cmd = "tab-5"; break;
        case "6": cmd = "tab-6"; break;
        case "7": cmd = "tab-7"; break;
        case "8": cmd = "tab-8"; break;
        case "9": cmd = "tab-9"; break;
      }
      if (cmd) {
        e.preventDefault();
        window.chrome.webview.postMessage(JSON.stringify({ type: "command", command: cmd }));
        return false;
      }
    }

    // Ctrl+Alt+Shift combos: swap panes (check before Ctrl+Alt to avoid partial match)
    if (ctrl && alt && shift) {
      var cmd = null;
      switch (key) {
        case "ArrowLeft": cmd = "swap-left"; break;
        case "ArrowRight": cmd = "swap-right"; break;
        case "ArrowUp": cmd = "swap-up"; break;
        case "ArrowDown": cmd = "swap-down"; break;
      }
      if (cmd) {
        e.preventDefault();
        window.chrome.webview.postMessage(JSON.stringify({ type: "command", command: cmd }));
        return false;
      }
    }

    // Ctrl+Alt combos: resize
    if (ctrl && alt && !shift) {
      var cmd = null;
      switch (key) {
        case "ArrowLeft": cmd = "resize-left"; break;
        case "ArrowRight": cmd = "resize-right"; break;
        case "ArrowUp": cmd = "resize-up"; break;
        case "ArrowDown": cmd = "resize-down"; break;
      }
      if (cmd) {
        e.preventDefault();
        window.chrome.webview.postMessage(JSON.stringify({ type: "command", command: cmd }));
        return false;
      }
    }

    return true;
  });

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

  // Notify native host when terminal receives focus (e.g., mouse click).
  // WebView2 captures pointer events before they reach WinUI, so the native
  // PointerPressed handler on the border never fires. This bridges the gap.
  term.textarea.addEventListener("focus", function () {
    try {
      window.chrome.webview.postMessage(JSON.stringify({ type: "command", command: "focus-pane" }));
    } catch (e) {
      // Not in WebView2 context
    }
  });

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
