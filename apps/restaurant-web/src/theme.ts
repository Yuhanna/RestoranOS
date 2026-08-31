export type ThemePreference = "system" | "light" | "dark" | "soft";
export type ResolvedTheme = "light" | "dark" | "soft";

const STORAGE_KEY = "restaurantos-theme";
const PREFERENCES: ThemePreference[] = ["system", "light", "dark", "soft"];

export const THEME_OPTIONS: { value: ThemePreference; label: string }[] = [
  { value: "system", label: "Sistem" },
  { value: "light", label: "Açık" },
  { value: "dark", label: "Koyu" },
  { value: "soft", label: "Yumuşak koyu" },
];

export function readThemePreference(): ThemePreference {
  try {
    const stored = localStorage.getItem(STORAGE_KEY);
    return PREFERENCES.includes(stored as ThemePreference) ? (stored as ThemePreference) : "system";
  } catch {
    return "system";
  }
}

export function writeThemePreference(preference: ThemePreference) {
  try {
    localStorage.setItem(STORAGE_KEY, preference);
  } catch {
    /* ignore */
  }
}

export function resolveTheme(preference: ThemePreference): ResolvedTheme {
  if (preference === "system") {
    return window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
  }
  return preference;
}

export function applyTheme(preference: ThemePreference) {
  const resolved = resolveTheme(preference);
  document.documentElement.dataset.theme = resolved;
  document.documentElement.dataset.themePreference = preference;
}

export function bootstrapTheme() {
  applyTheme(readThemePreference());
}

export function watchSystemTheme(onChange: () => void) {
  const media = window.matchMedia("(prefers-color-scheme: dark)");
  const handler = () => {
    if (readThemePreference() === "system") {
      onChange();
    }
  };
  media.addEventListener("change", handler);
  return () => media.removeEventListener("change", handler);
}
