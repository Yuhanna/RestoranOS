import { useState, useEffect, useCallback, useRef } from "react";
import { INITIAL_ORDERS, STATION_META, type KDSOrder, type KDSStatus, type StationId } from "./kds/data";

/* ─── Tokens ─── */
const BG = "#000000";
const SURFACE = "#0E0E0E";
const CARD = "#111111";
const CARD_RAISED = "#161616";
const BORDER = "#1C1C1C";
const BORDER_MID = "#242424";
const TEXT = "#F5F5F4";
const TEXT_MID = "#A8A29E";
const TEXT_DIM = "#57534E";

const STATUS = {
  new:       { label: "NEW",       color: "#3B82F6", bg: "rgba(59,130,246,0.12)", border: "rgba(59,130,246,0.3)" },
  preparing: { label: "FIRING",    color: "#F5F5F4", bg: "transparent",           border: BORDER_MID },
  ready:     { label: "UP",        color: "#22C55E", bg: "rgba(34,197,94,0.1)",   border: "rgba(34,197,94,0.35)" },
  delayed:   { label: "LATE",      color: "#EF4444", bg: "rgba(239,68,68,0.08)",  border: "rgba(239,68,68,0.4)" },
} as const;

/* ─── Helpers ─── */
function fmtClock(totalSec: number): string {
  const abs = Math.abs(totalSec);
  const m = Math.floor(abs / 60);
  const s = abs % 60;
  return `${String(m).padStart(2, "0")}:${String(s).padStart(2, "0")}`;
}

function fmtElapsed(sec: number): string {
  if (sec < 60) return `${sec}s`;
  return `${Math.floor(sec / 60)}m ${sec % 60}s`;
}

function elapsedColor(sec: number): string {
  if (sec > 900) return "#EF4444";
  if (sec > 540) return "#F97316";
  if (sec > 300) return "#F59E0B";
  return TEXT;
}

function etaColor(etaSec: number): string {
  if (etaSec < 0) return "#EF4444";
  if (etaSec < 120) return "#F97316";
  if (etaSec < 300) return "#F59E0B";
  return "#22C55E";
}

function prioritySort(orders: KDSOrder[]): KDSOrder[] {
  const rank: Record<KDSStatus, number> = { delayed: 0, new: 1, preparing: 2, ready: 3 };
  return [...orders].sort((a, b) => {
    const rd = rank[a.status] - rank[b.status];
    if (rd !== 0) return rd;
    if (a.priority === "vip" && b.priority !== "vip") return -1;
    if (b.priority === "vip" && a.priority !== "vip") return 1;
    return b.placedSecondsAgo - a.placedSecondsAgo;
  });
}

/* ─── Live clock ─── */
function useLiveClock() {
  const [now, setNow] = useState(() => new Date());
  useEffect(() => {
    const t = setInterval(() => setNow(new Date()), 1000);
    return () => clearInterval(t);
  }, []);
  return now;
}

/* ─── Tick elapsed ─── */
function useTickedOrders(initial: KDSOrder[]) {
  const [orders, setOrders] = useState<KDSOrder[]>(initial);
  const timerRef = useRef<ReturnType<typeof setInterval> | null>(null);

  useEffect(() => {
    timerRef.current = setInterval(() => {
      setOrders(prev =>
        prev.map(o => {
          if (o.status === "ready") return o;
          const newElapsed = o.placedSecondsAgo + 1;
          const newEta = o.etaSeconds - 1;
          let newStatus = o.status;
          if (o.status === "preparing" && newEta < 0) newStatus = "delayed";
          if (o.status === "delayed") newStatus = "delayed";
          return { ...o, placedSecondsAgo: newElapsed, etaSeconds: newEta, status: newStatus };
        })
      );
    }, 1000);
    return () => { if (timerRef.current) clearInterval(timerRef.current); };
  }, []);

  return [orders, setOrders] as const;
}

/* ════════════════════════════════════════════════
   Order Card
════════════════════════════════════════════════ */
function OrderCard({ order, onBump, onRecall, recalled }: {
  order: KDSOrder;
  onBump: (id: string) => void;
  onRecall?: (id: string) => void;
  recalled?: boolean;
}) {
  const [bumping, setBumping] = useState(false);
  const st = STATUS[order.status];
  const stationMeta = STATION_META[order.station];
  const isNew = order.status === "new";
  const isReady = order.status === "ready";
  const isDelayed = order.status === "delayed";

  const handleBump = () => {
    setBumping(true);
    setTimeout(() => { onBump(order.id); setBumping(false); }, 120);
  };

  const bumpLabel = isNew ? "START" : isReady ? "SERVED ✓" : "MARK READY";
  const bumpBg = isReady ? "#16A34A" : isNew ? "#1D4ED8" : "#27272A";
  const bumpColor = isReady ? "#FFFFFF" : isNew ? "#FFFFFF" : TEXT_MID;

  return (
    <div style={{
      background: recalled ? "rgba(255,255,255,0.02)" : CARD,
      border: `1px solid ${st.border}`,
      borderLeft: `3px solid ${isDelayed ? "#EF4444" : isReady ? "#22C55E" : isNew ? "#3B82F6" : stationMeta.color}`,
      borderRadius: 10,
      display: "flex", flexDirection: "column",
      overflow: "hidden",
      opacity: recalled ? 0.55 : 1,
      animation: isNew ? "flashIn 0.5s ease" : isDelayed ? "none" : "none",
      boxShadow: isDelayed
        ? "0 0 0 1px rgba(239,68,68,0.15), 0 0 24px rgba(239,68,68,0.08)"
        : isReady
        ? "0 0 0 1px rgba(34,197,94,0.15), 0 0 20px rgba(34,197,94,0.06)"
        : "none",
      transition: "opacity 0.2s",
    }}>

      {/* ── Header ── */}
      <div style={{
        padding: "14px 16px 12px",
        background: isDelayed ? "rgba(239,68,68,0.06)" : isReady ? "rgba(34,197,94,0.04)" : CARD_RAISED,
        borderBottom: `1px solid ${BORDER}`,
        display: "flex", flexDirection: "column", gap: 8,
      }}>

        {/* Row 1: order # + status + elapsed */}
        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 8 }}>
          <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
            {/* Status chip */}
            <div style={{
              background: st.bg, border: `1px solid ${st.border}`,
              borderRadius: 5, padding: "2px 8px",
              display: "flex", alignItems: "center", gap: 5,
            }}>
              {isDelayed && (
                <span style={{
                  display: "inline-block", width: 6, height: 6,
                  borderRadius: "50%", background: "#EF4444",
                  animation: "pulse 1s ease-in-out infinite",
                }} />
              )}
              <span style={{
                fontFamily: "'JetBrains Mono', monospace",
                fontSize: 11, fontWeight: 700, letterSpacing: "0.1em",
                color: st.color,
              }}>{st.label}</span>
            </div>

            {/* Priority */}
            {order.priority === "vip" && (
              <span style={{
                fontFamily: "'JetBrains Mono', monospace",
                fontSize: 10, fontWeight: 700, letterSpacing: "0.12em",
                color: "#FACC15", background: "rgba(250,204,21,0.1)",
                border: "1px solid rgba(250,204,21,0.25)",
                padding: "2px 7px", borderRadius: 4,
              }}>VIP</span>
            )}
            {order.priority === "high" && (
              <span style={{
                fontFamily: "'JetBrains Mono', monospace",
                fontSize: 10, fontWeight: 700, letterSpacing: "0.1em",
                color: "#FB923C", background: "rgba(251,146,60,0.1)",
                border: "1px solid rgba(251,146,60,0.2)",
                padding: "2px 7px", borderRadius: 4,
              }}>PRIORITY</span>
            )}

            {/* Order number */}
            <span style={{
              fontFamily: "'JetBrains Mono', monospace",
              fontSize: 13, fontWeight: 600, color: TEXT_MID,
            }}>#{order.shortId}</span>
          </div>

          {/* Elapsed clock */}
          <span style={{
            fontFamily: "'JetBrains Mono', monospace",
            fontSize: 18, fontWeight: 700,
            color: elapsedColor(order.placedSecondsAgo),
            letterSpacing: "-0.5px",
            fontVariantNumeric: "tabular-nums",
          }}>{fmtElapsed(order.placedSecondsAgo)}</span>
        </div>

        {/* Row 2: table + channel + station + ETA */}
        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
          <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
            {/* Table */}
            <div style={{
              fontFamily: "'JetBrains Mono', monospace",
              fontSize: 22, fontWeight: 700, color: TEXT,
              letterSpacing: "-0.5px", lineHeight: 1,
            }}>{order.table}</div>

            {/* Channel badge */}
            {order.channel !== "dine-in" && (
              <span style={{
                fontFamily: "'Outfit', sans-serif",
                fontSize: 10, fontWeight: 700, letterSpacing: "0.08em",
                textTransform: "uppercase",
                color: "#60A5FA", background: "rgba(96,165,250,0.1)",
                border: "1px solid rgba(96,165,250,0.2)",
                padding: "2px 8px", borderRadius: 4,
              }}>{order.channelLabel ?? order.channel}</span>
            )}

            {/* Covers */}
            <span style={{
              fontFamily: "'Outfit', sans-serif",
              fontSize: 13, color: TEXT_DIM,
            }}>{order.covers} cov · {order.waiter}</span>
          </div>

          {/* ETA */}
          {!isReady && (
            <div style={{ textAlign: "right" }}>
              <div style={{
                fontFamily: "'JetBrains Mono', monospace",
                fontSize: 22, fontWeight: 700, lineHeight: 1,
                color: etaColor(order.etaSeconds),
                letterSpacing: "-0.5px",
                fontVariantNumeric: "tabular-nums",
              }}>
                {order.etaSeconds < 0 ? "−" : ""}{fmtClock(order.etaSeconds)}
              </div>
              <div style={{
                fontFamily: "'Outfit', sans-serif",
                fontSize: 10, color: TEXT_DIM,
                textTransform: "uppercase", letterSpacing: "0.1em", marginTop: 2,
              }}>
                {order.etaSeconds < 0 ? "overdue" : "eta"}
              </div>
            </div>
          )}

          {isReady && (
            <div style={{ textAlign: "right" }}>
              <div style={{
                fontFamily: "'JetBrains Mono', monospace",
                fontSize: 18, fontWeight: 700, color: "#22C55E",
              }}>UP</div>
              <div style={{ fontFamily: "'Outfit', sans-serif", fontSize: 10, color: "#22C55E", opacity: 0.6, textTransform: "uppercase", letterSpacing: "0.1em" }}>
                pickup
              </div>
            </div>
          )}
        </div>
      </div>

      {/* ── Items ── */}
      <div style={{ padding: "12px 16px", flex: 1, display: "flex", flexDirection: "column", gap: 10 }}>
        {order.items.map((item) => (
          <div key={item.id}>
            <div style={{ display: "flex", alignItems: "baseline", gap: 10 }}>
              <span style={{
                fontFamily: "'JetBrains Mono', monospace",
                fontSize: 20, fontWeight: 700, color: TEXT,
                minWidth: 28, flexShrink: 0,
                fontVariantNumeric: "tabular-nums",
                lineHeight: 1.15,
              }}>×{item.qty}</span>
              <span style={{
                fontFamily: "'Outfit', sans-serif",
                fontSize: 18, fontWeight: 700, color: TEXT,
                lineHeight: 1.2, letterSpacing: "-0.2px",
              }}>{item.name.toUpperCase()}</span>
            </div>

            {item.modifiers.map((mod, i) => (
              <div key={i} style={{
                display: "flex", alignItems: "center", gap: 8,
                paddingLeft: 38, marginTop: 4,
              }}>
                <div style={{ width: 1, height: 14, background: BORDER_MID, flexShrink: 0 }} />
                <span style={{
                  fontFamily: "'Outfit', sans-serif",
                  fontSize: 13, color: TEXT_MID, lineHeight: 1.3,
                }}>{mod}</span>
              </div>
            ))}

            {item.note && (
              <div style={{
                marginTop: 6, marginLeft: 38,
                padding: "4px 10px",
                background: "rgba(250,204,21,0.08)",
                border: "1px solid rgba(250,204,21,0.2)",
                borderRadius: 5,
              }}>
                <span style={{ fontFamily: "'Outfit', sans-serif", fontSize: 12, color: "#FACC15" }}>
                  ⚑ {item.note}
                </span>
              </div>
            )}
          </div>
        ))}

        {/* Allergy note */}
        {order.allergyNote && (
          <div style={{
            marginTop: 4,
            padding: "8px 12px",
            background: "rgba(239,68,68,0.1)",
            border: "1px solid rgba(239,68,68,0.3)",
            borderRadius: 6,
            display: "flex", alignItems: "flex-start", gap: 8,
          }}>
            <span style={{ fontSize: 14, flexShrink: 0, marginTop: 1 }}>⚠</span>
            <span style={{
              fontFamily: "'Outfit', sans-serif",
              fontSize: 13, fontWeight: 700, color: "#FCA5A5", lineHeight: 1.4,
              textTransform: "uppercase", letterSpacing: "0.03em",
            }}>{order.allergyNote}</span>
          </div>
        )}

        {/* Kitchen note */}
        {order.kitchenNote && (
          <div style={{
            padding: "7px 12px",
            background: "rgba(250,204,21,0.07)",
            border: "1px solid rgba(250,204,21,0.18)",
            borderRadius: 6,
            display: "flex", alignItems: "flex-start", gap: 8,
          }}>
            <span style={{ fontFamily: "'JetBrains Mono', monospace", fontSize: 11, color: "#FDE68A", opacity: 0.6, flexShrink: 0, marginTop: 2 }}>NOTE</span>
            <span style={{
              fontFamily: "'Outfit', sans-serif",
              fontSize: 13, color: "#FDE68A", lineHeight: 1.4,
            }}>{order.kitchenNote}</span>
          </div>
        )}
      </div>

      {/* ── Bump button ── */}
      <button
        onClick={handleBump}
        style={{
          height: 56, width: "100%",
          background: bumping ? "rgba(255,255,255,0.05)" : bumpBg,
          border: "none", borderTop: `1px solid ${BORDER}`,
          color: bumpColor,
          fontFamily: "'JetBrains Mono', monospace",
          fontSize: 14, fontWeight: 700, letterSpacing: "0.12em",
          cursor: "pointer",
          display: "flex", alignItems: "center", justifyContent: "center",
          gap: 10,
          transition: "background 0.1s, transform 0.08s",
          transform: bumping ? "scale(0.98)" : "scale(1)",
          flexShrink: 0,
        }}
      >
        <span>{bumpLabel}</span>
        {!isReady && <span style={{ opacity: 0.5, fontSize: 12 }}>→</span>}
      </button>
    </div>
  );
}

/* ════════════════════════════════════════════════
   Station tab
════════════════════════════════════════════════ */
function StationTab({ station, active, count, hasCritical, onClick }: {
  station: StationId;
  active: boolean;
  count: number;
  hasCritical: boolean;
  onClick: () => void;
}) {
  const meta = STATION_META[station];
  return (
    <button
      onClick={onClick}
      style={{
        display: "flex", alignItems: "center", gap: 8,
        padding: "10px 20px",
        background: active ? meta.colorDim : "transparent",
        border: `1px solid ${active ? meta.color.replace(")", ", 0.4)").replace("rgb", "rgba") : BORDER}`,
        borderRadius: 8,
        cursor: "pointer",
        transition: "all 0.15s",
        flexShrink: 0,
        position: "relative",
      }}
    >
      <span style={{ fontSize: 14 }}>{meta.icon}</span>
      <span style={{
        fontFamily: "'Outfit', sans-serif",
        fontSize: 13, fontWeight: active ? 600 : 400,
        color: active ? meta.color : TEXT_MID,
      }}>{meta.label}</span>
      {count > 0 && (
        <div style={{
          minWidth: 20, height: 20, borderRadius: 99, padding: "0 5px",
          background: hasCritical ? "rgba(239,68,68,0.15)" : active ? meta.colorDim : "rgba(255,255,255,0.05)",
          border: `1px solid ${hasCritical ? "rgba(239,68,68,0.35)" : active ? meta.color.replace(")", ", 0.35)").replace("rgb", "rgba") : BORDER}`,
          display: "flex", alignItems: "center", justifyContent: "center",
        }}>
          <span style={{
            fontFamily: "'JetBrains Mono', monospace",
            fontSize: 11, fontWeight: 700,
            color: hasCritical ? "#EF4444" : active ? meta.color : TEXT_MID,
          }}>{count}</span>
        </div>
      )}
      {hasCritical && (
        <span style={{
          position: "absolute", top: -3, right: -3,
          width: 8, height: 8, borderRadius: "50%",
          background: "#EF4444",
          border: `2px solid ${BG}`,
          animation: "pulse 1s ease-in-out infinite",
        }} />
      )}
    </button>
  );
}

/* ════════════════════════════════════════════════
   Recall rail
════════════════════════════════════════════════ */
function RecallRail({ recalled, onRecall }: {
  recalled: KDSOrder[];
  onRecall: (id: string) => void;
}) {
  if (!recalled.length) return null;
  return (
    <div style={{
      background: SURFACE, borderTop: `1px solid ${BORDER}`,
      padding: "10px 20px",
      display: "flex", alignItems: "center", gap: 12,
    }}>
      <span style={{
        fontFamily: "'JetBrains Mono', monospace",
        fontSize: 10, fontWeight: 700, letterSpacing: "0.15em",
        color: TEXT_DIM, flexShrink: 0,
      }}>RECALL</span>
      <div style={{ display: "flex", gap: 8, overflow: "hidden" }}>
        {recalled.slice(0, 6).map(o => (
          <button
            key={o.id}
            onClick={() => onRecall(o.id)}
            style={{
              display: "flex", alignItems: "center", gap: 8,
              padding: "6px 14px",
              background: CARD, border: `1px solid ${BORDER_MID}`,
              borderRadius: 6, cursor: "pointer",
              fontFamily: "'JetBrains Mono', monospace",
            }}
          >
            <span style={{ fontSize: 12, color: TEXT_DIM }}>#{o.shortId}</span>
            <span style={{ fontSize: 12, fontWeight: 600, color: TEXT_MID }}>{o.table}</span>
            <span style={{ fontSize: 11, color: TEXT_DIM }}>↩ recall</span>
          </button>
        ))}
      </div>
    </div>
  );
}

/* ════════════════════════════════════════════════
   Root KDS
════════════════════════════════════════════════ */
export default function App() {
  const clock = useLiveClock();
  const [orders, setOrders] = useTickedOrders(INITIAL_ORDERS);
  const [station, setStation] = useState<StationId>("all");
  const [recalled, setRecalled] = useState<KDSOrder[]>([]);
  const [compact, setCompact] = useState(false);
  const [soundOn, setSoundOn] = useState(true);

  const handleBump = useCallback((id: string) => {
    setOrders(prev => {
      const order = prev.find(o => o.id === id);
      if (!order) return prev;

      if (order.status === "new") {
        return prev.map(o => o.id === id ? { ...o, status: "preparing" as KDSStatus } : o);
      }
      if (order.status === "preparing" || order.status === "delayed") {
        return prev.map(o => o.id === id ? { ...o, status: "ready" as KDSStatus, etaSeconds: 0 } : o);
      }
      if (order.status === "ready") {
        setRecalled(r => [order, ...r].slice(0, 8));
        return prev.filter(o => o.id !== id);
      }
      return prev;
    });
  }, [setOrders]);

  const handleRecall = useCallback((id: string) => {
    const order = recalled.find(o => o.id === id);
    if (!order) return;
    setRecalled(r => r.filter(o => o.id !== id));
    setOrders(prev => [{ ...order, status: "ready" as KDSStatus }, ...prev]);
  }, [recalled, setOrders]);

  const visible = orders.filter(o =>
    station === "all" || o.station === station
  );
  const sorted = prioritySort(visible);

  const counts = (() => {
    const all = orders;
    const result: Record<StationId, { count: number; hasCritical: boolean }> = {
      all: { count: all.length, hasCritical: all.some(o => o.status === "delayed") },
      grill:   { count: 0, hasCritical: false },
      pasta:   { count: 0, hasCritical: false },
      cold:    { count: 0, hasCritical: false },
      dessert: { count: 0, hasCritical: false },
      drinks:  { count: 0, hasCritical: false },
    };
    for (const o of all) {
      result[o.station].count++;
      if (o.status === "delayed") result[o.station].hasCritical = true;
    }
    return result;
  })();

  const delayedCount = orders.filter(o => o.status === "delayed").length;
  const readyCount = orders.filter(o => o.status === "ready").length;
  const newCount = orders.filter(o => o.status === "new").length;

  const clockStr = `${String(clock.getHours()).padStart(2, "0")}:${String(clock.getMinutes()).padStart(2, "0")}:${String(clock.getSeconds()).padStart(2, "0")}`;

  return (
    <div style={{
      minHeight: "100vh", display: "flex", flexDirection: "column",
      background: BG, color: TEXT,
      fontFamily: "'Outfit', sans-serif",
    }}>

      {/* ── Top bar ── */}
      <header style={{
        height: 56, flexShrink: 0,
        background: SURFACE, borderBottom: `1px solid ${BORDER}`,
        display: "flex", alignItems: "center",
        padding: "0 20px", gap: 0,
      }}>
        {/* Brand + station */}
        <div style={{ display: "flex", alignItems: "center", gap: 14, marginRight: 28 }}>
          <div style={{
            width: 30, height: 30, borderRadius: 8,
            background: "linear-gradient(135deg, #C4622D, #E8915A)",
            display: "flex", alignItems: "center", justifyContent: "center",
          }}>
            <span style={{ fontFamily: "'Fraunces', serif", fontSize: 14, color: "white" }}>M</span>
          </div>
          <div style={{ width: 1, height: 20, background: BORDER }} />
          <span style={{
            fontFamily: "'JetBrains Mono', monospace",
            fontSize: 11, fontWeight: 700, letterSpacing: "0.18em",
            color: TEXT_DIM, textTransform: "uppercase",
          }}>Kitchen Display</span>
        </div>

        {/* Status counters */}
        <div style={{ display: "flex", gap: 6, alignItems: "center" }}>
          {delayedCount > 0 && (
            <div style={{
              display: "flex", alignItems: "center", gap: 6,
              padding: "5px 12px", borderRadius: 6,
              background: "rgba(239,68,68,0.1)", border: "1px solid rgba(239,68,68,0.3)",
              animation: "pulse 2s ease-in-out infinite",
            }}>
              <span style={{ width: 6, height: 6, borderRadius: "50%", background: "#EF4444", display: "inline-block" }} />
              <span style={{ fontFamily: "'JetBrains Mono', monospace", fontSize: 12, fontWeight: 700, color: "#EF4444" }}>
                {delayedCount} LATE
              </span>
            </div>
          )}
          {readyCount > 0 && (
            <div style={{
              display: "flex", alignItems: "center", gap: 6,
              padding: "5px 12px", borderRadius: 6,
              background: "rgba(34,197,94,0.08)", border: "1px solid rgba(34,197,94,0.25)",
            }}>
              <span style={{ width: 6, height: 6, borderRadius: "50%", background: "#22C55E", display: "inline-block" }} />
              <span style={{ fontFamily: "'JetBrains Mono', monospace", fontSize: 12, fontWeight: 700, color: "#22C55E" }}>
                {readyCount} UP
              </span>
            </div>
          )}
          {newCount > 0 && (
            <div style={{
              display: "flex", alignItems: "center", gap: 6,
              padding: "5px 12px", borderRadius: 6,
              background: "rgba(59,130,246,0.08)", border: "1px solid rgba(59,130,246,0.25)",
            }}>
              <span style={{ width: 6, height: 6, borderRadius: "50%", background: "#3B82F6", display: "inline-block" }} />
              <span style={{ fontFamily: "'JetBrains Mono', monospace", fontSize: 12, fontWeight: 700, color: "#3B82F6" }}>
                {newCount} NEW
              </span>
            </div>
          )}
        </div>

        {/* Spacer */}
        <div style={{ flex: 1 }} />

        {/* Right controls */}
        <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
          {/* Avg wait */}
          <div style={{
            padding: "5px 14px", background: CARD, border: `1px solid ${BORDER}`,
            borderRadius: 6, display: "flex", gap: 8,
          }}>
            <span style={{ fontFamily: "'JetBrains Mono', monospace", fontSize: 11, color: TEXT_DIM }}>AVG WAIT</span>
            <span style={{ fontFamily: "'JetBrains Mono', monospace", fontSize: 11, fontWeight: 700, color: TEXT_MID }}>16m 24s</span>
          </div>

          {/* Compact toggle */}
          <button
            onClick={() => setCompact(c => !c)}
            style={{
              height: 34, padding: "0 12px",
              background: compact ? "rgba(129,140,248,0.1)" : CARD,
              border: `1px solid ${compact ? "rgba(129,140,248,0.3)" : BORDER}`,
              borderRadius: 6, color: compact ? "#818CF8" : TEXT_DIM,
              fontFamily: "'JetBrains Mono', monospace", fontSize: 11,
              cursor: "pointer", letterSpacing: "0.08em",
            }}
          >{compact ? "COMPACT ✓" : "COMPACT"}</button>

          {/* Sound */}
          <button
            onClick={() => setSoundOn(s => !s)}
            style={{
              width: 34, height: 34, borderRadius: 6,
              background: CARD, border: `1px solid ${BORDER}`,
              fontSize: 16, cursor: "pointer", color: soundOn ? TEXT_MID : TEXT_DIM,
            }}
          >{soundOn ? "🔊" : "🔇"}</button>

          <div style={{ width: 1, height: 24, background: BORDER }} />

          {/* Clock */}
          <span style={{
            fontFamily: "'JetBrains Mono', monospace",
            fontSize: 18, fontWeight: 700, color: TEXT,
            letterSpacing: "0.04em", fontVariantNumeric: "tabular-nums",
            minWidth: 90,
          }}>{clockStr}</span>
        </div>
      </header>

      {/* ── Station tabs ── */}
      <div style={{
        background: SURFACE, borderBottom: `1px solid ${BORDER}`,
        padding: "10px 20px",
        display: "flex", gap: 8, overflowX: "auto",
        scrollbarWidth: "none",
      }}>
        {(["all", "grill", "pasta", "cold", "dessert", "drinks"] as StationId[]).map(s => (
          <StationTab
            key={s}
            station={s}
            active={station === s}
            count={counts[s].count}
            hasCritical={counts[s].hasCritical}
            onClick={() => setStation(s)}
          />
        ))}

        <div style={{ marginLeft: "auto", display: "flex", gap: 8, alignItems: "center" }}>
          {/* Legend */}
          {[
            { label: "LATE", color: "#EF4444" },
            { label: "NEW", color: "#3B82F6" },
            { label: "UP", color: "#22C55E" },
          ].map(l => (
            <div key={l.label} style={{ display: "flex", alignItems: "center", gap: 5 }}>
              <div style={{ width: 6, height: 6, borderRadius: "50%", background: l.color }} />
              <span style={{ fontFamily: "'JetBrains Mono', monospace", fontSize: 10, color: TEXT_DIM, letterSpacing: "0.1em" }}>{l.label}</span>
            </div>
          ))}
        </div>
      </div>

      {/* ── Order grid ── */}
      <div style={{
        flex: 1, overflowY: "auto",
        padding: "16px 16px 8px",
        display: "grid",
        gridTemplateColumns: compact
          ? "repeat(auto-fill, minmax(240px, 1fr))"
          : "repeat(auto-fill, minmax(300px, 1fr))",
        gridAutoRows: "min-content",
        gap: 12,
        alignContent: "start",
      }}>
        {sorted.map(order => (
          <OrderCard
            key={order.id}
            order={order}
            onBump={handleBump}
          />
        ))}

        {sorted.length === 0 && (
          <div style={{
            gridColumn: "1 / -1",
            display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center",
            padding: "80px 0", gap: 16,
          }}>
            <div style={{
              width: 64, height: 64, borderRadius: 16,
              background: "rgba(34,197,94,0.08)",
              border: "1px solid rgba(34,197,94,0.2)",
              display: "flex", alignItems: "center", justifyContent: "center",
              fontSize: 28,
            }}>✓</div>
            <div style={{ fontFamily: "'JetBrains Mono', monospace", fontSize: 14, color: "#22C55E", letterSpacing: "0.1em" }}>ALL CLEAR</div>
            <div style={{ fontFamily: "'Outfit', sans-serif", fontSize: 13, color: TEXT_DIM }}>No active orders for this station</div>
          </div>
        )}
      </div>

      {/* ── Recall rail ── */}
      <RecallRail recalled={recalled} onRecall={handleRecall} />

      <style>{`
        @keyframes pulse {
          0%, 100% { opacity: 1; }
          50% { opacity: 0.4; }
        }
        @keyframes flashIn {
          0% { background: rgba(59,130,246,0.2); }
          100% { background: ${CARD}; }
        }
        * { box-sizing: border-box; }
        ::-webkit-scrollbar { width: 4px; height: 4px; }
        ::-webkit-scrollbar-track { background: transparent; }
        ::-webkit-scrollbar-thumb { background: ${BORDER_MID}; border-radius: 99px; }
      `}</style>
    </div>
  );
}
