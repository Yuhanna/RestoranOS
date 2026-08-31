import { createClientId } from "./createClientId";

/** HTTP (LAN) and older mobile browsers may lack crypto.randomUUID. Never throw on boot. */
export function ensureCryptoPolyfill() {
  if (typeof globalThis.crypto?.randomUUID === "function") {
    return;
  }

  const randomUuid = () => createClientId();

  if (globalThis.crypto) {
    Object.defineProperty(globalThis.crypto, "randomUUID", {
      configurable: true,
      value: randomUuid,
    });
    return;
  }

  Object.defineProperty(globalThis, "crypto", {
    configurable: true,
    value: {
      randomUUID: randomUuid,
      getRandomValues: (array: Uint8Array) => {
        for (let index = 0; index < array.length; index += 1) {
          array[index] = Math.floor(Math.random() * 256);
        }
        return array;
      },
    },
  });
}
