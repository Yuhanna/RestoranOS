(function () {
  var storageKey = "pasa-upgrade-dismissed";

  function readMap() {
    try {
      return JSON.parse(localStorage.getItem(storageKey) || "{}") || {};
    } catch (_) {
      return {};
    }
  }

  function writeMap(map) {
    try {
      localStorage.setItem(storageKey, JSON.stringify(map));
    } catch (_) { }
  }

  function isDismissed(featureKey) {
    if (!featureKey) return false;
    var map = readMap();
    var until = map[featureKey];
    if (!until) return false;
    if (Date.now() > until) {
      delete map[featureKey];
      writeMap(map);
      return false;
    }
    return true;
  }

  function dismiss(featureKey) {
    if (!featureKey) return;
    var map = readMap();
    // 7 gün hatırla — rahatsız etmeme politikası
    map[featureKey] = Date.now() + 7 * 24 * 60 * 60 * 1000;
    writeMap(map);
  }

  function ensureDialog() {
    var dialog = document.querySelector("[data-upgrade-dialog]");
    if (dialog) return dialog;
    dialog = document.createElement("dialog");
    dialog.className = "upgrade-dialog";
    dialog.setAttribute("data-upgrade-dialog", "");
    dialog.innerHTML =
      '<div class="upgrade-dialog__panel" data-upgrade-dialog-panel></div>' +
      '<form method="dialog" class="upgrade-dialog__backdrop-form">' +
      '<button type="submit" class="upgrade-dialog__scrim" aria-label="Kapat"></button>' +
      "</form>";
    document.body.appendChild(dialog);
    return dialog;
  }

  function closeUpgradeDialog() {
    var dialog = document.querySelector("[data-upgrade-dialog]");
    if (dialog && typeof dialog.close === "function" && dialog.open) {
      dialog.close();
    }
  }

  function openUpgradeDialog(featureKey) {
    if (!featureKey) return false;
    var tpl = document.querySelector(
      'template[data-upgrade-template="' + featureKey + '"]'
    );
    if (!tpl) return false;

    var dialog = ensureDialog();
    var panel = dialog.querySelector("[data-upgrade-dialog-panel]");
    if (!panel) return false;

    panel.replaceChildren(tpl.content.cloneNode(true));

    panel.querySelectorAll("[data-upgrade-dialog-close]").forEach(function (btn) {
      btn.addEventListener("click", function () {
        closeUpgradeDialog();
      });
    });

    if (typeof dialog.showModal === "function") {
      dialog.showModal();
    } else {
      dialog.setAttribute("open", "");
    }
    return true;
  }

  function revealInlinePrompt(featureKey) {
    var prompt = document.querySelector(
      '[data-upgrade-prompt][data-feature-key="' + featureKey + '"]'
    );
    if (!prompt) return false;
    prompt.hidden = false;
    prompt.classList.add("upgrade-prompt--spotlight");
    prompt.scrollIntoView({ behavior: "smooth", block: "nearest" });
    window.setTimeout(function () {
      prompt.classList.remove("upgrade-prompt--spotlight");
    }, 1600);
    return true;
  }

  /** SoftDiscover: locked control → dialog (preferred) or spotlight banner. */
  function showUpgrade(featureKey) {
    if (openUpgradeDialog(featureKey)) return true;
    return revealInlinePrompt(featureKey);
  }

  document.querySelectorAll("[data-upgrade-prompt]").forEach(function (el) {
    var key = el.getAttribute("data-feature-key") || "";
    if (isDismissed(key)) {
      el.hidden = true;
      return;
    }
    var dismissBtn = el.querySelector("[data-upgrade-dismiss]");
    if (dismissBtn) {
      dismissBtn.addEventListener("click", function () {
        dismiss(key);
        el.hidden = true;
      });
    }
  });

  document.addEventListener("click", function (event) {
    var trigger = event.target.closest("[data-upgrade-trigger]");
    if (!trigger) return;

    var key =
      trigger.getAttribute("data-upgrade-trigger") ||
      trigger.getAttribute("data-feature-key") ||
      "";
    if (!key) return;

    // Prevent locked links/buttons from navigating; allow bubble for preview handlers.
    if (trigger.matches("a, button")) {
      event.preventDefault();
    }
    showUpgrade(key);
  });

  window.PasaUpgrade = {
    show: showUpgrade,
    dismiss: dismiss,
    closeDialog: closeUpgradeDialog,
  };
})();
