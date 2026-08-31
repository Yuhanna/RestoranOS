import { afterEach, describe, expect, it, vi } from "vitest";
import { applyTheme, readThemePreference, resolveTheme, writeThemePreference } from "./theme";

describe("theme", () => {
  afterEach(() => {
    localStorage.clear();
    delete document.documentElement.dataset.theme;
    delete document.documentElement.dataset.themePreference;
    vi.unstubAllGlobals();
  });

  it("resolves system preference from matchMedia", () => {
    vi.stubGlobal("matchMedia", vi.fn().mockReturnValue({ matches: false }));

    expect(resolveTheme("system")).toBe("light");
  });

  it("persists and applies explicit themes", () => {
    writeThemePreference("light");
    expect(readThemePreference()).toBe("light");

    applyTheme("soft");
    expect(document.documentElement.dataset.theme).toBe("soft");
    expect(document.documentElement.dataset.themePreference).toBe("soft");
  });
});
