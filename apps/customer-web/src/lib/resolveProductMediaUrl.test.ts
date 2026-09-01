import { describe, expect, it } from "vitest";
import { resolveProductMediaUrl } from "./resolveProductMediaUrl";

describe("resolveProductMediaUrl", () => {
  it("keeps relative /media paths", () => {
    expect(resolveProductMediaUrl("/media/menu-items/abc.jpg")).toBe("/media/menu-items/abc.jpg");
  });

  it("strips localhost host from absolute media URLs", () => {
    expect(resolveProductMediaUrl("http://127.0.0.1:5183/media/menu-items/abc.jpg")).toBe(
      "/media/menu-items/abc.jpg",
    );
  });
});
