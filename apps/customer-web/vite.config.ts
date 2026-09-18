import react from "@vitejs/plugin-react";
import { defineConfig } from "vitest/config";

const apiProxyTarget = process.env.VITE_DEV_API_PROXY_TARGET ?? "http://127.0.0.1:5183";
// Phone LAN testing: HMR websockets leave CLOSE_WAIT sockets and make
// http://<pc-ip>:5173 hang or time out ("menü uzun süre gelmiyor").
const enableHmr = process.env.VITE_ENABLE_HMR === "1";

export default defineConfig({
  plugins: [react()],
  server: {
    // Explicit IPv4 bind — `true`/`::` alone is flaky for Android phones on Windows.
    host: "0.0.0.0",
    port: 5173,
    strictPort: true,
    hmr: enableHmr ? { clientPort: 5173 } : false,
    proxy: {
      "/api": {
        target: apiProxyTarget,
        changeOrigin: true,
      },
      "/hubs": {
        target: apiProxyTarget,
        changeOrigin: true,
        ws: true,
      },
      "/media": {
        target: apiProxyTarget,
        changeOrigin: true,
      },
    },
  },
  optimizeDeps: {
    include: ["react", "react-dom", "@microsoft/signalr"],
  },
  test: {
    environment: "jsdom",
    setupFiles: "./src/test/setup.ts",
    css: true,
  },
});
