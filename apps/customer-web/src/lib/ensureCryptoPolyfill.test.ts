import { describe, expect, it } from "vitest";
import { createClientId } from "./createClientId";
import { ensureCryptoPolyfill } from "./ensureCryptoPolyfill";

describe("ensureCryptoPolyfill", () => {
  it("does not throw when crypto is missing", () => {
    const original = globalThis.crypto;
    Object.defineProperty(globalThis, "crypto", {
      configurable: true,
      value: undefined,
    });

    expect(() => ensureCryptoPolyfill()).not.toThrow();
    expect(typeof globalThis.crypto?.randomUUID).toBe("function");
    expect(globalThis.crypto!.randomUUID()).toMatch(
      /^[0-9a-f-]{36}$|^id-[0-9a-f]+-[0-9a-f]+$/,
    );

    Object.defineProperty(globalThis, "crypto", {
      configurable: true,
      value: original,
    });
  });

  it("keeps createClientId usable on insecure contexts", () => {
    expect(createClientId()).toMatch(/^[0-9a-f-]{36}$|^id-[0-9a-f]+-[0-9a-f]+$/);
  });
});
