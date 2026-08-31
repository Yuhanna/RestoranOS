(function () {
  const STORAGE_KEY = "restaurantos-theme";
  const THEMES = ["system", "light", "dark", "soft"];

  function resolveTheme(preference) {
    if (preference === "system") {
      return window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
    }
    return THEMES.includes(preference) ? preference : "system";
  }

  function applyTheme(preference) {
    const resolved = resolveTheme(preference);
    document.documentElement.dataset.theme = resolved;
    document.documentElement.dataset.themePreference = preference;
  }

  function readPreference() {
    try {
      const stored = localStorage.getItem(STORAGE_KEY);
      return THEMES.includes(stored) ? stored : "system";
    } catch {
      return "system";
    }
  }

  function writePreference(preference) {
    try {
      localStorage.setItem(STORAGE_KEY, preference);
    } catch {
      /* ignore quota errors */
    }
  }

  // Early boot (also invoked from layout inline script).
  window.RestaurantOsTheme = {
    apply: applyTheme,
    read: readPreference,
    write: writePreference,
    resolve: resolveTheme,
  };

  applyTheme(readPreference());

  window.addEventListener("DOMContentLoaded", function () {
    const select = document.getElementById("theme-select");
    if (!select) return;

    select.value = readPreference();
    select.addEventListener("change", function () {
      const preference = select.value;
      writePreference(preference);
      applyTheme(preference);
    });
  });

  window
    .matchMedia("(prefers-color-scheme: dark)")
    .addEventListener("change", function () {
      if (readPreference() === "system") {
        applyTheme("system");
      }
    });
})();
