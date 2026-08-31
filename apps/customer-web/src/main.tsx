import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { App } from "./app/App";
import { AppErrorBoundary } from "./app/AppErrorBoundary";
import { ensureCryptoPolyfill } from "./lib/ensureCryptoPolyfill";
import "./styles.css";

ensureCryptoPolyfill();

const root = document.getElementById("root");

function renderBootFailure(message: string) {
  if (!root) return;
  root.innerHTML = `
    <main style="min-height:100dvh;padding:2rem;font-family:system-ui,sans-serif;background:#f8f5ef;color:#21170f;">
      <h1 style="margin:0 0 1rem;font-size:1.5rem;">Menü açılamadı</h1>
      <p style="margin:0 0 1rem;line-height:1.6;color:#74675d;">Uygulama başlatılırken bir hata oluştu. Bağlantınızı kontrol edip sayfayı yenileyin.</p>
      <p style="margin:0 0 1.5rem;font-size:0.875rem;color:#a32d2d;">${message}</p>
      <button type="button" onclick="location.reload()" style="min-height:2.75rem;padding:0.75rem 1.25rem;border:0;border-radius:0.625rem;background:#a8461e;color:#fff;font-weight:700;">Tekrar dene</button>
    </main>
  `;
}

if (!root) {
  throw new Error("Application root was not found.");
}

try {
  document.getElementById("boot-fallback")?.remove();
  createRoot(root).render(
    <StrictMode>
      <AppErrorBoundary>
        <App />
      </AppErrorBoundary>
    </StrictMode>,
  );
} catch (error) {
  const message = error instanceof Error ? error.message : "Bilinmeyen başlatma hatası";
  console.error("Customer web boot failed", error);
  renderBootFailure(message);
}

window.addEventListener("error", (event) => {
  console.error("Customer web runtime error", event.error ?? event.message);
});

window.addEventListener("unhandledrejection", (event) => {
  console.error("Customer web unhandled rejection", event.reason);
});
