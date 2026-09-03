/**
 * Management order / service-request alert audio.
 * Survives background tabs better via HTMLAudio keep-alive + retry + desktop notifications.
 */
(function (global) {
  "use strict";

  var soundStorageKey = "restaurant-os.orders.soundEnabled";
  var seenOrdersKey = "restaurant-os.orders.alertSeenIds";
  var soundEnabled = localStorage.getItem(soundStorageKey) !== "0";
  var audioCtx = null;
  var audioUnlocked = false;
  var pendingChime = null;
  var keepAliveTimer = null;
  var retryTimer = null;
  var mediaKeepAlive = null;
  var globalHub = null;
  var knownOrderIds = loadSeenIds();

  function loadSeenIds() {
    try {
      var raw = sessionStorage.getItem(seenOrdersKey);
      var list = raw ? JSON.parse(raw) : [];
      return new Set(Array.isArray(list) ? list.slice(-200) : []);
    } catch {
      return new Set();
    }
  }

  function persistSeenIds() {
    try {
      sessionStorage.setItem(seenOrdersKey, JSON.stringify(Array.from(knownOrderIds).slice(-200)));
    } catch {
      /* ignore */
    }
  }

  function chimePlan(kind) {
    if (kind === "bill") return { freqs: [523, 392, 330], gap: 0.14, type: "triangle" };
    if (kind === "waiter") return { freqs: [880, 1174, 880], gap: 0.1, type: "sine" };
    if (kind === "service") return { freqs: [988, 1319], gap: 0.11, type: "sine" };
    return { freqs: [660, 880, 990], gap: 0.12, type: "sine" };
  }

  function writeString(view, offset, text) {
    for (var i = 0; i < text.length; i++) view.setUint8(offset + i, text.charCodeAt(i));
  }

  function encodeWavPcm16(samples, sampleRate) {
    var buffer = new ArrayBuffer(44 + samples.length * 2);
    var view = new DataView(buffer);
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
    for (var i = 0; i < samples.length; i++) {
      var s = Math.max(-1, Math.min(1, samples[i]));
      view.setInt16(44 + i * 2, s < 0 ? s * 0x8000 : s * 0x7fff, true);
    }
    var bytes = new Uint8Array(buffer);
    var binary = "";
    for (var j = 0; j < bytes.length; j++) binary += String.fromCharCode(bytes[j]);
    return "data:audio/wav;base64," + btoa(binary);
  }

  function buildSilentLoopUri() {
    var sampleRate = 8000;
    var samples = new Float32Array(sampleRate);
    for (var i = 0; i < samples.length; i++) samples[i] = (i % 64 === 0 ? 0.0004 : 0);
    return encodeWavPcm16(samples, sampleRate);
  }

  function buildChimeUri(kind) {
    var plan = chimePlan(kind);
    var sampleRate = 22050;
    var toneLen = 0.28;
    var totalSec = plan.freqs.length * plan.gap + toneLen;
    var samples = new Float32Array(Math.ceil(sampleRate * totalSec));
    plan.freqs.forEach(function (freq, index) {
      var start = Math.floor((index * plan.gap) * sampleRate);
      var len = Math.floor(toneLen * sampleRate);
      for (var i = 0; i < len; i++) {
        var t = i / sampleRate;
        var env = Math.min(1, i / (0.02 * sampleRate)) * Math.exp(-3.5 * t);
        samples[start + i] += Math.sin(2 * Math.PI * freq * t) * env * 0.55;
      }
    });
    return encodeWavPcm16(samples, sampleRate);
  }

  function getAudioContext() {
    if (!audioCtx) {
      var Ctx = global.AudioContext || global.webkitAudioContext;
      if (!Ctx) return null;
      audioCtx = new Ctx();
    }
    return audioCtx;
  }

  function ensureMediaKeepAlive() {
    if (mediaKeepAlive) {
      if (mediaKeepAlive.paused) {
        mediaKeepAlive.play().catch(function () {});
      }
      return;
    }
    try {
      mediaKeepAlive = new Audio(buildSilentLoopUri());
      mediaKeepAlive.loop = true;
      mediaKeepAlive.volume = 0.001;
      mediaKeepAlive.setAttribute("playsinline", "true");
      mediaKeepAlive.play().catch(function () {});
    } catch {
      /* ignore */
    }
  }

  function ensureAudioKeepAlive() {
    var ctx = getAudioContext();
    if (!ctx || keepAliveTimer) return;
    try {
      var osc = ctx.createOscillator();
      var gain = ctx.createGain();
      gain.gain.value = 0.00001;
      osc.connect(gain);
      gain.connect(ctx.destination);
      osc.start();
    } catch {
      /* ignore */
    }
    keepAliveTimer = global.setInterval(function () {
      if (ctx.state === "suspended") ctx.resume().catch(function () {});
      ensureMediaKeepAlive();
    }, 10000);
  }

  function unlockAudio() {
    var ctx = getAudioContext();
    if (ctx && ctx.state === "suspended") {
      ctx.resume().catch(function () {});
    }
    ensureMediaKeepAlive();
    ensureAudioKeepAlive();
    audioUnlocked = true;
    emitState();
  }

  function notifyDesktop(kind, title, body) {
    if (!soundEnabled || !("Notification" in global)) return;
    if (Notification.permission !== "granted") return;
    try {
      var n = new Notification(title, {
        body: body,
        tag: "restaurant-os-" + kind,
        renotify: true,
        silent: false,
        requireInteraction: kind === "order"
      });
      global.setTimeout(function () { n.close(); }, 10000);
    } catch {
      /* ignore */
    }
  }

  function playHtmlChime(kind) {
    return new Promise(function (resolve, reject) {
      try {
        var audio = new Audio(buildChimeUri(kind));
        audio.volume = 0.9;
        var done = false;
        var finish = function (ok) {
          if (done) return;
          done = true;
          if (ok) resolve();
          else reject(new Error("html-audio-failed"));
        };
        audio.addEventListener("ended", function () { finish(true); });
        audio.addEventListener("error", function () { finish(false); });
        var p = audio.play();
        if (p && typeof p.then === "function") {
          p.then(function () { /* playing */ }).catch(function () { finish(false); });
        }
        global.setTimeout(function () { finish(true); }, 1200);
      } catch (err) {
        reject(err);
      }
    });
  }

  function playWebAudioChime(kind) {
    var ctx = getAudioContext();
    if (!ctx) return Promise.reject(new Error("no-audio-context"));

    var run = function () {
      audioUnlocked = true;
      ensureAudioKeepAlive();
      ensureMediaKeepAlive();
      var plan = chimePlan(kind);
      var now = ctx.currentTime;
      plan.freqs.forEach(function (freq, index) {
        var osc = ctx.createOscillator();
        var gain = ctx.createGain();
        osc.type = plan.type;
        osc.frequency.value = freq;
        var start = now + index * plan.gap;
        gain.gain.setValueAtTime(0.0001, start);
        gain.gain.exponentialRampToValueAtTime(0.22, start + 0.02);
        gain.gain.exponentialRampToValueAtTime(0.0001, start + 0.28);
        osc.connect(gain);
        gain.connect(ctx.destination);
        osc.start(start);
        osc.stop(start + 0.3);
      });
      emitState();
    };

    if (ctx.state === "suspended") {
      return ctx.resume().then(run);
    }
    run();
    return Promise.resolve();
  }

  function clearRetry() {
    if (retryTimer) {
      global.clearInterval(retryTimer);
      retryTimer = null;
    }
  }

  function scheduleRetry() {
    if (retryTimer || !pendingChime) return;
    retryTimer = global.setInterval(function () {
      if (!pendingChime || !soundEnabled) {
        clearRetry();
        return;
      }
      var kind = pendingChime;
      playAlertChime(kind, { force: true, fromRetry: true });
    }, 2500);
  }

  function flashOrdersNav() {
    var link = document.querySelector('.topbar nav a[href*="/Orders"]');
    if (!link) return;
    link.classList.add("nav-alert-pulse");
    global.setTimeout(function () { link.classList.remove("nav-alert-pulse"); }, 8000);
  }

  function playAlertChime(kind, options) {
    if (!soundEnabled) return;
    options = options || {};
    var force = !!options.force;
    var fromRetry = !!options.fromRetry;

    if (document.hidden && !force) {
      pendingChime = kind;
      if (kind === "order") {
        notifyDesktop("order", "Yeni sipariş", "Aktif siparişler ekranına bakın.");
        flashOrdersNav();
      } else if (kind === "bill") {
        notifyDesktop("bill", "Hesap isteği", "Müşteri hesap istedi.");
      } else {
        notifyDesktop("waiter", "Garson çağrısı", "Masadan servis çağrısı var.");
      }
      scheduleRetry();
    } else if (!fromRetry) {
      pendingChime = kind;
    }

    var markPlayed = function () {
      pendingChime = null;
      clearRetry();
      emitState();
    };

    playWebAudioChime(kind)
      .then(function () {
        if (!document.hidden || force) markPlayed();
      })
      .catch(function () {
        playHtmlChime(kind)
          .then(markPlayed)
          .catch(function () {
            pendingChime = kind;
            scheduleRetry();
            ensureMediaKeepAlive();
          });
      });

    // Parallel HTML path helps when Web Audio is muted in background tabs.
    if (document.hidden || force) {
      playHtmlChime(kind).then(function () {
        if (document.hidden) {
          /* keep pending until visible so a second audible pass can fire on focus */
        } else {
          markPlayed();
        }
      }).catch(function () {});
    }
  }

  var listeners = [];
  function emitState() {
    var state = {
      soundEnabled: soundEnabled,
      audioUnlocked: audioUnlocked,
      pending: !!pendingChime
    };
    listeners.forEach(function (fn) {
      try { fn(state); } catch { /* ignore */ }
    });
  }

  function onSoundState(fn) {
    listeners.push(fn);
    fn({ soundEnabled: soundEnabled, audioUnlocked: audioUnlocked, pending: !!pendingChime });
    return function () {
      listeners = listeners.filter(function (x) { return x !== fn; });
    };
  }

  function setSoundEnabled(enabled) {
    soundEnabled = !!enabled;
    localStorage.setItem(soundStorageKey, soundEnabled ? "1" : "0");
    if (!soundEnabled) {
      pendingChime = null;
      clearRetry();
    }
    emitState();
  }

  function isSoundEnabled() {
    return soundEnabled;
  }

  function isAudioUnlocked() {
    return audioUnlocked;
  }

  function rememberOrderId(id) {
    if (!id) return;
    knownOrderIds.add(String(id));
    persistSeenIds();
  }

  function isNewOrderAlert(order) {
    if (!order || !order.id) return false;
    var status = String(order.status || "").toLowerCase();
    if (status !== "submitted") return false;
    var id = String(order.id);
    if (knownOrderIds.has(id)) return false;
    knownOrderIds.add(id);
    persistSeenIds();
    return true;
  }

  function seedKnownOrders(ids) {
    (ids || []).forEach(function (id) {
      if (id) knownOrderIds.add(String(id));
    });
    persistSeenIds();
  }

  function startGlobalAlertHub(options) {
    if (globalHub || !options || !options.apiBase || !options.accessToken || !global.signalR) {
      return null;
    }
    if (global.__restaurantOsOrdersPageActive) {
      return null;
    }

    var connection = new signalR.HubConnectionBuilder()
      .withUrl(options.apiBase.replace(/\/$/, "") + "/hubs/v1/management-orders", {
        accessTokenFactory: function () { return options.accessToken; }
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    connection.on("orderStatusChanged", function (order) {
      if (isNewOrderAlert(order)) {
        playAlertChime("order");
        flashOrdersNav();
      }
    });

    connection.on("serviceRequestCreated", function (request) {
      var type = String((request && request.type) || "").toLowerCase();
      var kind = type === "bill" ? "bill" : type === "waiter" ? "waiter" : "service";
      playAlertChime(kind);
      flashOrdersNav();
    });

    connection.start().catch(function () {});
    globalHub = connection;
    return connection;
  }

  document.addEventListener("pointerdown", unlockAudio, { once: true });
  document.addEventListener("keydown", unlockAudio, { once: true });
  document.addEventListener("visibilitychange", function () {
    if (document.hidden) return;
    var ctx = getAudioContext();
    if (ctx && ctx.state === "suspended") ctx.resume().catch(function () {});
    ensureMediaKeepAlive();
    if (pendingChime) {
      var kind = pendingChime;
      pendingChime = null;
      clearRetry();
      playAlertChime(kind, { force: true });
    }
  });
  global.addEventListener("focus", function () {
    if (pendingChime) {
      var kind = pendingChime;
      pendingChime = null;
      clearRetry();
      playAlertChime(kind, { force: true });
    }
  });
  global.addEventListener("pageshow", function () {
    ensureMediaKeepAlive();
  });

  global.RestaurantOsAlerts = {
    play: playAlertChime,
    unlock: unlockAudio,
    setSoundEnabled: setSoundEnabled,
    isSoundEnabled: isSoundEnabled,
    isAudioUnlocked: isAudioUnlocked,
    onSoundState: onSoundState,
    rememberOrderId: rememberOrderId,
    seedKnownOrders: seedKnownOrders,
    isNewOrderAlert: isNewOrderAlert,
    startGlobalAlertHub: startGlobalAlertHub,
    requestNotificationPermission: function () {
      if (!("Notification" in global) || Notification.permission !== "default") {
        return Promise.resolve(Notification.permission);
      }
      return Notification.requestPermission().catch(function () { return "denied"; });
    }
  };
})(window);
