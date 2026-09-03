export type AlertKind = "order" | "bill" | "waiter" | "service";

const soundStorageKey = "restaurant-os.sound-enabled";

let audioContext: AudioContext | null = null;
let audioUnlocked = false;
let keepAliveTimer: number | undefined;
let retryTimer: number | undefined;
let pendingChime: AlertKind | null = null;
let mediaKeepAlive: HTMLAudioElement | null = null;
let soundEnabled = localStorage.getItem(soundStorageKey) !== "0";

const chimePlan = (kind: AlertKind) => {
  if (kind === "bill") return { freqs: [523, 392, 330], gap: 0.14, type: "triangle" as OscillatorType };
  if (kind === "waiter") return { freqs: [880, 1174, 880], gap: 0.1, type: "sine" as OscillatorType };
  if (kind === "service") return { freqs: [988, 1319], gap: 0.11, type: "sine" as OscillatorType };
  return { freqs: [660, 880, 990], gap: 0.12, type: "sine" as OscillatorType };
};

const writeString = (view: DataView, offset: number, text: string) => {
  for (let i = 0; i < text.length; i++) view.setUint8(offset + i, text.charCodeAt(i));
};

const encodeWavPcm16 = (samples: Float32Array, sampleRate: number) => {
  const buffer = new ArrayBuffer(44 + samples.length * 2);
  const view = new DataView(buffer);
  writeString(view, 0, "RIFF");
  view.setUint32(4, 36 + samples.length * 2, true);
  writeString(view, 8, "WAVE");
  writeString(view, 12, "fmt ");
  view.setUint32(16, 16, true);
  view.setUint16(20, 1, true);
  view.setUint16(22, 1, true);
  view.setUint32(24, sampleRate, true);
  view.setUint32(28, sampleRate * 2, true);
  view.setUint16(32, 2, true);
  view.setUint16(34, 16, true);
  writeString(view, 36, "data");
  view.setUint32(40, samples.length * 2, true);
  for (let i = 0; i < samples.length; i++) {
    const s = Math.max(-1, Math.min(1, samples[i] ?? 0));
    view.setInt16(44 + i * 2, s < 0 ? s * 0x8000 : s * 0x7fff, true);
  }
  const bytes = new Uint8Array(buffer);
  let binary = "";
  for (let j = 0; j < bytes.length; j++) binary += String.fromCharCode(bytes[j] ?? 0);
  return `data:audio/wav;base64,${btoa(binary)}`;
};

const buildSilentLoopUri = () => {
  const sampleRate = 8000;
  const samples = new Float32Array(sampleRate);
  for (let i = 0; i < samples.length; i++) samples[i] = i % 64 === 0 ? 0.0004 : 0;
  return encodeWavPcm16(samples, sampleRate);
};

const buildChimeUri = (kind: AlertKind) => {
  const plan = chimePlan(kind);
  const sampleRate = 22050;
  const toneLen = 0.28;
  const totalSec = plan.freqs.length * plan.gap + toneLen;
  const samples = new Float32Array(Math.ceil(sampleRate * totalSec));
  plan.freqs.forEach((freq, index) => {
    const start = Math.floor(index * plan.gap * sampleRate);
    const len = Math.floor(toneLen * sampleRate);
    for (let i = 0; i < len; i++) {
      const t = i / sampleRate;
      const env = Math.min(1, i / (0.02 * sampleRate)) * Math.exp(-3.5 * t);
      samples[start + i] = (samples[start + i] ?? 0) + Math.sin(2 * Math.PI * freq * t) * env * 0.55;
    }
  });
  return encodeWavPcm16(samples, sampleRate);
};

const getAudioContext = () => {
  if (typeof window === "undefined") return null;
  if (!audioContext) {
    const Ctx = window.AudioContext ?? (window as typeof window & { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
    if (!Ctx) return null;
    audioContext = new Ctx();
  }
  return audioContext;
};

const ensureMediaKeepAlive = () => {
  if (typeof window === "undefined") return;
  if (mediaKeepAlive) {
    if (mediaKeepAlive.paused) void mediaKeepAlive.play().catch(() => undefined);
    return;
  }
  try {
    mediaKeepAlive = new Audio(buildSilentLoopUri());
    mediaKeepAlive.loop = true;
    mediaKeepAlive.volume = 0.001;
    mediaKeepAlive.setAttribute("playsinline", "true");
    void mediaKeepAlive.play().catch(() => undefined);
  } catch {
    /* ignore */
  }
};

const ensureAudioKeepAlive = () => {
  const ctx = getAudioContext();
  if (!ctx || keepAliveTimer) return;
  try {
    const osc = ctx.createOscillator();
    const gain = ctx.createGain();
    gain.gain.value = 0.00001;
    osc.connect(gain);
    gain.connect(ctx.destination);
    osc.start();
  } catch {
    /* ignore */
  }
  keepAliveTimer = window.setInterval(() => {
    if (ctx.state === "suspended") void ctx.resume();
    ensureMediaKeepAlive();
  }, 10_000);
};

const clearRetry = () => {
  if (retryTimer) {
    window.clearInterval(retryTimer);
    retryTimer = undefined;
  }
};

const scheduleRetry = () => {
  if (retryTimer || !pendingChime) return;
  retryTimer = window.setInterval(() => {
    if (!pendingChime || !soundEnabled) {
      clearRetry();
      return;
    }
    playAlertChime(pendingChime, { force: true, fromRetry: true });
  }, 2500);
};

const playHtmlChime = (kind: AlertKind) =>
  new Promise<void>((resolve, reject) => {
    try {
      const audio = new Audio(buildChimeUri(kind));
      audio.volume = 0.9;
      let done = false;
      const finish = (ok: boolean) => {
        if (done) return;
        done = true;
        if (ok) resolve();
        else reject(new Error("html-audio-failed"));
      };
      audio.addEventListener("ended", () => finish(true));
      audio.addEventListener("error", () => finish(false));
      void audio.play().catch(() => finish(false));
      window.setTimeout(() => finish(true), 1200);
    } catch (error) {
      reject(error);
    }
  });

const playWebAudioChime = (kind: AlertKind) => {
  const ctx = getAudioContext();
  if (!ctx) return Promise.reject(new Error("no-audio-context"));

  const run = () => {
    audioUnlocked = true;
    ensureAudioKeepAlive();
    ensureMediaKeepAlive();
    const plan = chimePlan(kind);
    const now = ctx.currentTime;
    plan.freqs.forEach((freq, index) => {
      const osc = ctx.createOscillator();
      const gain = ctx.createGain();
      osc.type = plan.type;
      osc.frequency.value = freq;
      const start = now + index * plan.gap;
      gain.gain.setValueAtTime(0.0001, start);
      gain.gain.exponentialRampToValueAtTime(0.22, start + 0.02);
      gain.gain.exponentialRampToValueAtTime(0.0001, start + 0.28);
      osc.connect(gain);
      gain.connect(ctx.destination);
      osc.start(start);
      osc.stop(start + 0.3);
    });
  };

  if (ctx.state === "suspended") return ctx.resume().then(run);
  run();
  return Promise.resolve();
};

export const isSoundEnabled = () => soundEnabled;

export const setSoundEnabled = (enabled: boolean) => {
  soundEnabled = enabled;
  localStorage.setItem(soundStorageKey, enabled ? "1" : "0");
  if (!enabled) {
    pendingChime = null;
    clearRetry();
  }
};

export const unlockAudio = () => {
  const ctx = getAudioContext();
  if (ctx?.state === "suspended") void ctx.resume();
  ensureMediaKeepAlive();
  ensureAudioKeepAlive();
  audioUnlocked = true;
};

export const requestNotificationPermission = async () => {
  if (!("Notification" in window) || Notification.permission !== "default") return;
  try {
    await Notification.requestPermission();
  } catch {
    /* ignore */
  }
};

const notifyDesktop = (kind: AlertKind, title: string, body: string) => {
  if (!soundEnabled || !("Notification" in window) || Notification.permission !== "granted") return;
  try {
    const notification = new Notification(title, {
      body,
      tag: `restaurant-os-${kind}`,
      silent: false,
      requireInteraction: kind === "order",
    });
    window.setTimeout(() => notification.close(), 10_000);
  } catch {
    /* ignore */
  }
};

export const playAlertChime = (kind: AlertKind, options?: { force?: boolean; fromRetry?: boolean }) => {
  if (!soundEnabled) return;
  const force = options?.force ?? false;
  const fromRetry = options?.fromRetry ?? false;

  if (document.hidden && !force) {
    pendingChime = kind;
    if (kind === "order") notifyDesktop("order", "Yeni sipariş", "Operasyon ekranına bakın.");
    else if (kind === "bill") notifyDesktop("bill", "Hesap isteği", "Müşteri hesap istedi.");
    else notifyDesktop("waiter", "Garson çağrısı", "Masadan servis çağrısı var.");
    scheduleRetry();
  } else if (!fromRetry) {
    pendingChime = kind;
  }

  const markPlayed = () => {
    pendingChime = null;
    clearRetry();
  };

  void playWebAudioChime(kind)
    .then(() => {
      if (!document.hidden || force) markPlayed();
    })
    .catch(() => {
      void playHtmlChime(kind)
        .then(markPlayed)
        .catch(() => {
          pendingChime = kind;
          scheduleRetry();
          ensureMediaKeepAlive();
        });
    });

  if (document.hidden || force) {
    void playHtmlChime(kind)
      .then(() => {
        if (!document.hidden) markPlayed();
      })
      .catch(() => undefined);
  }
};

export const bindBackgroundAlertRecovery = () => {
  const onVisibility = () => {
    if (!document.hidden && pendingChime) {
      const kind = pendingChime;
      pendingChime = null;
      clearRetry();
      playAlertChime(kind, { force: true });
    }
    const ctx = getAudioContext();
    if (ctx && ctx.state === "suspended") void ctx.resume();
    ensureMediaKeepAlive();
  };

  const onFocus = () => {
    if (pendingChime) {
      const kind = pendingChime;
      pendingChime = null;
      clearRetry();
      playAlertChime(kind, { force: true });
    }
  };

  const onPointer = () => unlockAudio();
  document.addEventListener("visibilitychange", onVisibility);
  window.addEventListener("focus", onFocus);
  document.addEventListener("pointerdown", onPointer, { once: true });
  document.addEventListener("keydown", onPointer, { once: true });

  return () => {
    document.removeEventListener("visibilitychange", onVisibility);
    window.removeEventListener("focus", onFocus);
  };
};

export const serviceRequestAlertKind = (type: string): AlertKind =>
  type === "bill" ? "bill" : type === "waiter" ? "waiter" : "service";

export const notifyAudience = (notification: { title: string; body: string }) => {
  if (!("Notification" in window) || Notification.permission !== "granted") return;
  try {
    const desktop = new Notification(notification.title, {
      body: notification.body,
      tag: "restaurant-os-audience",
      silent: false,
    });
    window.setTimeout(() => desktop.close(), 12_000);
  } catch {
    /* ignore */
  }
};
