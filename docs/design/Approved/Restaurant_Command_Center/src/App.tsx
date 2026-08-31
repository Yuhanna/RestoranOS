import { useState, useEffect, useRef, useCallback } from "react";
import {
  RESTAURANT, CATEGORIES, MENU, ALLERGEN_LABELS, DIETARY_LABELS,
  type MenuItem, type AllergenKey, type DietaryTag,
} from "./data";

/* ─── tokens ─── */
const T = {
  bg: "#FAFAF7",
  surface: "#FFFFFF",
  text: "#1A1209",
  textMuted: "#7C6F63",
  textFaint: "#B5A99A",
  accent: "#C4622D",
  accentLight: "#FDF0EA",
  accentMid: "#E8915A",
  border: "rgba(26,18,9,0.08)",
  borderMid: "rgba(26,18,9,0.12)",
  shadow: "0 2px 16px rgba(26,18,9,0.07)",
  shadowLg: "0 8px 40px rgba(26,18,9,0.14)",
  radius: 20,
  radiusSm: 12,
  radiusPill: 999,
};

/* ─── types ─── */
interface CartItem {
  key: string;
  product: MenuItem;
  qty: number;
  selections: Record<string, string[]>;
  lineTotal: number;
}

type Screen = "qr" | "menu" | "product" | "cart";

/* ─── helpers ─── */
const fmt = (n: number) => `₺${n.toLocaleString("tr-TR")}`;

function calcItemTotal(product: MenuItem, selections: Record<string, string[]>, qty: number): number {
  let base = product.price;
  for (const [groupId, chosen] of Object.entries(selections)) {
    const group = product.modifiers.find(g => g.id === groupId);
    if (!group) continue;
    for (const optId of chosen) {
      const opt = group.options.find(o => o.id === optId);
      if (opt?.priceAddon) base += opt.priceAddon;
    }
  }
  return base * qty;
}

/* ════════════════════════════════════════════════
   QR Resolution Screen
════════════════════════════════════════════════ */
function QRScreen({ onReady }: { onReady: () => void }) {
  const [phase, setPhase] = useState<"scanning" | "found" | "loading">("scanning");

  useEffect(() => {
    const t1 = setTimeout(() => setPhase("found"), 900);
    const t2 = setTimeout(() => setPhase("loading"), 1700);
    const t3 = setTimeout(() => onReady(), 2600);
    return () => { clearTimeout(t1); clearTimeout(t2); clearTimeout(t3); };
  }, [onReady]);

  return (
    <div style={{
      flex: 1, display: "flex", flexDirection: "column",
      alignItems: "center", justifyContent: "center",
      background: T.text, padding: 32, gap: 0,
    }}>
      {/* Logo mark */}
      <div style={{
        width: 80, height: 80, borderRadius: 24,
        background: T.accent, display: "flex", alignItems: "center", justifyContent: "center",
        marginBottom: 32,
        boxShadow: `0 0 0 ${phase === "found" ? "12px" : "0px"} rgba(196,98,45,0.2)`,
        transition: "box-shadow 0.5s ease",
      }}>
        <span style={{ fontFamily: "'Fraunces', serif", fontSize: 36, color: "white", fontWeight: 400 }}>M</span>
      </div>

      <div style={{
        opacity: phase === "scanning" ? 1 : 0,
        transition: "opacity 0.3s",
        position: "absolute",
        display: "flex", flexDirection: "column", alignItems: "center", gap: 16,
        marginTop: 80,
      }}>
        <div style={{ display: "flex", gap: 6 }}>
          {[0, 1, 2].map(i => (
            <div key={i} style={{
              width: 6, height: 6, borderRadius: "50%", background: "rgba(255,255,255,0.3)",
              animation: `pulse 1.2s ease-in-out ${i * 0.2}s infinite`,
            }} />
          ))}
        </div>
        <p style={{ fontFamily: "'DM Sans', sans-serif", fontSize: 14, color: "rgba(255,255,255,0.4)", margin: 0 }}>
          Reading QR code…
        </p>
      </div>

      <div style={{
        opacity: phase !== "scanning" ? 1 : 0,
        transition: "opacity 0.4s",
        display: "flex", flexDirection: "column", alignItems: "center", gap: 8,
        marginTop: 8,
      }}>
        <p style={{
          fontFamily: "'DM Sans', sans-serif", fontSize: 12, fontWeight: 600,
          letterSpacing: "0.15em", textTransform: "uppercase",
          color: T.accent, margin: "0 0 8px",
        }}>
          {RESTAURANT.branch} · Table {RESTAURANT.table}
        </p>
        <h1 style={{
          fontFamily: "'Fraunces', serif", fontSize: 32, fontWeight: 400,
          color: "white", margin: 0, letterSpacing: "-0.3px", textAlign: "center",
        }}>
          {RESTAURANT.name}
        </h1>
        <p style={{ fontFamily: "'DM Sans', sans-serif", fontSize: 14, color: "rgba(255,255,255,0.45)", margin: "4px 0 0" }}>
          {RESTAURANT.tagline}
        </p>
      </div>

      {phase === "loading" && (
        <div style={{ position: "absolute", bottom: 80, left: 32, right: 32 }}>
          <div style={{ height: 2, background: "rgba(255,255,255,0.1)", borderRadius: 99, overflow: "hidden" }}>
            <div style={{
              height: "100%", background: T.accent, borderRadius: 99,
              animation: "fillBar 0.8s ease forwards",
            }} />
          </div>
        </div>
      )}

      <style>{`
        @keyframes pulse {
          0%, 100% { opacity: 0.3; transform: scale(1); }
          50% { opacity: 1; transform: scale(1.3); }
        }
        @keyframes fillBar {
          from { width: 0%; }
          to { width: 100%; }
        }
        @keyframes slideUp {
          from { transform: translateY(100%); opacity: 0; }
          to { transform: translateY(0); opacity: 1; }
        }
        @keyframes fadeIn {
          from { opacity: 0; transform: translateY(6px); }
          to { opacity: 1; transform: translateY(0); }
        }
        @keyframes scaleIn {
          from { opacity: 0; transform: scale(0.95); }
          to { opacity: 1; transform: scale(1); }
        }
      `}</style>
    </div>
  );
}

/* ════════════════════════════════════════════════
   Dietary & Allergen Chips
════════════════════════════════════════════════ */
function DietaryBadge({ tag }: { tag: DietaryTag }) {
  const d = DIETARY_LABELS[tag];
  return (
    <span style={{
      display: "inline-flex", alignItems: "center",
      background: d.bg, color: d.color,
      fontSize: 10, fontWeight: 700, letterSpacing: "0.04em",
      padding: "3px 7px", borderRadius: T.radiusPill,
    }}>{d.label}</span>
  );
}

function AllergenChip({ k }: { k: AllergenKey }) {
  return (
    <span style={{
      display: "inline-flex", alignItems: "center", gap: 4,
      background: "rgba(26,18,9,0.05)", color: T.textMuted,
      fontSize: 11, fontWeight: 500,
      padding: "4px 10px", borderRadius: T.radiusPill,
      border: `1px solid ${T.border}`,
    }}>
      {ALLERGEN_LABELS[k]}
    </span>
  );
}

/* ════════════════════════════════════════════════
   Product Card
════════════════════════════════════════════════ */
function ProductCard({ item, onTap, onAdd }: {
  item: MenuItem;
  onTap: () => void;
  onAdd: (e: React.MouseEvent) => void;
}) {
  const [pressed, setPressed] = useState(false);
  return (
    <div
      onClick={onTap}
      onMouseDown={() => setPressed(true)}
      onMouseUp={() => setPressed(false)}
      onMouseLeave={() => setPressed(false)}
      style={{
        background: T.surface, borderRadius: T.radius,
        overflow: "hidden", cursor: "pointer",
        boxShadow: pressed ? "0 1px 4px rgba(26,18,9,0.06)" : T.shadow,
        transform: pressed ? "scale(0.985)" : "scale(1)",
        transition: "transform 0.12s, box-shadow 0.12s",
        display: "flex", flexDirection: "column",
        animation: "fadeIn 0.3s ease both",
      }}
    >
      {/* Image */}
      <div style={{ position: "relative", height: 156, background: "#EDE0D4", overflow: "hidden" }}>
        <img
          src={item.img} alt={item.name}
          style={{ width: "100%", height: "100%", objectFit: "cover", display: "block" }}
          loading="lazy"
        />
        {/* Gradient overlay */}
        <div style={{ position: "absolute", inset: 0, background: "linear-gradient(180deg, transparent 50%, rgba(26,18,9,0.18) 100%)" }} />
        {item.badge && (
          <div style={{
            position: "absolute", top: 10, left: 10,
            background: T.accent, color: "white",
            fontSize: 10, fontWeight: 700, letterSpacing: "0.05em",
            padding: "4px 10px", borderRadius: T.radiusPill,
          }}>
            {item.badge}
          </div>
        )}
      </div>

      {/* Body */}
      <div style={{ padding: "14px 14px 16px", flex: 1, display: "flex", flexDirection: "column", gap: 6 }}>
        <div style={{ display: "flex", gap: 4, flexWrap: "wrap" }}>
          {item.tags.map(t => <DietaryBadge key={t} tag={t} />)}
        </div>

        <h3 style={{
          fontFamily: "'Fraunces', serif", fontSize: 16, fontWeight: 400,
          color: T.text, margin: 0, lineHeight: 1.25,
        }}>{item.name}</h3>

        <p style={{
          fontFamily: "'DM Sans', sans-serif", fontSize: 12,
          color: T.textMuted, margin: 0, lineHeight: 1.5,
          display: "-webkit-box", WebkitLineClamp: 2,
          WebkitBoxOrient: "vertical", overflow: "hidden",
        }}>{item.desc}</p>

        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginTop: "auto", paddingTop: 8 }}>
          <span style={{
            fontFamily: "'Fraunces', serif", fontSize: 18, fontWeight: 500, color: T.accent,
          }}>{fmt(item.price)}</span>

          <button
            onClick={onAdd}
            onMouseDown={e => e.stopPropagation()}
            style={{
              width: 34, height: 34, borderRadius: "50%",
              background: T.accent, border: "none",
              color: "white", fontSize: 20, lineHeight: 1,
              cursor: "pointer", display: "flex", alignItems: "center", justifyContent: "center",
              flexShrink: 0, transition: "transform 0.1s, background 0.1s",
            }}
            onMouseEnter={e => { (e.currentTarget as HTMLElement).style.background = "#A8501F"; }}
            onMouseLeave={e => { (e.currentTarget as HTMLElement).style.background = T.accent; }}
          >+</button>
        </div>
      </div>
    </div>
  );
}

/* ════════════════════════════════════════════════
   Product Detail Sheet
════════════════════════════════════════════════ */
function ProductSheet({ item, onClose, onAddToCart }: {
  item: MenuItem;
  onClose: () => void;
  onAddToCart: (item: MenuItem, selections: Record<string, string[]>, qty: number) => void;
}) {
  const [qty, setQty] = useState(1);
  const [selections, setSelections] = useState<Record<string, string[]>>(() => {
    const init: Record<string, string[]> = {};
    for (const g of item.modifiers) {
      if (g.options.length > 0) init[g.id] = [g.options[0].id];
    }
    return init;
  });

  const toggle = (groupId: string, optId: string, multi: boolean) => {
    setSelections(prev => {
      const cur = prev[groupId] ?? [];
      if (multi) {
        return { ...prev, [groupId]: cur.includes(optId) ? cur.filter(x => x !== optId) : [...cur, optId] };
      }
      return { ...prev, [groupId]: [optId] };
    });
  };

  const canAdd = item.modifiers.every(g => !g.required || (selections[g.id] ?? []).length > 0);
  const total = calcItemTotal(item, selections, qty);

  return (
    <div style={{
      position: "absolute", inset: 0, zIndex: 50,
      display: "flex", flexDirection: "column", justifyContent: "flex-end",
    }}>
      {/* Scrim */}
      <div
        onClick={onClose}
        style={{ position: "absolute", inset: 0, background: "rgba(10,8,6,0.5)", backdropFilter: "blur(2px)" }}
      />

      {/* Sheet */}
      <div style={{
        position: "relative", zIndex: 1,
        background: T.bg, borderRadius: "28px 28px 0 0",
        maxHeight: "90%", display: "flex", flexDirection: "column",
        animation: "slideUp 0.3s cubic-bezier(0.32,0.72,0,1)",
      }}>
        {/* Drag handle */}
        <div style={{ display: "flex", justifyContent: "center", padding: "12px 0 0" }}>
          <div style={{ width: 36, height: 4, borderRadius: 99, background: T.borderMid }} />
        </div>

        {/* Scrollable content */}
        <div style={{ overflowY: "auto", flex: 1 }}>
          {/* Hero image */}
          <div style={{ height: 240, background: "#EDE0D4", position: "relative", margin: "12px 16px 0", borderRadius: 20, overflow: "hidden" }}>
            <img src={item.img} alt={item.name} style={{ width: "100%", height: "100%", objectFit: "cover" }} />
            {item.badge && (
              <div style={{
                position: "absolute", top: 14, left: 14,
                background: T.accent, color: "white",
                fontSize: 11, fontWeight: 700, padding: "4px 12px", borderRadius: T.radiusPill,
              }}>{item.badge}</div>
            )}
            <button
              onClick={onClose}
              style={{
                position: "absolute", top: 14, right: 14,
                width: 32, height: 32, borderRadius: "50%",
                background: "rgba(0,0,0,0.35)", backdropFilter: "blur(8px)",
                border: "none", color: "white", fontSize: 18,
                cursor: "pointer", display: "flex", alignItems: "center", justifyContent: "center",
              }}
            >×</button>
          </div>

          <div style={{ padding: "20px 20px 0" }}>
            {/* Dietary tags */}
            {item.tags.length > 0 && (
              <div style={{ display: "flex", gap: 6, flexWrap: "wrap", marginBottom: 10 }}>
                {item.tags.map(t => <DietaryBadge key={t} tag={t} />)}
              </div>
            )}

            {/* Name + price */}
            <div style={{ display: "flex", alignItems: "flex-start", justifyContent: "space-between", gap: 12, marginBottom: 8 }}>
              <h2 style={{
                fontFamily: "'Fraunces', serif", fontSize: 26, fontWeight: 400,
                color: T.text, margin: 0, lineHeight: 1.15, flex: 1,
              }}>{item.name}</h2>
              <span style={{
                fontFamily: "'Fraunces', serif", fontSize: 24, fontWeight: 500,
                color: T.accent, flexShrink: 0, paddingTop: 2,
              }}>{fmt(item.price)}</span>
            </div>

            {/* Description */}
            <p style={{
              fontFamily: "'DM Sans', sans-serif", fontSize: 14, color: T.textMuted,
              lineHeight: 1.65, margin: "0 0 20px",
            }}>{item.longDesc}</p>

            {/* Allergens */}
            {item.allergens.length > 0 && (
              <div style={{ marginBottom: 24 }}>
                <p style={{
                  fontFamily: "'DM Sans', sans-serif", fontSize: 11, fontWeight: 600,
                  letterSpacing: "0.12em", textTransform: "uppercase", color: T.textFaint,
                  margin: "0 0 8px",
                }}>Contains</p>
                <div style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
                  {item.allergens.map(a => <AllergenChip key={a} k={a} />)}
                </div>
              </div>
            )}

            {/* Modifiers */}
            {item.modifiers.map(group => (
              <div key={group.id} style={{ marginBottom: 24 }}>
                <div style={{ display: "flex", alignItems: "baseline", gap: 8, marginBottom: 12 }}>
                  <p style={{
                    fontFamily: "'DM Sans', sans-serif", fontSize: 14, fontWeight: 600,
                    color: T.text, margin: 0,
                  }}>{group.label}</p>
                  <span style={{
                    fontSize: 11, color: group.required ? T.accent : T.textFaint,
                    fontWeight: 500,
                  }}>{group.required ? "Required" : "Optional"}</span>
                </div>
                <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                  {group.options.map(opt => {
                    const chosen = (selections[group.id] ?? []).includes(opt.id);
                    return (
                      <button
                        key={opt.id}
                        onClick={() => toggle(group.id, opt.id, group.multi)}
                        style={{
                          display: "flex", alignItems: "center", justifyContent: "space-between",
                          padding: "13px 16px",
                          background: chosen ? T.accentLight : T.surface,
                          border: `1.5px solid ${chosen ? T.accent : T.border}`,
                          borderRadius: T.radiusSm, cursor: "pointer",
                          transition: "all 0.15s",
                        }}
                      >
                        <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
                          <div style={{
                            width: group.multi ? 18 : 18, height: group.multi ? 18 : 18,
                            borderRadius: group.multi ? 5 : "50%",
                            border: `2px solid ${chosen ? T.accent : T.borderMid}`,
                            background: chosen ? T.accent : "transparent",
                            display: "flex", alignItems: "center", justifyContent: "center",
                            flexShrink: 0, transition: "all 0.15s",
                          }}>
                            {chosen && <span style={{ color: "white", fontSize: 11, lineHeight: 1 }}>{group.multi ? "✓" : ""}</span>}
                            {chosen && !group.multi && <div style={{ width: 7, height: 7, borderRadius: "50%", background: "white" }} />}
                          </div>
                          <span style={{
                            fontFamily: "'DM Sans', sans-serif", fontSize: 14,
                            color: T.text, fontWeight: chosen ? 500 : 400,
                          }}>{opt.label}</span>
                        </div>
                        {opt.priceAddon && (
                          <span style={{
                            fontFamily: "'DM Sans', sans-serif", fontSize: 13,
                            color: T.accent, fontWeight: 600,
                          }}>+{fmt(opt.priceAddon)}</span>
                        )}
                      </button>
                    );
                  })}
                </div>
              </div>
            ))}

            {/* Bottom spacer */}
            <div style={{ height: 130 }} />
          </div>
        </div>

        {/* Sticky CTA */}
        <div style={{
          padding: "16px 20px 32px",
          background: T.bg,
          borderTop: `1px solid ${T.border}`,
        }}>
          <div style={{ display: "flex", gap: 12, alignItems: "center" }}>
            {/* Qty stepper */}
            <div style={{
              display: "flex", alignItems: "center", gap: 0,
              background: T.surface, border: `1.5px solid ${T.border}`,
              borderRadius: T.radiusSm, overflow: "hidden",
            }}>
              <button
                onClick={() => setQty(q => Math.max(1, q - 1))}
                style={{
                  width: 42, height: 48, background: "none", border: "none",
                  fontSize: 20, color: qty === 1 ? T.textFaint : T.text, cursor: "pointer",
                }}
              >−</button>
              <span style={{
                width: 28, textAlign: "center",
                fontFamily: "'DM Sans', sans-serif", fontSize: 15, fontWeight: 600, color: T.text,
              }}>{qty}</span>
              <button
                onClick={() => setQty(q => q + 1)}
                style={{
                  width: 42, height: 48, background: "none", border: "none",
                  fontSize: 20, color: T.text, cursor: "pointer",
                }}
              >+</button>
            </div>

            {/* Add to cart */}
            <button
              disabled={!canAdd}
              onClick={() => { onAddToCart(item, selections, qty); onClose(); }}
              style={{
                flex: 1, height: 48, borderRadius: T.radiusSm, border: "none",
                background: canAdd ? T.accent : T.textFaint, color: "white",
                fontFamily: "'DM Sans', sans-serif", fontSize: 15, fontWeight: 600,
                cursor: canAdd ? "pointer" : "not-allowed",
                display: "flex", alignItems: "center", justifyContent: "space-between",
                padding: "0 20px", transition: "background 0.15s",
              }}
            >
              <span>Add to order</span>
              <span style={{ fontFamily: "'Fraunces', serif", fontSize: 16, fontWeight: 500 }}>{fmt(total)}</span>
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

/* ════════════════════════════════════════════════
   Cart Sheet
════════════════════════════════════════════════ */
function CartSheet({ cart, onClose, onUpdateQty, onRemove }: {
  cart: CartItem[];
  onClose: () => void;
  onUpdateQty: (key: string, qty: number) => void;
  onRemove: (key: string) => void;
}) {
  const total = cart.reduce((s, i) => s + i.lineTotal, 0);

  return (
    <div style={{ position: "absolute", inset: 0, zIndex: 50, display: "flex", flexDirection: "column", justifyContent: "flex-end" }}>
      <div onClick={onClose} style={{ position: "absolute", inset: 0, background: "rgba(10,8,6,0.5)", backdropFilter: "blur(2px)" }} />
      <div style={{
        position: "relative", zIndex: 1,
        background: T.bg, borderRadius: "28px 28px 0 0",
        maxHeight: "88%", display: "flex", flexDirection: "column",
        animation: "slideUp 0.3s cubic-bezier(0.32,0.72,0,1)",
      }}>
        <div style={{ display: "flex", justifyContent: "center", padding: "12px 0 0" }}>
          <div style={{ width: 36, height: 4, borderRadius: 99, background: T.borderMid }} />
        </div>
        <div style={{ padding: "16px 20px 0", display: "flex", justifyContent: "space-between", alignItems: "center" }}>
          <h2 style={{ fontFamily: "'Fraunces', serif", fontSize: 24, fontWeight: 400, color: T.text, margin: 0 }}>Your Order</h2>
          <button onClick={onClose} style={{ background: "none", border: "none", fontSize: 24, color: T.textMuted, cursor: "pointer", lineHeight: 1 }}>×</button>
        </div>
        <p style={{ fontFamily: "'DM Sans', sans-serif", fontSize: 13, color: T.textMuted, margin: "4px 20px 16px" }}>
          {RESTAURANT.name} · {RESTAURANT.branch} · Table {RESTAURANT.table}
        </p>

        <div style={{ overflowY: "auto", flex: 1, padding: "0 20px" }}>
          {cart.length === 0 ? (
            <div style={{ textAlign: "center", padding: "40px 0" }}>
              <p style={{ fontFamily: "'Fraunces', serif", fontStyle: "italic", fontSize: 18, color: T.textFaint }}>Nothing here yet</p>
            </div>
          ) : (
            <div style={{ display: "flex", flexDirection: "column", gap: 2 }}>
              {cart.map((item, idx) => (
                <div key={item.key} style={{
                  padding: "14px 0",
                  borderBottom: idx < cart.length - 1 ? `1px solid ${T.border}` : "none",
                  display: "flex", gap: 12, alignItems: "flex-start",
                }}>
                  <div style={{ width: 56, height: 56, borderRadius: 12, overflow: "hidden", background: "#EDE0D4", flexShrink: 0 }}>
                    <img src={item.product.img} alt={item.product.name} style={{ width: "100%", height: "100%", objectFit: "cover" }} />
                  </div>
                  <div style={{ flex: 1, minWidth: 0 }}>
                    <div style={{ fontFamily: "'DM Sans', sans-serif", fontSize: 14, fontWeight: 500, color: T.text, marginBottom: 3 }}>{item.product.name}</div>
                    {Object.entries(item.selections).map(([gId, opts]) => {
                      if (!opts.length) return null;
                      const group = item.product.modifiers.find(g => g.id === gId);
                      if (!group) return null;
                      const labels = opts.map(oId => group.options.find(o => o.id === oId)?.label ?? "").filter(Boolean);
                      return (
                        <div key={gId} style={{ fontSize: 12, color: T.textMuted, marginBottom: 2 }}>{labels.join(", ")}</div>
                      );
                    })}
                    <div style={{ display: "flex", alignItems: "center", gap: 8, marginTop: 8 }}>
                      <div style={{
                        display: "flex", alignItems: "center",
                        border: `1px solid ${T.border}`, borderRadius: 8, overflow: "hidden",
                      }}>
                        <button onClick={() => item.qty > 1 ? onUpdateQty(item.key, item.qty - 1) : onRemove(item.key)}
                          style={{ width: 30, height: 28, background: "none", border: "none", fontSize: 16, color: T.textMuted, cursor: "pointer" }}>
                          {item.qty === 1 ? "🗑" : "−"}
                        </button>
                        <span style={{ width: 20, textAlign: "center", fontSize: 13, fontWeight: 600, color: T.text }}>{item.qty}</span>
                        <button onClick={() => onUpdateQty(item.key, item.qty + 1)}
                          style={{ width: 30, height: 28, background: "none", border: "none", fontSize: 16, color: T.text, cursor: "pointer" }}>+</button>
                      </div>
                    </div>
                  </div>
                  <span style={{ fontFamily: "'Fraunces', serif", fontSize: 16, fontWeight: 500, color: T.accent, flexShrink: 0 }}>{fmt(item.lineTotal)}</span>
                </div>
              ))}
            </div>
          )}

          {cart.length > 0 && (
            <div style={{ padding: "20px 0 8px" }}>
              {[
                { label: "Subtotal", val: fmt(total) },
                { label: "Service charge (12%)", val: fmt(Math.round(total * 0.12)) },
              ].map(r => (
                <div key={r.label} style={{ display: "flex", justifyContent: "space-between", padding: "6px 0" }}>
                  <span style={{ fontFamily: "'DM Sans', sans-serif", fontSize: 14, color: T.textMuted }}>{r.label}</span>
                  <span style={{ fontFamily: "'DM Sans', sans-serif", fontSize: 14, color: T.text }}>{r.val}</span>
                </div>
              ))}
              <div style={{ height: 1, background: T.border, margin: "10px 0" }} />
              <div style={{ display: "flex", justifyContent: "space-between" }}>
                <span style={{ fontFamily: "'DM Sans', sans-serif", fontSize: 15, fontWeight: 700, color: T.text }}>Total</span>
                <span style={{ fontFamily: "'Fraunces', serif", fontSize: 20, fontWeight: 500, color: T.accent }}>{fmt(total + Math.round(total * 0.12))}</span>
              </div>
            </div>
          )}

          <div style={{ height: 110 }} />
        </div>

        {cart.length > 0 && (
          <div style={{ padding: "16px 20px 32px", borderTop: `1px solid ${T.border}`, background: T.bg }}>
            <button style={{
              width: "100%", height: 52, background: T.accent, color: "white", border: "none",
              borderRadius: T.radiusSm, fontFamily: "'DM Sans', sans-serif", fontSize: 16, fontWeight: 600,
              cursor: "pointer",
            }}>
              Place Order
            </button>
          </div>
        )}
      </div>
    </div>
  );
}

/* ════════════════════════════════════════════════
   Menu Screen
════════════════════════════════════════════════ */
function MenuScreen({
  onSelectProduct,
  onQuickAdd,
  cart,
  onOpenCart,
}: {
  onSelectProduct: (item: MenuItem) => void;
  onQuickAdd: (item: MenuItem) => void;
  cart: CartItem[];
  onOpenCart: () => void;
}) {
  const [activeCat, setActiveCat] = useState("all");
  const [search, setSearch] = useState("");
  const [searchOpen, setSearchOpen] = useState(false);
  const searchRef = useRef<HTMLInputElement>(null);
  const scrollRef = useRef<HTMLDivElement>(null);

  const filtered = MENU.filter(item => {
    const matchCat = activeCat === "all" || item.category === activeCat;
    const q = search.toLowerCase();
    const matchSearch = !q || item.name.toLowerCase().includes(q) || item.desc.toLowerCase().includes(q);
    return matchCat && matchSearch;
  });

  const cartCount = cart.reduce((s, i) => s + i.qty, 0);
  const cartTotal = cart.reduce((s, i) => s + i.lineTotal, 0);

  const openSearch = () => {
    setSearchOpen(true);
    setTimeout(() => searchRef.current?.focus(), 50);
  };
  const closeSearch = () => {
    setSearchOpen(false);
    setSearch("");
  };

  const groupedByCategory = activeCat === "all"
    ? CATEGORIES.slice(1).map(cat => ({
        cat,
        items: filtered.filter(i => i.category === cat.id),
      })).filter(g => g.items.length > 0)
    : [{ cat: CATEGORIES.find(c => c.id === activeCat)!, items: filtered }];

  return (
    <div style={{ flex: 1, display: "flex", flexDirection: "column", background: T.bg, overflow: "hidden" }}>

      {/* ── Header ── */}
      <div style={{
        background: T.surface,
        padding: "0 20px",
        borderBottom: `1px solid ${T.border}`,
        flexShrink: 0,
      }}>
        {/* Restaurant identity */}
        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", paddingTop: 20, paddingBottom: 14 }}>
          <div style={{ display: "flex", alignItems: "center", gap: 12 }}>
            <div style={{
              width: 44, height: 44, borderRadius: 14,
              background: T.accent, display: "flex", alignItems: "center", justifyContent: "center",
              flexShrink: 0,
            }}>
              <span style={{ fontFamily: "'Fraunces', serif", fontSize: 20, color: "white", fontWeight: 400 }}>M</span>
            </div>
            <div>
              <h1 style={{
                fontFamily: "'Fraunces', serif", fontSize: 20, fontWeight: 400,
                color: T.text, margin: 0, lineHeight: 1.1,
              }}>{RESTAURANT.name}</h1>
              <p style={{
                fontFamily: "'DM Sans', sans-serif", fontSize: 12, color: T.textMuted,
                margin: 0, marginTop: 2,
              }}>
                {RESTAURANT.branch} · Table {RESTAURANT.table}
              </p>
            </div>
          </div>

          {/* Search icon */}
          <button
            onClick={openSearch}
            style={{
              width: 38, height: 38, borderRadius: "50%",
              background: T.bg, border: `1px solid ${T.border}`,
              display: "flex", alignItems: "center", justifyContent: "center",
              cursor: "pointer", fontSize: 16,
            }}
          >
            🔍
          </button>
        </div>

        {/* Search bar (expanded) */}
        {searchOpen && (
          <div style={{
            display: "flex", alignItems: "center", gap: 10,
            background: T.bg, border: `1.5px solid ${T.accent}`,
            borderRadius: T.radiusSm, padding: "0 14px",
            marginBottom: 14,
            animation: "fadeIn 0.15s ease",
          }}>
            <span style={{ fontSize: 16 }}>🔍</span>
            <input
              ref={searchRef}
              value={search}
              onChange={e => setSearch(e.target.value)}
              placeholder="Search dishes…"
              style={{
                flex: 1, height: 40, background: "none", border: "none", outline: "none",
                fontFamily: "'DM Sans', sans-serif", fontSize: 14, color: T.text,
              }}
            />
            <button onClick={closeSearch} style={{ background: "none", border: "none", fontSize: 18, color: T.textMuted, cursor: "pointer" }}>×</button>
          </div>
        )}

        {/* Category scroll */}
        <div style={{
          display: "flex", gap: 8, overflowX: "auto", paddingBottom: 14,
          scrollbarWidth: "none", msOverflowStyle: "none",
        }}>
          {CATEGORIES.map(cat => (
            <button
              key={cat.id}
              onClick={() => { setActiveCat(cat.id); scrollRef.current?.scrollTo({ top: 0, behavior: "smooth" }); }}
              style={{
                flexShrink: 0, padding: "7px 16px",
                borderRadius: T.radiusPill, border: "none", cursor: "pointer",
                fontFamily: "'DM Sans', sans-serif", fontSize: 13, fontWeight: 500,
                background: activeCat === cat.id ? T.accent : T.bg,
                color: activeCat === cat.id ? "white" : T.textMuted,
                transition: "all 0.15s",
              }}
            >
              {cat.label}
            </button>
          ))}
        </div>
      </div>

      {/* ── Scrollable menu ── */}
      <div ref={scrollRef} style={{ flex: 1, overflowY: "auto", padding: "20px 16px 0", scrollbarWidth: "none" }}>
        {groupedByCategory.map(({ cat, items }) => (
          <div key={cat.id} style={{ marginBottom: 32 }}>
            {activeCat === "all" && (
              <div style={{ display: "flex", alignItems: "baseline", gap: 10, marginBottom: 14 }}>
                <h2 style={{
                  fontFamily: "'Fraunces', serif", fontSize: 22, fontWeight: 400,
                  color: T.text, margin: 0,
                }}>{cat.label}</h2>
                <span style={{ fontFamily: "'DM Sans', sans-serif", fontSize: 13, color: T.textFaint }}>{items.length} items</span>
              </div>
            )}
            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12 }}>
              {items.map(item => (
                <ProductCard
                  key={item.id}
                  item={item}
                  onTap={() => onSelectProduct(item)}
                  onAdd={e => { e.stopPropagation(); onQuickAdd(item); }}
                />
              ))}
            </div>
          </div>
        ))}

        {filtered.length === 0 && (
          <div style={{ textAlign: "center", padding: "60px 0" }}>
            <p style={{ fontFamily: "'Fraunces', serif", fontStyle: "italic", fontSize: 20, color: T.textFaint }}>No dishes found</p>
          </div>
        )}

        {/* Cart spacer */}
        <div style={{ height: cartCount > 0 ? 96 : 20 }} />
      </div>

      {/* ── Sticky cart bar ── */}
      {cartCount > 0 && (
        <div style={{
          position: "absolute", bottom: 0, left: 0, right: 0,
          padding: "12px 16px 28px",
          background: "linear-gradient(180deg, transparent 0%, rgba(250,250,247,0.95) 24px, rgba(250,250,247,1) 100%)",
        }}>
          <button
            onClick={onOpenCart}
            style={{
              width: "100%", height: 56, borderRadius: 16,
              background: T.text, border: "none", cursor: "pointer",
              display: "flex", alignItems: "center", justifyContent: "space-between",
              padding: "0 20px",
              boxShadow: "0 4px 24px rgba(26,18,9,0.22)",
              animation: "scaleIn 0.2s ease",
            }}
          >
            <div style={{ display: "flex", alignItems: "center", gap: 12 }}>
              <div style={{
                width: 26, height: 26, borderRadius: 8,
                background: T.accent, display: "flex", alignItems: "center", justifyContent: "center",
              }}>
                <span style={{ color: "white", fontFamily: "'DM Sans', sans-serif", fontSize: 12, fontWeight: 700 }}>{cartCount}</span>
              </div>
              <span style={{ fontFamily: "'DM Sans', sans-serif", fontSize: 14, fontWeight: 500, color: "white" }}>View order</span>
            </div>
            <span style={{ fontFamily: "'Fraunces', serif", fontSize: 18, fontWeight: 500, color: "white" }}>{fmt(cartTotal)}</span>
          </button>
        </div>
      )}
    </div>
  );
}

/* ════════════════════════════════════════════════
   Root App
════════════════════════════════════════════════ */
export default function App() {
  const [screen, setScreen] = useState<Screen>("qr");
  const [selectedItem, setSelectedItem] = useState<MenuItem | null>(null);
  const [cartOpen, setCartOpen] = useState(false);
  const [cart, setCart] = useState<CartItem[]>([]);

  const handleAddToCart = useCallback((product: MenuItem, selections: Record<string, string[]>, qty: number) => {
    const key = `${product.id}-${JSON.stringify(selections)}`;
    setCart(prev => {
      const existing = prev.find(i => i.key === key);
      if (existing) {
        return prev.map(i => i.key === key
          ? { ...i, qty: i.qty + qty, lineTotal: calcItemTotal(product, selections, i.qty + qty) }
          : i
        );
      }
      return [...prev, { key, product, qty, selections, lineTotal: calcItemTotal(product, selections, qty) }];
    });
  }, []);

  const handleQuickAdd = useCallback((product: MenuItem) => {
    const defaultSelections: Record<string, string[]> = {};
    for (const g of product.modifiers) {
      if (g.options.length > 0) defaultSelections[g.id] = [g.options[0].id];
    }
    handleAddToCart(product, defaultSelections, 1);
  }, [handleAddToCart]);

  const handleUpdateQty = (key: string, qty: number) => {
    setCart(prev => prev.map(i => i.key === key ? { ...i, qty, lineTotal: calcItemTotal(i.product, i.selections, qty) } : i));
  };
  const handleRemove = (key: string) => setCart(prev => prev.filter(i => i.key !== key));

  return (
    <div style={{
      minHeight: "100vh", display: "flex", alignItems: "center", justifyContent: "center",
      background: "linear-gradient(135deg, #1A1209 0%, #2D1F10 50%, #1A1209 100%)",
      padding: "24px 0",
      fontFamily: "'DM Sans', sans-serif",
    }}>
      {/* Phone frame */}
      <div style={{
        width: 390, height: "min(844px, calc(100vh - 48px))",
        borderRadius: 50,
        background: T.bg,
        overflow: "hidden",
        position: "relative",
        display: "flex",
        flexDirection: "column",
        boxShadow: "0 40px 120px rgba(0,0,0,0.6), 0 0 0 1px rgba(255,255,255,0.08)",
      }}>
        {/* Status bar */}
        <div style={{
          height: 44, background: screen === "qr" ? T.text : T.surface,
          display: "flex", alignItems: "center", justifyContent: "space-between",
          padding: "0 28px", flexShrink: 0,
          transition: "background 0.4s",
        }}>
          <span style={{
            fontSize: 15, fontWeight: 600,
            color: screen === "qr" ? "white" : T.text,
          }}>9:41</span>
          <div style={{ display: "flex", alignItems: "center", gap: 6 }}>
            <span style={{ fontSize: 13, color: screen === "qr" ? "rgba(255,255,255,0.5)" : T.textMuted }}>●●●●</span>
            <span style={{ fontSize: 13, color: screen === "qr" ? "rgba(255,255,255,0.5)" : T.textMuted }}>WiFi</span>
            <span style={{ fontSize: 13, color: screen === "qr" ? "rgba(255,255,255,0.5)" : T.textMuted }}>🔋</span>
          </div>
        </div>

        {/* Content */}
        <div style={{ flex: 1, display: "flex", flexDirection: "column", overflow: "hidden", position: "relative" }}>
          {screen === "qr" && <QRScreen onReady={() => setScreen("menu")} />}

          {screen === "menu" && (
            <MenuScreen
              onSelectProduct={item => setSelectedItem(item)}
              onQuickAdd={handleQuickAdd}
              cart={cart}
              onOpenCart={() => setCartOpen(true)}
            />
          )}

          {/* Product sheet overlay */}
          {selectedItem && (
            <ProductSheet
              item={selectedItem}
              onClose={() => setSelectedItem(null)}
              onAddToCart={(item, sel, qty) => { handleAddToCart(item, sel, qty); setSelectedItem(null); }}
            />
          )}

          {/* Cart sheet overlay */}
          {cartOpen && (
            <CartSheet
              cart={cart}
              onClose={() => setCartOpen(false)}
              onUpdateQty={handleUpdateQty}
              onRemove={handleRemove}
            />
          )}
        </div>

        {/* Home indicator */}
        <div style={{
          height: 34, background: screen === "qr" ? T.text : T.bg,
          display: "flex", alignItems: "center", justifyContent: "center",
          flexShrink: 0, transition: "background 0.4s",
        }}>
          <div style={{ width: 134, height: 5, borderRadius: 99, background: screen === "qr" ? "rgba(255,255,255,0.2)" : "rgba(26,18,9,0.15)" }} />
        </div>
      </div>
    </div>
  );
}
