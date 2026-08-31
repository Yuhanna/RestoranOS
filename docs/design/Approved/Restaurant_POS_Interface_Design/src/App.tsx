import { useState, useCallback, useMemo, useRef, useEffect } from "react";
import { CATEGORIES, PRODUCTS, QUICK, DISCOUNT_PRESETS, TABLES, type POSProduct } from "./pos/data";

/* ─── Tokens ─── */
const BG      = "#0D0D0F";
const SURF    = "#131315";
const CARD    = "#1A1A1E";
const RAISED  = "#202025";
const BORDER  = "#26262C";
const BORDMD  = "#2E2E36";
const TEXT    = "#F1F1F3";
const TEXTMD  = "#9CA3AF";
const TEXTDM  = "#4B5563";
const INDIGO  = "#6366F1";
const INDIGODM= "rgba(99,102,241,0.12)";
const INDIGOBR= "rgba(99,102,241,0.3)";
const GREEN   = "#16A34A";
const GREENDM = "rgba(22,163,74,0.12)";
const GREENBR = "rgba(22,163,74,0.3)";
const AMBER   = "#D97706";
const AMBDM   = "rgba(217,119,6,0.12)";
const RED     = "#DC2626";
const REDDM   = "rgba(220,38,38,0.1)";
const REDBR   = "rgba(220,38,38,0.3)";

/* ─── Types ─── */
interface OrderLine { item: POSProduct; qty: number; note?: string; }
interface Discount  { type: "pct" | "fixed"; value: number; label: string; }
interface SplitPart { idx: number; paid: boolean; method?: "cash" | "card"; }

type POSMode   = "pos" | "payment" | "receipt" | "refund";
type PayStep   = "methods" | "cash" | "card" | "split" | "qr";
type Numpad    = "discount-pct" | "discount-fixed" | "cash-received" | "note" | null;

/* ─── Formatting ─── */
const TL = (n: number) => `₺${n.toLocaleString("tr-TR")}`;
const fmt2 = (n: number) => n.toLocaleString("tr-TR", { minimumFractionDigits: 0 });

/* ─── Numpad component ─── */
function NumPad({ value, onChange, onDone, label, prefix = "" }: {
  value: string; onChange: (v: string) => void;
  onDone: () => void; label: string; prefix?: string;
}) {
  const tap = (k: string) => {
    if (k === "⌫") { onChange(value.slice(0, -1)); return; }
    if (k === "." && value.includes(".")) return;
    if (value === "0" && k !== ".") { onChange(k); return; }
    onChange(value + k);
  };
  const keys = ["7","8","9","4","5","6","1","2","3","00","0","."];
  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 0 }}>
      {/* Display */}
      <div style={{
        background: BG, borderRadius: "10px 10px 0 0", padding: "16px 20px",
        borderBottom: `1px solid ${BORDER}`,
      }}>
        <div style={{ fontSize: 11, fontWeight: 600, color: TEXTDM, letterSpacing: "0.1em", textTransform: "uppercase", marginBottom: 6 }}>{label}</div>
        <div style={{
          fontFamily: "'JetBrains Mono', monospace",
          fontSize: 36, fontWeight: 700, color: TEXT, letterSpacing: "-1px",
          minHeight: 44, display: "flex", alignItems: "center",
        }}>
          {prefix}{value || "0"}
        </div>
      </div>
      {/* Keys */}
      <div style={{ display: "grid", gridTemplateColumns: "repeat(3,1fr)", gap: 1, background: BORDER }}>
        {keys.map(k => (
          <button key={k} onClick={() => tap(k)} style={{
            height: 62, background: CARD, border: "none", cursor: "pointer",
            fontFamily: "'JetBrains Mono', monospace",
            fontSize: 20, fontWeight: 600, color: TEXT,
            transition: "background 0.08s",
          }}
            onMouseDown={e => { (e.currentTarget as HTMLElement).style.background = RAISED; }}
            onMouseUp={e => { (e.currentTarget as HTMLElement).style.background = CARD; }}
          >{k}</button>
        ))}
        <button onClick={() => onChange("")} style={{
          height: 62, background: CARD, border: "none", cursor: "pointer",
          fontSize: 20, color: RED, fontWeight: 700,
        }}>C</button>
        <button onClick={() => tap("⌫")} style={{
          height: 62, background: CARD, border: "none", cursor: "pointer",
          fontSize: 20, color: TEXTMD,
        }}>⌫</button>
        <button onClick={onDone} style={{
          height: 62, background: INDIGO, border: "none", cursor: "pointer",
          fontFamily: "'JetBrains Mono', monospace",
          fontSize: 14, fontWeight: 700, color: "white", letterSpacing: "0.06em",
        }}>OK</button>
      </div>
    </div>
  );
}

/* ─── Product button ─── */
function ProductBtn({ product, onAdd, compact = false }: {
  product: POSProduct; onAdd: (p: POSProduct) => void; compact?: boolean;
}) {
  const [pressed, setPressed] = useState(false);
  return (
    <button
      onMouseDown={() => setPressed(true)}
      onMouseUp={() => { setPressed(false); onAdd(product); }}
      onMouseLeave={() => setPressed(false)}
      style={{
        background: pressed ? RAISED : CARD,
        border: `1px solid ${BORDER}`,
        borderRadius: 10, padding: compact ? "10px 12px" : "14px 14px",
        cursor: "pointer", textAlign: "left",
        display: "flex", flexDirection: "column", justifyContent: "space-between",
        gap: 6, minHeight: compact ? 60 : 76,
        transform: pressed ? "scale(0.97)" : "scale(1)",
        transition: "transform 0.08s, background 0.08s",
        position: "relative", overflow: "hidden",
      }}
    >
      {product.popular && (
        <div style={{
          position: "absolute", top: 0, right: 0,
          width: 0, height: 0,
          borderTop: "22px solid rgba(99,102,241,0.45)",
          borderLeft: "22px solid transparent",
        }} />
      )}
      <span style={{
        fontFamily: "'DM Sans', sans-serif",
        fontSize: compact ? 12 : 13, fontWeight: 600,
        color: TEXT, lineHeight: 1.3,
      }}>{product.name}</span>
      <span style={{
        fontFamily: "'JetBrains Mono', monospace",
        fontSize: compact ? 13 : 14, fontWeight: 700,
        color: INDIGO,
      }}>{TL(product.price)}</span>
    </button>
  );
}

/* ─── Order line row ─── */
function OrderLineRow({ line, onChange, onRemove }: {
  line: OrderLine;
  onChange: (qty: number) => void;
  onRemove: () => void;
}) {
  const lineTotal = line.item.price * line.qty;
  return (
    <div style={{
      display: "flex", alignItems: "center", gap: 8,
      padding: "10px 14px",
      borderBottom: `1px solid ${BORDER}`,
    }}>
      {/* Qty stepper */}
      <div style={{
        display: "flex", alignItems: "center",
        border: `1px solid ${BORDER}`, borderRadius: 7, overflow: "hidden",
        flexShrink: 0,
      }}>
        <button onClick={() => line.qty > 1 ? onChange(line.qty - 1) : onRemove()} style={{
          width: 32, height: 32, background: "none", border: "none",
          color: line.qty === 1 ? RED : TEXTMD,
          fontSize: 18, cursor: "pointer", display: "flex", alignItems: "center", justifyContent: "center",
        }}>{line.qty === 1 ? "×" : "−"}</button>
        <span style={{
          width: 26, textAlign: "center",
          fontFamily: "'JetBrains Mono', monospace",
          fontSize: 14, fontWeight: 700, color: TEXT,
        }}>{line.qty}</span>
        <button onClick={() => onChange(line.qty + 1)} style={{
          width: 32, height: 32, background: "none", border: "none",
          color: TEXT, fontSize: 18, cursor: "pointer", display: "flex", alignItems: "center", justifyContent: "center",
        }}>+</button>
      </div>

      {/* Name */}
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{
          fontFamily: "'DM Sans', sans-serif",
          fontSize: 13, fontWeight: 500, color: TEXT,
          overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap",
        }}>{line.item.name}</div>
        {line.note && (
          <div style={{ fontSize: 11, color: AMBER, marginTop: 2 }}>⚑ {line.note}</div>
        )}
      </div>

      {/* Unit price */}
      <div style={{
        fontFamily: "'JetBrains Mono', monospace",
        fontSize: 12, color: TEXTDM, flexShrink: 0, textAlign: "right",
      }}>{TL(line.item.price)}<br />ea</div>

      {/* Line total */}
      <div style={{
        fontFamily: "'JetBrains Mono', monospace",
        fontSize: 14, fontWeight: 700, color: TEXT,
        minWidth: 72, textAlign: "right", flexShrink: 0,
      }}>{TL(lineTotal)}</div>
    </div>
  );
}

/* ─── Table picker ─── */
function TablePicker({ selected, onSelect, onWalkIn, onClose }: {
  selected: number | null; onSelect: (n: number) => void;
  onWalkIn: () => void; onClose: () => void;
}) {
  return (
    <div style={{
      position: "fixed", inset: 0, zIndex: 100,
      background: "rgba(0,0,0,0.7)", backdropFilter: "blur(4px)",
      display: "flex", alignItems: "center", justifyContent: "center",
    }} onClick={onClose}>
      <div onClick={e => e.stopPropagation()} style={{
        background: SURF, border: `1px solid ${BORDER}`,
        borderRadius: 16, padding: "24px 28px", width: 560,
        boxShadow: "0 24px 80px rgba(0,0,0,0.6)",
      }}>
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 20 }}>
          <h2 style={{ fontFamily: "'DM Sans', sans-serif", fontSize: 17, fontWeight: 700, color: TEXT, margin: 0 }}>Select Table</h2>
          <button onClick={onClose} style={{ background: "none", border: "none", color: TEXTMD, fontSize: 22, cursor: "pointer" }}>×</button>
        </div>

        <button onClick={onWalkIn} style={{
          width: "100%", padding: "12px", marginBottom: 16,
          background: INDIGODM, border: `1px solid ${INDIGOBR}`,
          borderRadius: 10, color: INDIGO, fontFamily: "'DM Sans', sans-serif",
          fontSize: 14, fontWeight: 600, cursor: "pointer",
        }}>
          Walk-in · No table / Takeaway
        </button>

        <div style={{ display: "grid", gridTemplateColumns: "repeat(6,1fr)", gap: 8 }}>
          {TABLES.map(t => (
            <button key={t.n} onClick={() => onSelect(t.n)} style={{
              padding: "10px 0", borderRadius: 8, border: `1.5px solid`,
              borderColor: selected === t.n ? INDIGO : t.occupied ? BORDMD : BORDER,
              background: selected === t.n ? INDIGODM : t.occupied ? RAISED : CARD,
              cursor: "pointer", display: "flex", flexDirection: "column",
              alignItems: "center", gap: 4,
            }}>
              <span style={{
                fontFamily: "'JetBrains Mono', monospace",
                fontSize: 15, fontWeight: 700,
                color: selected === t.n ? INDIGO : t.occupied ? TEXT : TEXTMD,
              }}>T{t.n}</span>
              {t.occupied && (
                <span style={{ fontSize: 10, color: TEXTDM }}>{t.covers}×</span>
              )}
            </button>
          ))}
        </div>
      </div>
    </div>
  );
}

/* ─── Discount modal ─── */
function DiscountModal({ onApply, onClose }: {
  onApply: (d: Discount) => void; onClose: () => void;
}) {
  const [mode, setMode] = useState<"pct" | "fixed">("pct");
  const [val, setVal] = useState("");

  return (
    <div style={{
      position: "fixed", inset: 0, zIndex: 100,
      background: "rgba(0,0,0,0.7)", backdropFilter: "blur(4px)",
      display: "flex", alignItems: "center", justifyContent: "center",
    }} onClick={onClose}>
      <div onClick={e => e.stopPropagation()} style={{
        background: SURF, border: `1px solid ${BORDER}`,
        borderRadius: 16, overflow: "hidden", width: 340,
        boxShadow: "0 24px 80px rgba(0,0,0,0.6)",
      }}>
        {/* Header */}
        <div style={{ padding: "18px 20px 14px", borderBottom: `1px solid ${BORDER}`, display: "flex", justifyContent: "space-between" }}>
          <div style={{ fontSize: 15, fontWeight: 700, color: TEXT }}>Apply Discount</div>
          <button onClick={onClose} style={{ background: "none", border: "none", color: TEXTMD, fontSize: 22, cursor: "pointer", lineHeight: 1 }}>×</button>
        </div>

        {/* Presets */}
        <div style={{ padding: "14px 16px", borderBottom: `1px solid ${BORDER}` }}>
          <div style={{ fontSize: 11, color: TEXTDM, letterSpacing: "0.1em", textTransform: "uppercase", marginBottom: 10 }}>Quick Select</div>
          <div style={{ display: "flex", flexWrap: "wrap", gap: 8 }}>
            {DISCOUNT_PRESETS.map(p => (
              <button key={p.label} onClick={() => onApply({ type: p.type, value: p.value, label: p.label })} style={{
                padding: "8px 14px", borderRadius: 8,
                background: CARD, border: `1px solid ${BORDER}`,
                color: TEXT, fontFamily: "'DM Sans', sans-serif",
                fontSize: 13, fontWeight: 500, cursor: "pointer",
              }}>{p.label}</button>
            ))}
          </div>
        </div>

        {/* Custom */}
        <div style={{ padding: "0" }}>
          <div style={{ display: "flex", borderBottom: `1px solid ${BORDER}` }}>
            {(["pct", "fixed"] as const).map(m => (
              <button key={m} onClick={() => setMode(m)} style={{
                flex: 1, padding: "12px", background: mode === m ? INDIGODM : "none",
                border: "none", borderBottom: `2px solid ${mode === m ? INDIGO : "transparent"}`,
                color: mode === m ? INDIGO : TEXTMD,
                fontFamily: "'DM Sans', sans-serif", fontSize: 13, fontWeight: 600,
                cursor: "pointer",
              }}>{m === "pct" ? "Percentage %" : "Fixed ₺"}</button>
            ))}
          </div>
          <NumPad
            value={val} onChange={setVal} label={mode === "pct" ? "Discount %" : "Fixed Amount ₺"}
            prefix={mode === "pct" ? "" : "₺"}
            onDone={() => {
              const n = parseFloat(val);
              if (!n) return;
              onApply({ type: mode, value: n, label: mode === "pct" ? `${n}% off` : `₺${n} off` });
            }}
          />
        </div>
      </div>
    </div>
  );
}

/* ─── Refund modal ─── */
function RefundModal({ onClose }: { onClose: () => void }) {
  const [step, setStep] = useState<"search" | "items">("search");
  const [orderId, setOrderId] = useState("");

  return (
    <div style={{
      position: "fixed", inset: 0, zIndex: 100,
      background: "rgba(0,0,0,0.7)", backdropFilter: "blur(4px)",
      display: "flex", alignItems: "center", justifyContent: "center",
    }} onClick={onClose}>
      <div onClick={e => e.stopPropagation()} style={{
        background: SURF, border: `1px solid ${REDBR}`,
        borderRadius: 16, overflow: "hidden", width: 440,
        boxShadow: "0 24px 80px rgba(0,0,0,0.6)",
      }}>
        <div style={{ padding: "18px 20px 14px", borderBottom: `1px solid ${BORDER}`, display: "flex", justifyContent: "space-between", alignItems: "center" }}>
          <div style={{ display: "flex", gap: 10, alignItems: "center" }}>
            <div style={{ width: 8, height: 8, borderRadius: "50%", background: RED }} />
            <span style={{ fontSize: 15, fontWeight: 700, color: TEXT }}>Process Refund</span>
          </div>
          <button onClick={onClose} style={{ background: "none", border: "none", color: TEXTMD, fontSize: 22, cursor: "pointer" }}>×</button>
        </div>

        {step === "search" ? (
          <div style={{ padding: "20px" }}>
            <div style={{ fontSize: 12, color: TEXTMD, marginBottom: 10 }}>Enter order number or scan receipt barcode</div>
            <div style={{ display: "flex", gap: 10 }}>
              <input
                value={orderId} onChange={e => setOrderId(e.target.value)}
                placeholder="ORD-2838"
                style={{
                  flex: 1, padding: "12px 14px",
                  background: CARD, border: `1px solid ${BORDER}`,
                  borderRadius: 8, color: TEXT, fontFamily: "'JetBrains Mono', monospace",
                  fontSize: 15, outline: "none",
                }}
              />
              <button onClick={() => orderId && setStep("items")} style={{
                padding: "12px 20px", background: RED, border: "none",
                borderRadius: 8, color: "white", fontFamily: "'DM Sans', sans-serif",
                fontSize: 14, fontWeight: 600, cursor: "pointer",
              }}>Find</button>
            </div>

            <div style={{ marginTop: 20, padding: "14px", background: CARD, borderRadius: 10, border: `1px solid ${BORDER}` }}>
              <div style={{ fontSize: 11, color: TEXTDM, marginBottom: 10, textTransform: "uppercase", letterSpacing: "0.1em" }}>Recent Completed Orders</div>
              {[
                { id: "ORD-2838", table: "T18", total: 890, time: "21:56" },
                { id: "ORD-2837", table: "T9",  total: 820, time: "21:52" },
                { id: "ORD-2830", table: "T4",  total: 640, time: "21:40" },
              ].map(o => (
                <button key={o.id} onClick={() => { setOrderId(o.id); setStep("items"); }} style={{
                  display: "flex", justifyContent: "space-between", width: "100%",
                  padding: "10px 0", background: "none", border: "none", borderBottom: `1px solid ${BORDER}`,
                  cursor: "pointer", color: TEXT,
                }}>
                  <span style={{ fontFamily: "'JetBrains Mono', monospace", fontSize: 13 }}>{o.id}</span>
                  <span style={{ fontSize: 13, color: TEXTMD }}>{o.table}</span>
                  <span style={{ fontFamily: "'JetBrains Mono', monospace", fontSize: 13 }}>{TL(o.total)}</span>
                  <span style={{ fontSize: 12, color: TEXTDM }}>{o.time}</span>
                </button>
              ))}
            </div>
          </div>
        ) : (
          <div style={{ padding: "20px" }}>
            <div style={{ display: "flex", gap: 8, alignItems: "center", marginBottom: 16 }}>
              <button onClick={() => setStep("search")} style={{ background: "none", border: "none", color: TEXTMD, cursor: "pointer", fontSize: 14 }}>← Back</button>
              <span style={{ fontFamily: "'JetBrains Mono', monospace", fontSize: 13, color: TEXTMD }}>{orderId}</span>
            </div>
            {[
              { name: "Salmon Tartare", qty: 1, price: 165 },
              { name: "Tasting Amuse-Bouche", qty: 2, price: 220 },
            ].map(item => (
              <div key={item.name} style={{
                display: "flex", gap: 12, padding: "12px 0", borderBottom: `1px solid ${BORDER}`,
                alignItems: "center",
              }}>
                <input type="checkbox" defaultChecked style={{ width: 18, height: 18, cursor: "pointer", accentColor: RED }} />
                <div style={{ flex: 1 }}>
                  <div style={{ fontSize: 14, color: TEXT }}>{item.name}</div>
                  <div style={{ fontSize: 12, color: TEXTMD }}>×{item.qty} · {TL(item.price)} ea</div>
                </div>
                <div style={{ fontFamily: "'JetBrains Mono', monospace", fontSize: 14, color: TEXT }}>{TL(item.price * item.qty)}</div>
              </div>
            ))}
            <div style={{ display: "flex", gap: 10, marginTop: 16 }}>
              <button onClick={onClose} style={{
                flex: 1, padding: "14px", background: REDDM, border: `1px solid ${REDBR}`,
                borderRadius: 8, color: RED, fontFamily: "'DM Sans', sans-serif",
                fontSize: 14, fontWeight: 700, cursor: "pointer",
              }}>Refund ₺385 → Cash</button>
              <button onClick={onClose} style={{
                flex: 1, padding: "14px", background: REDDM, border: `1px solid ${REDBR}`,
                borderRadius: 8, color: RED, fontFamily: "'DM Sans', sans-serif",
                fontSize: 14, fontWeight: 700, cursor: "pointer",
              }}>Refund ₺385 → Card</button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

/* ─── Cash session modal ─── */
function CashSessionModal({ isOpen, onClose }: { isOpen: boolean; onClose: () => void }) {
  const [float, setFloat] = useState("2000");
  return (
    <div style={{
      position: "fixed", inset: 0, zIndex: 100,
      background: "rgba(0,0,0,0.7)", backdropFilter: "blur(4px)",
      display: "flex", alignItems: "center", justifyContent: "center",
    }} onClick={onClose}>
      <div onClick={e => e.stopPropagation()} style={{
        background: SURF, border: `1px solid ${BORDER}`,
        borderRadius: 16, overflow: "hidden", width: 400,
        boxShadow: "0 24px 80px rgba(0,0,0,0.6)",
      }}>
        <div style={{ padding: "18px 20px 14px", borderBottom: `1px solid ${BORDER}`, display: "flex", justifyContent: "space-between" }}>
          <span style={{ fontSize: 15, fontWeight: 700, color: TEXT }}>
            {isOpen ? "Close Cash Session" : "Open Cash Session"}
          </span>
          <button onClick={onClose} style={{ background: "none", border: "none", color: TEXTMD, fontSize: 22, cursor: "pointer" }}>×</button>
        </div>

        {isOpen ? (
          <div style={{ padding: "20px" }}>
            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12, marginBottom: 20 }}>
              {[
                { label: "Opening Float", val: "₺2,000" },
                { label: "Cash Sales", val: "₺14,200" },
                { label: "Cash Refunds", val: "−₺385" },
                { label: "Expected in Drawer", val: "₺15,815" },
              ].map(r => (
                <div key={r.label} style={{ background: CARD, borderRadius: 9, padding: "12px 14px", border: `1px solid ${BORDER}` }}>
                  <div style={{ fontSize: 11, color: TEXTDM, marginBottom: 4 }}>{r.label}</div>
                  <div style={{ fontFamily: "'JetBrains Mono', monospace", fontSize: 18, fontWeight: 700, color: TEXT }}>{r.val}</div>
                </div>
              ))}
            </div>
            <div style={{ marginBottom: 16 }}>
              <div style={{ fontSize: 12, color: TEXTMD, marginBottom: 8 }}>Actual cash counted</div>
              <div style={{ display: "flex", gap: 10 }}>
                <input defaultValue="15800" style={{
                  flex: 1, padding: "12px 14px", background: CARD, border: `1px solid ${BORDER}`,
                  borderRadius: 8, color: TEXT, fontFamily: "'JetBrains Mono', monospace", fontSize: 16, outline: "none",
                }} />
              </div>
              <div style={{ fontSize: 12, color: RED, marginTop: 6 }}>Variance: −₺15 (short)</div>
            </div>
            <button onClick={onClose} style={{
              width: "100%", padding: "14px", background: RED, border: "none",
              borderRadius: 8, color: "white", fontFamily: "'DM Sans', sans-serif",
              fontSize: 15, fontWeight: 700, cursor: "pointer",
            }}>Close Session & Print Z Report</button>
          </div>
        ) : (
          <div style={{ padding: "20px" }}>
            <NumPad value={float} onChange={setFloat} label="Opening Float (₺)" prefix="₺" onDone={onClose} />
            <button onClick={onClose} style={{
              width: "100%", padding: "14px", background: GREEN, border: "none",
              borderRadius: "0 0 8px 8px", color: "white", fontFamily: "'DM Sans', sans-serif",
              fontSize: 15, fontWeight: 700, cursor: "pointer", marginTop: 1,
            }}>Open Session with {TL(parseFloat(float) || 0)}</button>
          </div>
        )}
      </div>
    </div>
  );
}

/* ─── Payment modal ─── */
function PaymentModal({ total, subtotal, discount, onClose, onComplete }: {
  total: number; subtotal: number; discount: Discount | null;
  onClose: () => void; onComplete: () => void;
}) {
  const [step, setStep] = useState<PayStep>("methods");
  const [cashVal, setCashVal] = useState("");
  const [splitCount, setSplitCount] = useState(2);
  const [splitPaid, setSplitPaid] = useState<boolean[]>([false, false]);

  const cashNum = parseFloat(cashVal) || 0;
  const change = Math.max(0, cashNum - total);
  const perPerson = Math.ceil(total / splitCount);

  const toggleSplit = (i: number) => setSplitPaid(p => { const n = [...p]; n[i] = !n[i]; return n; });
  const allSplitPaid = splitPaid.every(Boolean);

  return (
    <div style={{
      position: "fixed", inset: 0, zIndex: 100,
      background: "rgba(0,0,0,0.75)", backdropFilter: "blur(6px)",
      display: "flex", alignItems: "center", justifyContent: "center",
    }}>
      <div style={{
        background: SURF, border: `1px solid ${BORDER}`,
        borderRadius: 20, overflow: "hidden", width: 480,
        boxShadow: "0 32px 100px rgba(0,0,0,0.7)",
        display: "flex", flexDirection: "column",
        maxHeight: "90vh",
      }}>
        {/* Header */}
        <div style={{
          padding: "20px 24px", borderBottom: `1px solid ${BORDER}`,
          display: "flex", justifyContent: "space-between", alignItems: "center",
          flexShrink: 0,
        }}>
          <div>
            <div style={{ fontSize: 11, color: TEXTDM, letterSpacing: "0.1em", textTransform: "uppercase", marginBottom: 4 }}>Charge Total</div>
            <div style={{
              fontFamily: "'JetBrains Mono', monospace",
              fontSize: 36, fontWeight: 700, color: TEXT, letterSpacing: "-1px",
            }}>{TL(total)}</div>
          </div>
          {step !== "methods" && (
            <button onClick={() => setStep("methods")} style={{
              padding: "8px 14px", background: CARD, border: `1px solid ${BORDER}`,
              borderRadius: 8, color: TEXTMD, fontFamily: "'DM Sans', sans-serif",
              fontSize: 13, cursor: "pointer",
            }}>← Back</button>
          )}
          {step === "methods" && (
            <button onClick={onClose} style={{
              width: 34, height: 34, borderRadius: "50%", background: RAISED,
              border: `1px solid ${BORDER}`, color: TEXTMD, fontSize: 20, cursor: "pointer",
            }}>×</button>
          )}
        </div>

        {/* Order summary strip */}
        {step === "methods" && (
          <div style={{
            padding: "12px 24px", background: BG, borderBottom: `1px solid ${BORDER}`,
            display: "flex", gap: 24, flexShrink: 0,
          }}>
            <div>
              <div style={{ fontSize: 11, color: TEXTDM }}>Subtotal</div>
              <div style={{ fontFamily: "'JetBrains Mono', monospace", fontSize: 14, color: TEXTMD }}>{TL(subtotal)}</div>
            </div>
            {discount && (
              <div>
                <div style={{ fontSize: 11, color: TEXTDM }}>Discount</div>
                <div style={{ fontFamily: "'JetBrains Mono', monospace", fontSize: 14, color: AMBER }}>−{discount.label}</div>
              </div>
            )}
            <div>
              <div style={{ fontSize: 11, color: TEXTDM }}>Service (12%)</div>
              <div style={{ fontFamily: "'JetBrains Mono', monospace", fontSize: 14, color: TEXTMD }}>{TL(Math.round(total * 0.12 / 1.12))}</div>
            </div>
          </div>
        )}

        {/* Content */}
        <div style={{ overflowY: "auto", flex: 1 }}>

          {/* Method selection */}
          {step === "methods" && (
            <div style={{ padding: "20px 24px", display: "flex", flexDirection: "column", gap: 10 }}>
              {[
                { id: "cash",  icon: "💵", label: "Cash",          sub: "Enter received amount" },
                { id: "card",  icon: "💳", label: "Card",          sub: "Tap / insert / swipe" },
                { id: "split", icon: "⊘",  label: "Split Payment", sub: "Divide by guests or items" },
                { id: "qr",    icon: "▦",  label: "QR Pay",        sub: "Customer scans to pay" },
              ].map(m => (
                <button key={m.id} onClick={() => setStep(m.id as PayStep)} style={{
                  display: "flex", alignItems: "center", gap: 16,
                  padding: "16px 20px", borderRadius: 12,
                  background: CARD, border: `1px solid ${BORDER}`,
                  cursor: "pointer", textAlign: "left",
                  transition: "border-color 0.15s, background 0.15s",
                }}
                  onMouseEnter={e => {
                    (e.currentTarget as HTMLElement).style.borderColor = INDIGOBR;
                    (e.currentTarget as HTMLElement).style.background = INDIGODM;
                  }}
                  onMouseLeave={e => {
                    (e.currentTarget as HTMLElement).style.borderColor = BORDER;
                    (e.currentTarget as HTMLElement).style.background = CARD;
                  }}
                >
                  <span style={{ fontSize: 28, width: 40, textAlign: "center" }}>{m.icon}</span>
                  <div>
                    <div style={{ fontSize: 16, fontWeight: 700, color: TEXT, marginBottom: 2 }}>{m.label}</div>
                    <div style={{ fontSize: 13, color: TEXTMD }}>{m.sub}</div>
                  </div>
                  <span style={{ marginLeft: "auto", color: TEXTDM, fontSize: 18 }}>›</span>
                </button>
              ))}
            </div>
          )}

          {/* Cash payment */}
          {step === "cash" && (
            <div>
              <NumPad value={cashVal} onChange={setCashVal} label="Cash Received (₺)" prefix="₺" onDone={() => {}} />
              {cashNum > 0 && (
                <div style={{
                  margin: "0 0", padding: "16px 20px",
                  background: change >= 0 ? GREENDM : REDDM,
                  border: `1px solid ${change >= 0 ? GREENBR : REDBR}`,
                  display: "flex", justifyContent: "space-between", alignItems: "center",
                }}>
                  <span style={{ fontSize: 14, fontWeight: 600, color: change >= 0 ? GREEN : RED }}>
                    {change >= 0 ? "Change Due" : "Insufficient"}
                  </span>
                  <span style={{
                    fontFamily: "'JetBrains Mono', monospace",
                    fontSize: 28, fontWeight: 700, color: change >= 0 ? GREEN : RED,
                    letterSpacing: "-0.5px",
                  }}>{TL(change)}</span>
                </div>
              )}
              <button
                disabled={cashNum < total}
                onClick={onComplete}
                style={{
                  width: "100%", padding: "18px",
                  background: cashNum >= total ? GREEN : TEXTDM,
                  border: "none", color: "white",
                  fontFamily: "'DM Sans', sans-serif", fontSize: 16, fontWeight: 700,
                  cursor: cashNum >= total ? "pointer" : "not-allowed",
                }}
              >Confirm Cash Payment · {TL(total)}</button>
            </div>
          )}

          {/* Card */}
          {step === "card" && (
            <div style={{ padding: "32px 24px", textAlign: "center" }}>
              <div style={{
                width: 80, height: 80, borderRadius: "50%",
                background: INDIGODM, border: `2px solid ${INDIGOBR}`,
                display: "flex", alignItems: "center", justifyContent: "center",
                fontSize: 36, margin: "0 auto 20px",
              }}>💳</div>
              <div style={{ fontSize: 16, fontWeight: 600, color: TEXT, marginBottom: 8 }}>
                Awaiting Terminal
              </div>
              <div style={{ fontSize: 14, color: TEXTMD, marginBottom: 28, lineHeight: 1.6 }}>
                Please ask the guest to tap, insert, or swipe their card on the Stripe terminal.
              </div>
              <div style={{
                padding: "12px 20px", background: INDIGODM, borderRadius: 10,
                border: `1px solid ${INDIGOBR}`, marginBottom: 20,
                fontFamily: "'JetBrains Mono', monospace", fontSize: 28, fontWeight: 700, color: INDIGO,
              }}>{TL(total)}</div>
              <div style={{ display: "flex", flexDirection: "column", gap: 8, margin: "0 0 8px" }}>
                <button onClick={onComplete} style={{
                  padding: "14px", background: GREEN, border: "none", borderRadius: 8,
                  color: "white", fontFamily: "'DM Sans', sans-serif",
                  fontSize: 15, fontWeight: 700, cursor: "pointer",
                }}>✓ Payment Approved</button>
                <button onClick={() => setStep("methods")} style={{
                  padding: "14px", background: REDDM, border: `1px solid ${REDBR}`,
                  borderRadius: 8, color: RED, fontFamily: "'DM Sans', sans-serif",
                  fontSize: 14, fontWeight: 600, cursor: "pointer",
                }}>✗ Declined — Try Again</button>
              </div>
            </div>
          )}

          {/* Split */}
          {step === "split" && (
            <div style={{ padding: "20px 24px" }}>
              <div style={{ marginBottom: 20 }}>
                <div style={{ fontSize: 12, color: TEXTMD, marginBottom: 12 }}>Number of guests</div>
                <div style={{ display: "flex", alignItems: "center", gap: 16 }}>
                  <button onClick={() => { const n = Math.max(2, splitCount-1); setSplitCount(n); setSplitPaid(Array(n).fill(false)); }}
                    style={{ width: 44, height: 44, borderRadius: "50%", background: CARD, border: `1px solid ${BORDER}`, color: TEXT, fontSize: 22, cursor: "pointer" }}>−</button>
                  <div style={{ textAlign: "center" }}>
                    <div style={{ fontFamily: "'JetBrains Mono', monospace", fontSize: 32, fontWeight: 700, color: TEXT }}>{splitCount}</div>
                    <div style={{ fontSize: 12, color: TEXTDM }}>guests</div>
                  </div>
                  <button onClick={() => { const n = splitCount+1; setSplitCount(n); setSplitPaid(Array(n).fill(false)); }}
                    style={{ width: 44, height: 44, borderRadius: "50%", background: CARD, border: `1px solid ${BORDER}`, color: TEXT, fontSize: 22, cursor: "pointer" }}>+</button>
                  <div style={{ marginLeft: "auto", textAlign: "right" }}>
                    <div style={{ fontFamily: "'JetBrains Mono', monospace", fontSize: 22, fontWeight: 700, color: INDIGO }}>{TL(perPerson)}</div>
                    <div style={{ fontSize: 12, color: TEXTDM }}>per person</div>
                  </div>
                </div>
              </div>

              <div style={{ display: "flex", flexDirection: "column", gap: 8, marginBottom: 20 }}>
                {Array.from({ length: splitCount }, (_, i) => (
                  <div key={i} style={{
                    display: "flex", alignItems: "center", gap: 14, padding: "12px 16px",
                    background: splitPaid[i] ? GREENDM : CARD,
                    border: `1px solid ${splitPaid[i] ? GREENBR : BORDER}`,
                    borderRadius: 10,
                  }}>
                    <div style={{
                      width: 32, height: 32, borderRadius: "50%",
                      background: splitPaid[i] ? GREEN : RAISED,
                      display: "flex", alignItems: "center", justifyContent: "center",
                      fontFamily: "'JetBrains Mono', monospace", fontSize: 14, fontWeight: 700,
                      color: splitPaid[i] ? "white" : TEXTMD,
                    }}>{i + 1}</div>
                    <div>
                      <div style={{ fontSize: 13, fontWeight: 500, color: TEXT }}>Guest {i + 1}</div>
                      <div style={{ fontFamily: "'JetBrains Mono', monospace", fontSize: 13, color: splitPaid[i] ? GREEN : TEXTMD }}>
                        {TL(perPerson)} {splitPaid[i] ? "· Paid ✓" : "· Pending"}
                      </div>
                    </div>
                    {!splitPaid[i] && (
                      <div style={{ marginLeft: "auto", display: "flex", gap: 8 }}>
                        <button onClick={() => toggleSplit(i)} style={{
                          padding: "7px 14px", background: INDIGODM, border: `1px solid ${INDIGOBR}`,
                          borderRadius: 6, color: INDIGO, fontFamily: "'DM Sans', sans-serif",
                          fontSize: 12, fontWeight: 600, cursor: "pointer",
                        }}>Card</button>
                        <button onClick={() => toggleSplit(i)} style={{
                          padding: "7px 14px", background: GREENDM, border: `1px solid ${GREENBR}`,
                          borderRadius: 6, color: GREEN, fontFamily: "'DM Sans', sans-serif",
                          fontSize: 12, fontWeight: 600, cursor: "pointer",
                        }}>Cash</button>
                      </div>
                    )}
                    {splitPaid[i] && (
                      <button onClick={() => toggleSplit(i)} style={{
                        marginLeft: "auto", padding: "7px 14px",
                        background: "none", border: "none",
                        color: TEXTDM, fontSize: 12, cursor: "pointer",
                      }}>Undo</button>
                    )}
                  </div>
                ))}
              </div>

              <button
                disabled={!allSplitPaid}
                onClick={onComplete}
                style={{
                  width: "100%", padding: "16px",
                  background: allSplitPaid ? GREEN : TEXTDM,
                  border: "none", borderRadius: 10, color: "white",
                  fontFamily: "'DM Sans', sans-serif", fontSize: 15, fontWeight: 700,
                  cursor: allSplitPaid ? "pointer" : "not-allowed",
                }}
              >{allSplitPaid ? "Complete Split Payment ✓" : `${splitPaid.filter(Boolean).length}/${splitCount} Paid`}</button>
            </div>
          )}

          {/* QR */}
          {step === "qr" && (
            <div style={{ padding: "32px 24px", textAlign: "center" }}>
              <div style={{
                width: 160, height: 160, margin: "0 auto 20px",
                background: "white", borderRadius: 12, padding: 12,
                display: "flex", alignItems: "center", justifyContent: "center",
              }}>
                <div style={{ fontSize: 11, color: "#666", textAlign: "center" }}>
                  [QR Code]<br />
                  <span style={{ fontFamily: "'JetBrains Mono', monospace", fontSize: 10 }}>{TL(total)}</span>
                </div>
              </div>
              <div style={{ fontSize: 14, color: TEXTMD, marginBottom: 20 }}>
                Guest scans with their camera or banking app
              </div>
              <button onClick={onComplete} style={{
                width: "100%", padding: "16px", background: GREEN, border: "none", borderRadius: 10,
                color: "white", fontFamily: "'DM Sans', sans-serif", fontSize: 15, fontWeight: 700, cursor: "pointer",
              }}>Payment Confirmed ✓</button>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

/* ─── Receipt ─── */
function ReceiptView({ lines, total, discount, table, onNewSale }: {
  lines: OrderLine[]; total: number; discount: Discount | null;
  table: number | null; onNewSale: () => void;
}) {
  const subtotal = lines.reduce((s, l) => s + l.item.price * l.qty, 0);
  const discountAmt = discount
    ? discount.type === "pct" ? Math.round(subtotal * discount.value / 100) : discount.value
    : 0;
  const service = Math.round((subtotal - discountAmt) * 0.12);

  return (
    <div style={{
      flex: 1, display: "flex", flexDirection: "column",
      alignItems: "center", justifyContent: "center",
      background: BG, padding: 32,
    }}>
      <div style={{
        background: SURF, border: `1px solid ${BORDER}`,
        borderRadius: 16, width: 380, overflow: "hidden",
        boxShadow: "0 24px 60px rgba(0,0,0,0.5)",
      }}>
        {/* Header */}
        <div style={{ background: CARD, padding: "24px 28px", textAlign: "center", borderBottom: `1px solid ${BORDER}` }}>
          <div style={{
            width: 48, height: 48, borderRadius: 14, margin: "0 auto 12px",
            background: "linear-gradient(135deg,#C4622D,#E8915A)",
            display: "flex", alignItems: "center", justifyContent: "center",
          }}>
            <span style={{ fontFamily: "'Fraunces', serif", fontSize: 22, color: "white" }}>M</span>
          </div>
          <div style={{ fontFamily: "'Fraunces', serif", fontSize: 20, color: TEXT, marginBottom: 2 }}>Marea · Nişantaşı</div>
          <div style={{ fontSize: 12, color: TEXTDM }}>
            {table ? `Table ${table}` : "Walk-in"} · {new Date().toLocaleTimeString("tr-TR", { hour: "2-digit", minute: "2-digit" })}
          </div>
        </div>

        {/* Items */}
        <div style={{ padding: "16px 20px" }}>
          {lines.map(l => (
            <div key={l.item.id} style={{ display: "flex", justifyContent: "space-between", padding: "5px 0" }}>
              <span style={{ fontSize: 13, color: TEXT }}>×{l.qty} {l.item.name}</span>
              <span style={{ fontFamily: "'JetBrains Mono', monospace", fontSize: 13, color: TEXT }}>{TL(l.item.price * l.qty)}</span>
            </div>
          ))}
          <div style={{ height: 1, background: BORDER, margin: "12px 0" }} />
          <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 4 }}>
            <span style={{ fontSize: 13, color: TEXTMD }}>Subtotal</span>
            <span style={{ fontFamily: "'JetBrains Mono', monospace", fontSize: 13, color: TEXTMD }}>{TL(subtotal)}</span>
          </div>
          {discount && (
            <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 4 }}>
              <span style={{ fontSize: 13, color: AMBER }}>{discount.label}</span>
              <span style={{ fontFamily: "'JetBrains Mono', monospace", fontSize: 13, color: AMBER }}>−{TL(discountAmt)}</span>
            </div>
          )}
          <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 12 }}>
            <span style={{ fontSize: 13, color: TEXTMD }}>Service charge (12%)</span>
            <span style={{ fontFamily: "'JetBrains Mono', monospace", fontSize: 13, color: TEXTMD }}>{TL(service)}</span>
          </div>
          <div style={{ display: "flex", justifyContent: "space-between" }}>
            <span style={{ fontSize: 17, fontWeight: 700, color: TEXT }}>Total</span>
            <span style={{ fontFamily: "'JetBrains Mono', monospace", fontSize: 22, fontWeight: 700, color: TEXT }}>{TL(total)}</span>
          </div>
        </div>

        {/* Actions */}
        <div style={{ padding: "12px 20px 20px", borderTop: `1px solid ${BORDER}`, display: "flex", flexDirection: "column", gap: 8 }}>
          <div style={{ display: "flex", gap: 8 }}>
            <button style={{
              flex: 1, padding: "11px", background: CARD, border: `1px solid ${BORDER}`,
              borderRadius: 8, color: TEXTMD, fontFamily: "'DM Sans', sans-serif",
              fontSize: 13, fontWeight: 500, cursor: "pointer",
            }}>🖨 Print</button>
            <button style={{
              flex: 1, padding: "11px", background: CARD, border: `1px solid ${BORDER}`,
              borderRadius: 8, color: TEXTMD, fontFamily: "'DM Sans', sans-serif",
              fontSize: 13, fontWeight: 500, cursor: "pointer",
            }}>✉ Email / SMS</button>
          </div>
          <button onClick={onNewSale} style={{
            width: "100%", padding: "14px", background: INDIGO, border: "none",
            borderRadius: 8, color: "white", fontFamily: "'DM Sans', sans-serif",
            fontSize: 15, fontWeight: 700, cursor: "pointer",
          }}>New Sale →</button>
        </div>
      </div>
    </div>
  );
}

/* ════════════════════════════════════════════════
   Root POS
════════════════════════════════════════════════ */
export default function App() {
  const [mode, setMode] = useState<POSMode>("pos");
  const [category, setCategory] = useState("quick");
  const [search, setSearch] = useState("");
  const [order, setOrder] = useState<OrderLine[]>([]);
  const [discount, setDiscount] = useState<Discount | null>(null);
  const [table, setTable] = useState<number | null>(7);
  const [showTable, setShowTable] = useState(false);
  const [showDiscount, setShowDiscount] = useState(false);
  const [showRefund, setShowRefund] = useState(false);
  const [showSession, setShowSession] = useState(false);
  const [sessionOpen] = useState(true);
  const searchRef = useRef<HTMLInputElement>(null);

  const now = new Date();
  const clockStr = `${String(now.getHours()).padStart(2, "0")}:${String(now.getMinutes()).padStart(2, "0")}`;

  /* ─ Derived totals ─ */
  const subtotal = useMemo(() => order.reduce((s, l) => s + l.item.price * l.qty, 0), [order]);
  const discountAmt = useMemo(() => {
    if (!discount) return 0;
    return discount.type === "pct" ? Math.round(subtotal * discount.value / 100) : discount.value;
  }, [discount, subtotal]);
  const service = Math.round((subtotal - discountAmt) * 0.12);
  const total = subtotal - discountAmt + service;

  /* ─ Order mutations ─ */
  const addItem = useCallback((product: POSProduct) => {
    setOrder(prev => {
      const existing = prev.find(l => l.item.id === product.id);
      if (existing) return prev.map(l => l.item.id === product.id ? { ...l, qty: l.qty + 1 } : l);
      return [...prev, { item: product, qty: 1 }];
    });
  }, []);

  const setQty = useCallback((id: string, qty: number) => {
    setOrder(prev => prev.map(l => l.item.id === id ? { ...l, qty } : l));
  }, []);

  const removeLine = useCallback((id: string) => {
    setOrder(prev => prev.filter(l => l.item.id !== id));
  }, []);

  const clearOrder = () => { setOrder([]); setDiscount(null); };

  /* ─ Filtered products ─ */
  const products = useMemo(() => {
    if (search.trim()) {
      const q = search.toLowerCase();
      return PRODUCTS.filter(p => p.name.toLowerCase().includes(q));
    }
    if (category === "quick") return QUICK;
    return PRODUCTS.filter(p => p.category === category);
  }, [category, search]);

  if (mode === "receipt") {
    return (
      <div style={{ minHeight: "100vh", background: BG, display: "flex", flexDirection: "column" }}>
        <div style={{
          height: 52, background: SURF, borderBottom: `1px solid ${BORDER}`,
          display: "flex", alignItems: "center", padding: "0 20px", gap: 12,
        }}>
          <div style={{
            width: 28, height: 28, borderRadius: 8,
            background: "linear-gradient(135deg,#C4622D,#E8915A)",
            display: "flex", alignItems: "center", justifyContent: "center",
          }}>
            <span style={{ fontFamily: "'Fraunces', serif", fontSize: 14, color: "white" }}>M</span>
          </div>
          <span style={{ fontSize: 13, fontWeight: 600, color: TEXT }}>Payment Complete</span>
          <div style={{ marginLeft: "auto", display: "flex", alignItems: "center", gap: 6 }}>
            <div style={{ width: 8, height: 8, borderRadius: "50%", background: GREEN }} />
            <span style={{ fontSize: 12, color: GREEN, fontWeight: 600 }}>PAID</span>
          </div>
        </div>
        <ReceiptView
          lines={order} total={total} discount={discount} table={table}
          onNewSale={() => { clearOrder(); setMode("pos"); }}
        />
      </div>
    );
  }

  return (
    <div style={{ minHeight: "100vh", display: "flex", flexDirection: "column", background: BG, fontFamily: "'DM Sans', sans-serif", color: TEXT }}>

      {/* ── Top bar ── */}
      <header style={{
        height: 52, flexShrink: 0,
        background: SURF, borderBottom: `1px solid ${BORDER}`,
        display: "flex", alignItems: "center", padding: "0 16px", gap: 12,
      }}>
        {/* Brand */}
        <div style={{ display: "flex", alignItems: "center", gap: 10, marginRight: 4 }}>
          <div style={{
            width: 28, height: 28, borderRadius: 8,
            background: "linear-gradient(135deg,#C4622D,#E8915A)",
            display: "flex", alignItems: "center", justifyContent: "center",
          }}>
            <span style={{ fontFamily: "'Fraunces', serif", fontSize: 14, color: "white" }}>M</span>
          </div>
          <span style={{
            fontFamily: "'JetBrains Mono', monospace",
            fontSize: 11, fontWeight: 700, letterSpacing: "0.15em", color: TEXTDM, textTransform: "uppercase",
          }}>Point of Sale</span>
        </div>

        <div style={{ width: 1, height: 20, background: BORDER }} />

        {/* Table selector */}
        <button onClick={() => setShowTable(true)} style={{
          display: "flex", alignItems: "center", gap: 8,
          padding: "7px 14px", borderRadius: 8,
          background: table ? INDIGODM : CARD,
          border: `1px solid ${table ? INDIGOBR : BORDER}`,
          color: table ? INDIGO : TEXTMD,
          fontFamily: "'DM Sans', sans-serif", fontSize: 13, fontWeight: 600,
          cursor: "pointer",
        }}>
          <span style={{ fontSize: 14 }}>⊟</span>
          {table ? `Table ${table}` : "Walk-in / No Table"}
          <span style={{ fontSize: 11, color: TEXTDM }}>▾</span>
        </button>

        {/* Cashier */}
        <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
          <div style={{
            width: 26, height: 26, borderRadius: "50%",
            background: INDIGODM, border: `1px solid ${INDIGOBR}`,
            display: "flex", alignItems: "center", justifyContent: "center",
            fontSize: 11, fontWeight: 700, color: INDIGO,
          }}>Z</div>
          <span style={{ fontSize: 12, color: TEXTMD }}>Zeynep · Cashier</span>
        </div>

        <div style={{ flex: 1 }} />

        {/* Session status */}
        <button onClick={() => setShowSession(true)} style={{
          display: "flex", alignItems: "center", gap: 8,
          padding: "7px 14px", borderRadius: 8,
          background: sessionOpen ? GREENDM : REDDM,
          border: `1px solid ${sessionOpen ? GREENBR : REDBR}`,
          cursor: "pointer",
        }}>
          <div style={{ width: 7, height: 7, borderRadius: "50%", background: sessionOpen ? GREEN : RED }} />
          <span style={{ fontSize: 12, fontWeight: 600, color: sessionOpen ? GREEN : RED }}>
            Session Open · ₺2,000 float
          </span>
        </button>

        {/* Refund */}
        <button onClick={() => setShowRefund(true)} style={{
          padding: "7px 14px", borderRadius: 8,
          background: REDDM, border: `1px solid ${REDBR}`,
          color: RED, fontFamily: "'DM Sans', sans-serif",
          fontSize: 12, fontWeight: 600, cursor: "pointer",
        }}>↩ Refund</button>

        {/* Clock */}
        <span style={{
          fontFamily: "'JetBrains Mono', monospace",
          fontSize: 16, fontWeight: 700, color: TEXT, minWidth: 48, textAlign: "right",
        }}>{clockStr}</span>
      </header>

      {/* ── Main area ── */}
      <div style={{ flex: 1, display: "flex", overflow: "hidden" }}>

        {/* ── Product panel ── */}
        <div style={{ flex: 1, display: "flex", flexDirection: "column", overflow: "hidden", borderRight: `1px solid ${BORDER}` }}>

          {/* Search + category tabs */}
          <div style={{ background: SURF, borderBottom: `1px solid ${BORDER}`, flexShrink: 0 }}>
            {/* Search */}
            <div style={{ padding: "10px 14px 0" }}>
              <div style={{
                display: "flex", alignItems: "center", gap: 10,
                background: CARD, border: `1px solid ${BORDER}`,
                borderRadius: 9, padding: "0 14px", height: 40,
              }}>
                <span style={{ fontSize: 15, color: TEXTDM }}>🔍</span>
                <input
                  ref={searchRef}
                  value={search} onChange={e => setSearch(e.target.value)}
                  placeholder="Search products…"
                  style={{
                    flex: 1, background: "none", border: "none", outline: "none",
                    fontFamily: "'DM Sans', sans-serif", fontSize: 13, color: TEXT,
                  }}
                />
                {search && (
                  <button onClick={() => setSearch("")} style={{ background: "none", border: "none", color: TEXTDM, cursor: "pointer", fontSize: 16 }}>×</button>
                )}
              </div>
            </div>

            {/* Category tabs */}
            <div style={{ display: "flex", gap: 4, padding: "10px 14px", overflowX: "auto", scrollbarWidth: "none" }}>
              {CATEGORIES.map(cat => (
                <button key={cat.id} onClick={() => { setCategory(cat.id); setSearch(""); }} style={{
                  flexShrink: 0, padding: "8px 16px", borderRadius: 8,
                  background: category === cat.id && !search ? INDIGODM : CARD,
                  border: `1px solid ${category === cat.id && !search ? INDIGOBR : BORDER}`,
                  color: category === cat.id && !search ? INDIGO : TEXTMD,
                  fontFamily: "'DM Sans', sans-serif", fontSize: 13, fontWeight: category === cat.id && !search ? 600 : 400,
                  cursor: "pointer", display: "flex", alignItems: "center", gap: 6, whiteSpace: "nowrap",
                }}>
                  <span>{cat.icon}</span>
                  {cat.label}
                </button>
              ))}
            </div>
          </div>

          {/* Product grid */}
          <div style={{
            flex: 1, overflowY: "auto", padding: "12px 14px",
            display: "grid",
            gridTemplateColumns: "repeat(auto-fill, minmax(160px, 1fr))",
            gridAutoRows: "min-content",
            gap: 8, alignContent: "start",
          }}>
            {products.map(p => (
              <ProductBtn key={p.id} product={p} onAdd={addItem} />
            ))}
            {products.length === 0 && (
              <div style={{ gridColumn: "1/-1", textAlign: "center", padding: "60px 0", color: TEXTDM, fontSize: 14 }}>
                No products found
              </div>
            )}
          </div>
        </div>

        {/* ── Order panel ── */}
        <div style={{ width: 380, flexShrink: 0, display: "flex", flexDirection: "column", background: SURF }}>

          {/* Order header */}
          <div style={{
            padding: "13px 16px", borderBottom: `1px solid ${BORDER}`,
            display: "flex", justifyContent: "space-between", alignItems: "center",
            flexShrink: 0,
          }}>
            <div>
              <span style={{ fontSize: 14, fontWeight: 700, color: TEXT }}>
                {table ? `Table ${table}` : "Walk-in"}
              </span>
              {order.length > 0 && (
                <span style={{ fontSize: 12, color: TEXTDM, marginLeft: 10 }}>
                  {order.reduce((s, l) => s + l.qty, 0)} items
                </span>
              )}
            </div>
            {order.length > 0 && (
              <button onClick={clearOrder} style={{
                padding: "5px 12px", background: REDDM, border: `1px solid ${REDBR}`,
                borderRadius: 6, color: RED, fontSize: 12, fontWeight: 600, cursor: "pointer",
              }}>Clear</button>
            )}
          </div>

          {/* Line items */}
          <div style={{ flex: 1, overflowY: "auto" }}>
            {order.length === 0 ? (
              <div style={{
                display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center",
                height: "100%", gap: 12, padding: 32, textAlign: "center",
              }}>
                <div style={{
                  width: 56, height: 56, borderRadius: 14, background: CARD,
                  border: `1px solid ${BORDER}`, display: "flex", alignItems: "center",
                  justifyContent: "center", fontSize: 24,
                }}>◻</div>
                <div style={{ fontSize: 14, color: TEXTDM }}>Tap products to add to order</div>
              </div>
            ) : (
              order.map(line => (
                <OrderLineRow
                  key={line.item.id} line={line}
                  onChange={qty => setQty(line.item.id, qty)}
                  onRemove={() => removeLine(line.item.id)}
                />
              ))
            )}
          </div>

          {/* Totals + Actions */}
          <div style={{ flexShrink: 0, borderTop: `1px solid ${BORDER}` }}>
            {/* Discount row */}
            <div style={{
              padding: "10px 16px",
              borderBottom: `1px solid ${BORDER}`,
              display: "flex", justifyContent: "space-between", alignItems: "center",
            }}>
              {discount ? (
                <>
                  <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                    <span style={{
                      fontFamily: "'JetBrains Mono', monospace",
                      fontSize: 12, color: AMBER, fontWeight: 600,
                      background: AMBDM, border: `1px solid rgba(217,119,6,0.2)`,
                      padding: "2px 8px", borderRadius: 4,
                    }}>{discount.label}</span>
                    <span style={{ fontFamily: "'JetBrains Mono', monospace", fontSize: 13, color: AMBER }}>
                      −{TL(discountAmt)}
                    </span>
                  </div>
                  <button onClick={() => setDiscount(null)} style={{
                    background: "none", border: "none", color: TEXTDM, cursor: "pointer", fontSize: 16,
                  }}>×</button>
                </>
              ) : (
                <button onClick={() => setShowDiscount(true)} style={{
                  background: "none", border: "none", color: TEXTDM,
                  fontFamily: "'DM Sans', sans-serif", fontSize: 13, cursor: "pointer",
                  display: "flex", alignItems: "center", gap: 6,
                }}>
                  <span style={{ fontSize: 16 }}>⊕</span> Add discount
                </button>
              )}
            </div>

            {/* Totals */}
            <div style={{ padding: "12px 16px" }}>
              {[
                { label: "Subtotal", val: TL(subtotal), dim: true },
                ...(discount ? [{ label: discount.label, val: `−${TL(discountAmt)}`, dim: false, amber: true }] : []),
                { label: "Service (12%)", val: TL(service), dim: true },
              ].map(r => (
                <div key={r.label} style={{ display: "flex", justifyContent: "space-between", marginBottom: 5 }}>
                  <span style={{ fontSize: 13, color: TEXTDM }}>{r.label}</span>
                  <span style={{
                    fontFamily: "'JetBrains Mono', monospace",
                    fontSize: 13, color: (r as { amber?: boolean }).amber ? AMBER : TEXTMD,
                  }}>{r.val}</span>
                </div>
              ))}
              <div style={{ height: 1, background: BORDER, margin: "10px 0" }} />
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "baseline" }}>
                <span style={{ fontSize: 15, fontWeight: 700, color: TEXT }}>Total</span>
                <span style={{
                  fontFamily: "'JetBrains Mono', monospace",
                  fontSize: 28, fontWeight: 700, color: TEXT, letterSpacing: "-0.5px",
                }}>{TL(total)}</span>
              </div>
            </div>

            {/* Pay button */}
            <div style={{ padding: "0 14px 14px" }}>
              <button
                disabled={order.length === 0}
                onClick={() => setMode("payment")}
                style={{
                  width: "100%", height: 56, borderRadius: 12,
                  background: order.length > 0 ? INDIGO : TEXTDM,
                  border: "none", color: "white",
                  fontFamily: "'DM Sans', sans-serif", fontSize: 17, fontWeight: 700,
                  cursor: order.length > 0 ? "pointer" : "not-allowed",
                  display: "flex", alignItems: "center", justifyContent: "space-between",
                  padding: "0 20px",
                  transition: "background 0.15s",
                }}
              >
                <span>Charge</span>
                <span style={{ fontFamily: "'JetBrains Mono', monospace", fontSize: 18, fontWeight: 700 }}>{TL(total)}</span>
              </button>
            </div>
          </div>
        </div>
      </div>

      {/* ── Modals ── */}
      {mode === "payment" && (
        <PaymentModal
          total={total} subtotal={subtotal} discount={discount}
          onClose={() => setMode("pos")}
          onComplete={() => setMode("receipt")}
        />
      )}

      {showTable && (
        <TablePicker
          selected={table}
          onSelect={n => { setTable(n); setShowTable(false); }}
          onWalkIn={() => { setTable(null); setShowTable(false); }}
          onClose={() => setShowTable(false)}
        />
      )}

      {showDiscount && (
        <DiscountModal
          onApply={d => { setDiscount(d); setShowDiscount(false); }}
          onClose={() => setShowDiscount(false)}
        />
      )}

      {showRefund && <RefundModal onClose={() => setShowRefund(false)} />}
      {showSession && <CashSessionModal isOpen={sessionOpen} onClose={() => setShowSession(false)} />}

      <style>{`
        * { box-sizing: border-box; }
        ::-webkit-scrollbar { width: 4px; }
        ::-webkit-scrollbar-track { background: transparent; }
        ::-webkit-scrollbar-thumb { background: ${BORDER}; border-radius: 99px; }
        input::placeholder { color: ${TEXTDM}; }
      `}</style>
    </div>
  );
}
