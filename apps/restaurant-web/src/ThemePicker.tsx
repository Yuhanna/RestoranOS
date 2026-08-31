import { useEffect, useState } from "react";
import {
  applyTheme,
  readThemePreference,
  THEME_OPTIONS,
  type ThemePreference,
  watchSystemTheme,
  writeThemePreference,
} from "./theme";

export function ThemePicker() {
  const [preference, setPreference] = useState<ThemePreference>(() => readThemePreference());

  useEffect(() => {
    applyTheme(preference);
    writeThemePreference(preference);
  }, [preference]);

  useEffect(
    () =>
      watchSystemTheme(() => {
        applyTheme("system");
      }),
    [],
  );

  return (
    <label className="theme-picker">
      <span className="theme-picker__label">Tema</span>
      <select
        aria-label="Tema seçin"
        value={preference}
        onChange={(event) => setPreference(event.target.value as ThemePreference)}
      >
        {THEME_OPTIONS.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
    </label>
  );
}
