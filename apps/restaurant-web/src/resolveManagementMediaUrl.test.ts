import { describe, expect, it } from "vitest";
import { resolveManagementMediaUrl } from "./resolveManagementMediaUrl";

describe("resolveManagementMediaUrl", () => {
  it("keeps relative media paths on same-origin (empty API base)", () => {
    expect(resolveManagementMediaUrl("/media/brand/logo.svg")).toBe("/media/brand/logo.svg");
  });

  it("prefixes relative media paths with the management API origin", () => {
    expect(resolveManagementMediaUrl("/media/brand/logo.svg", "https://api.pasa.app")).toBe(
      "https://api.pasa.app/media/brand/logo.svg",
    );
  });

  it("rewrites absolute API media URLs onto the configured origin", () => {
    expect(
      resolveManagementMediaUrl("http://127.0.0.1:5183/media/brand/logo.svg", "https://api.pasa.app"),
    ).toBe("https://api.pasa.app/media/brand/logo.svg");
  });

  it("leaves non-media absolute URLs unchanged", () => {
    expect(resolveManagementMediaUrl("https://cdn.example/logo.svg", "https://api.pasa.app")).toBe(
      "https://cdn.example/logo.svg",
    );
  });
});
