import react from "@vitejs/plugin-react";
import { defineConfig } from "vitest/config";

export default defineConfig({
  plugins: [react()],
  server: {
    host: true,
    port: 5175,
    proxy: {
      "/api": {
        target: process.env.VITE_DEV_API_PROXY_TARGET ?? "http://127.0.0.1:5183",
        changeOrigin: true,
      },
    },
  },
  test: {
    environment: "jsdom",
    setupFiles: "./src/test/setup.ts",
    css: true,
    globals: true,
  },
});
