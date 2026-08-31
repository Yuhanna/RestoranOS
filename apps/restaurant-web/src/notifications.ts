export type AlertKind = "order" | "bill" | "waiter" | "service";

const soundStorageKey = "restaurant-os.sound-enabled";

let audioContext: AudioContext | null = null;
let audioUnlocked = false;
let keepAliveTimer: number | undefined;
let pendingChime: AlertKind | null = null;
let soundEnabled = localStorage.getItem(soundStorageKey) !== "0";

const chimePlan = (kind: AlertKind) => {
  if (kind === "bill") return { freqs: [523, 392, 330], gap: 0.14, type: "triangle" as OscillatorType };
  if (kind === "waiter") return { freqs: [880, 1174, 880], gap: 0.1, type: "sine" as OscillatorType };
  if (kind === "service") return { freqs: [988, 1319], gap: 0.11, type: "sine" as OscillatorType };
  return { freqs: [660, 880, 990], gap: 0.12, type: "sine" as OscillatorType };
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

const ensureAudioKeepAlive = () => {
  const ctx = getAudioContext();
  if (!ctx || keepAliveTimer) return;
  const osc = ctx.createOscillator();
  const gain = ctx.createGain();
  gain.gain.value = 0.00001;
  osc.connect(gain);
  gain.connect(ctx.destination);
  osc.start();
  keepAliveTimer = window.setInterval(() => {
    if (ctx.state === "suspended") void ctx.resume();
  }, 15_000);
};

export const isSoundEnabled = () => soundEnabled;

export const setSoundEnabled = (enabled: boolean) => {
  soundEnabled = enabled;
  localStorage.setItem(soundStorageKey, enabled ? "1" : "0");
};

export const unlockAudio = () => {
  const ctx = getAudioContext();
  if (!ctx || audioUnlocked) return;
  void ctx.resume().then(() => {
    audioUnlocked = true;
    ensureAudioKeepAlive();
  });
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
    });
    window.setTimeout(() => notification.close(), 8_000);
  } catch {
    /* ignore */
  }
};

export const playAlertChime = (kind: AlertKind, options?: { force?: boolean }) => {
  if (!soundEnabled) return;
  const force = options?.force ?? false;

  if (document.hidden && !force) {
    pendingChime = kind;
    if (kind === "order") notifyDesktop("order", "Yeni sipariş", "Operasyon ekranına bakın.");
    else if (kind === "bill") notifyDesktop("bill", "Hesap isteği", "Müşteri hesap istedi.");
    else notifyDesktop("waiter", "Garson çağrısı", "Masadan servis çağrısı var.");
  }

  const ctx = getAudioContext();
  if (!ctx) return;

  const run = () => {
    audioUnlocked = true;
    ensureAudioKeepAlive();
    const plan = chimePlan(kind);
    const now = ctx.currentTime;
    plan.freqs.forEach((freq, index) => {
      const osc = ctx.createOscillator();
      const gain = ctx.createGain();
      osc.type = plan.type;
      osc.frequency.value = freq;
      const start = now + index * plan.gap;
      gain.gain.setValueAtTime(0.0001, start);
      gain.gain.exponentialRampToValueAtTime(0.2, start + 0.02);
      gain.gain.exponentialRampToValueAtTime(0.0001, start + 0.28);
      osc.connect(gain);
      gain.connect(ctx.destination);
      osc.start(start);
      osc.stop(start + 0.3);
    });
  };

  if (ctx.state === "suspended") {
    void ctx.resume().then(run).catch(() => {
      pendingChime = kind;
    });
    return;
  }

  run();
};

export const bindBackgroundAlertRecovery = () => {
  const onVisibility = () => {
    if (!document.hidden && pendingChime) {
      const kind = pendingChime;
      pendingChime = null;
      playAlertChime(kind, { force: true });
    }
    const ctx = getAudioContext();
    if (ctx && ctx.state === "suspended") void ctx.resume();
  };

  const onPointer = () => unlockAudio();
  document.addEventListener("visibilitychange", onVisibility);
  document.addEventListener("pointerdown", onPointer, { once: true });
  document.addEventListener("keydown", onPointer, { once: true });

  return () => {
    document.removeEventListener("visibilitychange", onVisibility);
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
