import { useState } from "react";

const THEMES = [
  { id: "luxury", label: "Luxury Hospitality" },
  { id: "saas", label: "Modern SaaS" },
  { id: "food", label: "Premium Food" },
  { id: "minimal", label: "Minimal Apple-like" },
  { id: "ops", label: "Restaurant Ops" },
];

/* ─── shared data ─── */
const MENU_ITEMS = [
  {
    name: "Wagyu Beef Tenderloin",
    desc: "Pan-seared A5 wagyu, truffle jus, seasonal microgreens",
    price: "₺680",
    tag: "Chef's Selection",
    img: "https://images.unsplash.com/photo-1663530761401-15eefb544889?w=600&h=400&fit=crop&auto=format",
  },
  {
    name: "Grilled Sea Bass",
    desc: "Mediterranean sea bass, saffron velouté, fennel salad",
    price: "₺420",
    tag: "Popular",
    img: "https://images.unsplash.com/photo-1676471926534-d5c9771909fa?w=600&h=400&fit=crop&auto=format",
  },
  {
    name: "Truffle Risotto",
    desc: "Arborio rice, black truffle, 36-month Parmigiano",
    price: "₺340",
    tag: null,
    img: "https://images.unsplash.com/photo-1643879397174-4f10ac503566?w=600&h=400&fit=crop&auto=format",
  },
];

const ORDER_STATUS = { step: 2, eta: "12 min", table: "Table 7", items: 3 };
const STATUS_STEPS = ["Received", "Preparing", "Ready", "Served"];

/* ══════════════════════════════════════════════════════════
   1. LUXURY HOSPITALITY
   Deep midnight navy · Gold leaf · EB Garamond display · Cormorant body
   Thin ruled lines · No borders just space · Jewel-box cards
══════════════════════════════════════════════════════════ */
function LuxuryTheme() {
  const [active, setActive] = useState(0);
  return (
    <div
      style={{ fontFamily: "'EB Garamond', serif", background: "#0C0F1A", color: "#F0EBE1", minHeight: "100vh" }}
    >
      {/* Nav */}
      <nav style={{ borderBottom: "1px solid rgba(200,170,90,0.2)", padding: "0 48px" }}>
        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", height: 72 }}>
          <div style={{ display: "flex", alignItems: "center", gap: 32 }}>
            <span style={{ fontFamily: "'EB Garamond', serif", fontSize: 22, letterSpacing: "0.12em", color: "#C8AA5A", fontWeight: 500 }}>
              NOBU ISTANBUL
            </span>
            <div style={{ width: 1, height: 20, background: "rgba(200,170,90,0.3)" }} />
            {["Menu", "Wine Cellar", "Experience", "Reserve"].map((n, i) => (
              <button
                key={n}
                onClick={() => setActive(i)}
                style={{
                  background: "none",
                  border: "none",
                  cursor: "pointer",
                  fontFamily: "'EB Garamond', serif",
                  fontSize: 15,
                  letterSpacing: "0.08em",
                  color: active === i ? "#C8AA5A" : "rgba(240,235,225,0.55)",
                  paddingBottom: 2,
                  borderBottom: active === i ? "1px solid #C8AA5A" : "1px solid transparent",
                  transition: "color 0.2s",
                }}
              >
                {n}
              </button>
            ))}
          </div>
          <div style={{ display: "flex", alignItems: "center", gap: 24 }}>
            <span style={{ fontFamily: "'EB Garamond', serif", fontSize: 13, letterSpacing: "0.15em", color: "rgba(200,170,90,0.7)", textTransform: "uppercase" }}>
              Table 7 · Guest Session
            </span>
            <button
              style={{
                background: "none",
                border: "1px solid rgba(200,170,90,0.5)",
                color: "#C8AA5A",
                padding: "8px 24px",
                fontFamily: "'EB Garamond', serif",
                fontSize: 13,
                letterSpacing: "0.12em",
                cursor: "pointer",
                transition: "all 0.2s",
              }}
            >
              VIEW BILL
            </button>
          </div>
        </div>
      </nav>

      <div style={{ padding: "0 48px" }}>
        {/* Category strip */}
        <div style={{ display: "flex", gap: 40, paddingTop: 40, paddingBottom: 32, borderBottom: "1px solid rgba(200,170,90,0.1)" }}>
          {["Amuse-Bouche", "Starters", "Mains", "Desserts", "Wine Pairing"].map((c, i) => (
            <button
              key={c}
              style={{
                background: "none",
                border: "none",
                cursor: "pointer",
                fontFamily: "'EB Garamond', serif",
                fontSize: 14,
                letterSpacing: "0.1em",
                color: i === 2 ? "#C8AA5A" : "rgba(240,235,225,0.4)",
                textTransform: "uppercase",
              }}
            >
              {c}
            </button>
          ))}
        </div>

        {/* Section heading */}
        <div style={{ paddingTop: 56, paddingBottom: 48 }}>
          <p style={{ fontSize: 11, letterSpacing: "0.25em", color: "#C8AA5A", textTransform: "uppercase", marginBottom: 12 }}>Main Course</p>
          <h2 style={{ fontFamily: "'EB Garamond', serif", fontSize: 42, fontWeight: 400, fontStyle: "italic", color: "#F0EBE1", lineHeight: 1.1, margin: 0 }}>
            Plats Principaux
          </h2>
        </div>

        {/* Cards */}
        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: 2 }}>
          {MENU_ITEMS.map((item) => (
            <div key={item.name} style={{ position: "relative", overflow: "hidden", background: "#111520", cursor: "pointer" }}
              onMouseEnter={e => { (e.currentTarget as HTMLElement).style.outline = "1px solid rgba(200,170,90,0.3)"; }}
              onMouseLeave={e => { (e.currentTarget as HTMLElement).style.outline = "none"; }}
            >
              <div style={{ height: 220, overflow: "hidden" }}>
                <img src={item.img} alt={item.name} style={{ width: "100%", height: "100%", objectFit: "cover", transition: "transform 0.6s", filter: "brightness(0.85)" }} />
              </div>
              {item.tag && (
                <div style={{ position: "absolute", top: 20, left: 20, background: "rgba(12,15,26,0.8)", border: "1px solid rgba(200,170,90,0.4)", padding: "4px 12px" }}>
                  <span style={{ fontSize: 10, letterSpacing: "0.2em", color: "#C8AA5A", textTransform: "uppercase" }}>{item.tag}</span>
                </div>
              )}
              <div style={{ padding: "28px 28px 32px" }}>
                <h3 style={{ fontFamily: "'EB Garamond', serif", fontSize: 22, fontWeight: 400, color: "#F0EBE1", margin: "0 0 8px" }}>{item.name}</h3>
                <p style={{ fontFamily: "'EB Garamond', serif", fontStyle: "italic", fontSize: 14, color: "rgba(240,235,225,0.45)", lineHeight: 1.6, margin: "0 0 24px" }}>{item.desc}</p>
                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                  <span style={{ fontFamily: "'EB Garamond', serif", fontSize: 24, fontWeight: 500, color: "#C8AA5A" }}>{item.price}</span>
                  <button style={{
                    background: "none", border: "1px solid rgba(200,170,90,0.4)", color: "#C8AA5A",
                    padding: "8px 20px", fontFamily: "'EB Garamond', serif", fontSize: 12,
                    letterSpacing: "0.12em", textTransform: "uppercase", cursor: "pointer",
                    transition: "all 0.2s",
                  }}>
                    Select
                  </button>
                </div>
              </div>
            </div>
          ))}
        </div>

        {/* Order status */}
        <div style={{ marginTop: 64, padding: "40px 48px", background: "#111520", border: "1px solid rgba(200,170,90,0.15)", display: "grid", gridTemplateColumns: "1fr auto", alignItems: "center", gap: 40 }}>
          <div>
            <p style={{ fontSize: 10, letterSpacing: "0.25em", textTransform: "uppercase", color: "#C8AA5A", margin: "0 0 16px" }}>Order Status</p>
            <div style={{ display: "flex", alignItems: "center", gap: 0 }}>
              {STATUS_STEPS.map((s, i) => (
                <div key={s} style={{ display: "flex", alignItems: "center" }}>
                  <div style={{ display: "flex", flexDirection: "column", alignItems: "center", gap: 8 }}>
                    <div style={{
                      width: 10, height: 10,
                      border: i < ORDER_STATUS.step ? "none" : "1px solid rgba(200,170,90,0.4)",
                      background: i < ORDER_STATUS.step ? "#C8AA5A" : i === ORDER_STATUS.step ? "rgba(200,170,90,0.2)" : "transparent",
                      borderRadius: "50%",
                    }} />
                    <span style={{ fontSize: 11, letterSpacing: "0.1em", color: i <= ORDER_STATUS.step ? "#C8AA5A" : "rgba(240,235,225,0.3)", whiteSpace: "nowrap" }}>{s}</span>
                  </div>
                  {i < STATUS_STEPS.length - 1 && (
                    <div style={{ width: 80, height: 1, background: i < ORDER_STATUS.step ? "rgba(200,170,90,0.5)" : "rgba(200,170,90,0.15)", margin: "0 0 20px" }} />
                  )}
                </div>
              ))}
            </div>
          </div>
          <div style={{ textAlign: "right" }}>
            <p style={{ fontFamily: "'EB Garamond', serif", fontStyle: "italic", fontSize: 14, color: "rgba(240,235,225,0.4)", margin: "0 0 4px" }}>Estimated arrival</p>
            <p style={{ fontFamily: "'EB Garamond', serif", fontSize: 40, fontWeight: 400, color: "#C8AA5A", margin: 0, lineHeight: 1 }}>{ORDER_STATUS.eta}</p>
          </div>
        </div>

        {/* Bottom cart bar */}
        <div style={{ margin: "32px 0 0", padding: "20px 32px", background: "#C8AA5A", display: "flex", justifyContent: "space-between", alignItems: "center" }}>
          <span style={{ fontFamily: "'EB Garamond', serif", fontSize: 15, letterSpacing: "0.08em", color: "#0C0F1A" }}>
            {ORDER_STATUS.items} items in order
          </span>
          <span style={{ fontFamily: "'EB Garamond', serif", fontSize: 22, fontWeight: 500, color: "#0C0F1A" }}>₺1,440</span>
        </div>
      </div>
    </div>
  );
}

/* ══════════════════════════════════════════════════════════
   2. MODERN SAAS
   Slate-900 sidebar · White canvas · Inter · Blue-600 accent
   Rounded-xl cards · Shadow-sm · Dense data tables · Badges
══════════════════════════════════════════════════════════ */
function SaasTheme() {
  const [tab, setTab] = useState(0);
  return (
    <div style={{ fontFamily: "'Inter', sans-serif", display: "flex", minHeight: "100vh", background: "#F8FAFC" }}>
      {/* Sidebar */}
      <aside style={{ width: 220, background: "#0F172A", display: "flex", flexDirection: "column", padding: "0 0 24px", flexShrink: 0 }}>
        <div style={{ padding: "20px 20px 8px" }}>
          <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 28 }}>
            <div style={{ width: 32, height: 32, borderRadius: 8, background: "#3B82F6", display: "flex", alignItems: "center", justifyContent: "center" }}>
              <span style={{ color: "white", fontSize: 14, fontWeight: 700 }}>R</span>
            </div>
            <div>
              <div style={{ fontSize: 13, fontWeight: 600, color: "#F1F5F9" }}>Restaurant OS</div>
              <div style={{ fontSize: 11, color: "#64748B" }}>Table 7 · 4 guests</div>
            </div>
          </div>
          {[
            { icon: "▦", label: "Menu", active: true },
            { icon: "○", label: "My Order" },
            { icon: "◷", label: "Track" },
            { icon: "◈", label: "Bill" },
          ].map((item) => (
            <div key={item.label} style={{
              display: "flex", alignItems: "center", gap: 10, padding: "9px 12px", borderRadius: 7, marginBottom: 2,
              background: item.active ? "rgba(59,130,246,0.15)" : "transparent", cursor: "pointer",
            }}>
              <span style={{ fontSize: 14, color: item.active ? "#3B82F6" : "#475569" }}>{item.icon}</span>
              <span style={{ fontSize: 13, fontWeight: item.active ? 600 : 400, color: item.active ? "#E2E8F0" : "#64748B" }}>{item.label}</span>
              {item.active && <div style={{ marginLeft: "auto", width: 6, height: 6, borderRadius: "50%", background: "#3B82F6" }} />}
            </div>
          ))}
        </div>
        <div style={{ marginTop: "auto", padding: "0 20px" }}>
          <div style={{ padding: "12px", background: "rgba(59,130,246,0.1)", borderRadius: 8, border: "1px solid rgba(59,130,246,0.2)" }}>
            <div style={{ fontSize: 11, color: "#3B82F6", fontWeight: 600, marginBottom: 4 }}>PREPARING</div>
            <div style={{ fontSize: 12, color: "#94A3B8" }}>ETA {ORDER_STATUS.eta}</div>
            <div style={{ marginTop: 8, height: 4, background: "#1E293B", borderRadius: 99 }}>
              <div style={{ width: "55%", height: "100%", background: "#3B82F6", borderRadius: 99 }} />
            </div>
          </div>
        </div>
      </aside>

      {/* Main */}
      <main style={{ flex: 1, padding: "28px 32px", overflow: "auto" }}>
        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 24 }}>
          <div>
            <h1 style={{ fontSize: 20, fontWeight: 700, color: "#0F172A", margin: 0 }}>Dinner Menu</h1>
            <p style={{ fontSize: 13, color: "#64748B", margin: "4px 0 0" }}>Browse and order from your table</p>
          </div>
          <div style={{ display: "flex", gap: 8 }}>
            <input placeholder="Search dishes..." style={{ padding: "8px 14px", border: "1px solid #E2E8F0", borderRadius: 8, fontSize: 13, color: "#0F172A", background: "white", outline: "none", width: 200 }} />
            <button style={{ padding: "8px 18px", background: "#3B82F6", color: "white", border: "none", borderRadius: 8, fontSize: 13, fontWeight: 600, cursor: "pointer" }}>
              + Add Item
            </button>
          </div>
        </div>

        {/* Tabs */}
        <div style={{ display: "flex", gap: 4, marginBottom: 24, background: "#F1F5F9", borderRadius: 10, padding: 4, width: "fit-content" }}>
          {["All", "Starters", "Mains", "Desserts", "Drinks"].map((t, i) => (
            <button key={t} onClick={() => setTab(i)} style={{
              padding: "7px 16px", borderRadius: 7, border: "none", cursor: "pointer", fontSize: 13, fontWeight: 500,
              background: tab === i ? "white" : "transparent",
              color: tab === i ? "#0F172A" : "#64748B",
              boxShadow: tab === i ? "0 1px 3px rgba(0,0,0,0.1)" : "none",
            }}>
              {t}
            </button>
          ))}
        </div>

        {/* Cards grid */}
        <div style={{ display: "grid", gridTemplateColumns: "repeat(3, 1fr)", gap: 16 }}>
          {MENU_ITEMS.map((item) => (
            <div key={item.name} style={{ background: "white", borderRadius: 12, overflow: "hidden", border: "1px solid #E2E8F0", boxShadow: "0 1px 3px rgba(0,0,0,0.04)", cursor: "pointer", transition: "box-shadow 0.15s" }}
              onMouseEnter={e => { (e.currentTarget as HTMLElement).style.boxShadow = "0 4px 12px rgba(0,0,0,0.08)"; }}
              onMouseLeave={e => { (e.currentTarget as HTMLElement).style.boxShadow = "0 1px 3px rgba(0,0,0,0.04)"; }}
            >
              <div style={{ position: "relative", height: 160, background: "#F1F5F9" }}>
                <img src={item.img} alt={item.name} style={{ width: "100%", height: "100%", objectFit: "cover" }} />
                {item.tag && (
                  <div style={{ position: "absolute", top: 10, left: 10, background: item.tag === "Popular" ? "#DBEAFE" : "#F0FDF4", color: item.tag === "Popular" ? "#1D4ED8" : "#166534", padding: "3px 10px", borderRadius: 99, fontSize: 11, fontWeight: 600 }}>
                    {item.tag}
                  </div>
                )}
              </div>
              <div style={{ padding: "16px" }}>
                <div style={{ fontSize: 14, fontWeight: 600, color: "#0F172A", marginBottom: 4 }}>{item.name}</div>
                <div style={{ fontSize: 12, color: "#64748B", lineHeight: 1.5, marginBottom: 14 }}>{item.desc}</div>
                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                  <span style={{ fontSize: 16, fontWeight: 700, color: "#0F172A" }}>{item.price}</span>
                  <button style={{ padding: "6px 14px", background: "#3B82F6", color: "white", border: "none", borderRadius: 6, fontSize: 12, fontWeight: 600, cursor: "pointer" }}>
                    Add
                  </button>
                </div>
              </div>
            </div>
          ))}
        </div>

        {/* Order status bar */}
        <div style={{ marginTop: 24, background: "white", borderRadius: 12, border: "1px solid #E2E8F0", padding: "20px 24px", display: "flex", alignItems: "center", justifyContent: "space-between" }}>
          <div style={{ display: "flex", alignItems: "center", gap: 32 }}>
            <div>
              <div style={{ fontSize: 11, fontWeight: 600, color: "#64748B", textTransform: "uppercase", letterSpacing: "0.05em", marginBottom: 2 }}>Status</div>
              <div style={{ display: "flex", alignItems: "center", gap: 6 }}>
                <div style={{ width: 8, height: 8, borderRadius: "50%", background: "#F59E0B" }} />
                <span style={{ fontSize: 14, fontWeight: 600, color: "#0F172A" }}>Preparing</span>
              </div>
            </div>
            <div style={{ width: 1, height: 32, background: "#E2E8F0" }} />
            <div>
              <div style={{ fontSize: 11, fontWeight: 600, color: "#64748B", textTransform: "uppercase", letterSpacing: "0.05em", marginBottom: 2 }}>ETA</div>
              <div style={{ fontSize: 14, fontWeight: 700, color: "#0F172A" }}>{ORDER_STATUS.eta}</div>
            </div>
            <div style={{ width: 1, height: 32, background: "#E2E8F0" }} />
            <div>
              <div style={{ fontSize: 11, fontWeight: 600, color: "#64748B", textTransform: "uppercase", letterSpacing: "0.05em", marginBottom: 2 }}>Items</div>
              <div style={{ fontSize: 14, fontWeight: 700, color: "#0F172A" }}>{ORDER_STATUS.items} in queue</div>
            </div>
          </div>
          <button style={{ padding: "10px 20px", background: "#0F172A", color: "white", border: "none", borderRadius: 8, fontSize: 13, fontWeight: 600, cursor: "pointer" }}>
            View Bill →
          </button>
        </div>
      </main>
    </div>
  );
}

/* ══════════════════════════════════════════════════════════
   3. PREMIUM FOOD
   Cream linen · Warm terracotta accent · Fraunces display
   Generous imagery · Overlapping cards · Appetite-driven
══════════════════════════════════════════════════════════ */
function FoodTheme() {
  return (
    <div style={{ fontFamily: "'DM Sans', sans-serif", background: "#FAF6F0", color: "#1C1208", minHeight: "100vh" }}>
      {/* Nav */}
      <nav style={{ background: "#FAF6F0", padding: "0 40px", position: "sticky", top: 0, zIndex: 10, borderBottom: "1px solid rgba(28,18,8,0.08)" }}>
        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", height: 64 }}>
          <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
            <span style={{ fontFamily: "'Fraunces', serif", fontSize: 20, fontWeight: 500, color: "#C4622D" }}>Marea</span>
            <span style={{ fontSize: 12, color: "rgba(28,18,8,0.35)" }}>· Table 7</span>
          </div>
          <div style={{ display: "flex", gap: 32 }}>
            {["Menu", "Drinks", "Desserts"].map((n) => (
              <button key={n} style={{ background: "none", border: "none", fontSize: 15, fontWeight: 500, color: "rgba(28,18,8,0.55)", cursor: "pointer" }}>{n}</button>
            ))}
          </div>
          <button style={{ background: "#C4622D", color: "white", border: "none", padding: "10px 22px", borderRadius: 99, fontSize: 14, fontWeight: 600, cursor: "pointer" }}>
            Cart · ₺1,440
          </button>
        </div>
      </nav>

      {/* Hero band */}
      <div style={{ padding: "48px 40px 32px" }}>
        <p style={{ fontFamily: "'DM Sans', sans-serif", fontSize: 12, fontWeight: 600, letterSpacing: "0.15em", textTransform: "uppercase", color: "#C4622D", marginBottom: 10 }}>
          Tonight's Menu
        </p>
        <h1 style={{ fontFamily: "'Fraunces', serif", fontSize: 52, fontWeight: 400, lineHeight: 1.05, margin: "0 0 6px", color: "#1C1208" }}>
          Crafted with<br /><em>intention.</em>
        </h1>
      </div>

      {/* Category pills */}
      <div style={{ padding: "0 40px 36px", display: "flex", gap: 8, flexWrap: "wrap" }}>
        {["All", "Starters", "Mains", "Pasta & Risotto", "Grill", "Desserts"].map((c, i) => (
          <button key={c} style={{
            background: i === 2 ? "#C4622D" : "white",
            color: i === 2 ? "white" : "#1C1208",
            border: "1.5px solid",
            borderColor: i === 2 ? "#C4622D" : "rgba(28,18,8,0.12)",
            padding: "8px 18px", borderRadius: 99, fontSize: 13, fontWeight: 500, cursor: "pointer",
          }}>
            {c}
          </button>
        ))}
      </div>

      {/* Cards — magazine layout */}
      <div style={{ padding: "0 40px", display: "grid", gridTemplateColumns: "1.4fr 1fr 1fr", gap: 16 }}>
        {MENU_ITEMS.map((item, i) => (
          <div key={item.name} style={{
            background: "white",
            borderRadius: 20,
            overflow: "hidden",
            boxShadow: "0 2px 16px rgba(28,18,8,0.06)",
            cursor: "pointer",
            position: "relative",
          }}>
            <div style={{ height: i === 0 ? 300 : 200, overflow: "hidden", background: "#EDE0D4" }}>
              <img src={item.img} alt={item.name} style={{ width: "100%", height: "100%", objectFit: "cover" }} />
            </div>
            {item.tag && (
              <div style={{ position: "absolute", top: 16, left: 16, background: "#C4622D", color: "white", padding: "4px 12px", borderRadius: 99, fontSize: 11, fontWeight: 700, letterSpacing: "0.05em" }}>
                {item.tag}
              </div>
            )}
            <div style={{ padding: "20px 22px 22px" }}>
              <h3 style={{ fontFamily: "'Fraunces', serif", fontSize: i === 0 ? 22 : 18, fontWeight: 400, color: "#1C1208", margin: "0 0 6px", lineHeight: 1.2 }}>{item.name}</h3>
              <p style={{ fontSize: 13, color: "rgba(28,18,8,0.5)", lineHeight: 1.5, margin: "0 0 18px" }}>{item.desc}</p>
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                <span style={{ fontFamily: "'Fraunces', serif", fontSize: 20, fontWeight: 500, color: "#C4622D" }}>{item.price}</span>
                <button style={{
                  width: 36, height: 36, borderRadius: "50%", background: "#C4622D", border: "none",
                  color: "white", fontSize: 20, cursor: "pointer", display: "flex", alignItems: "center", justifyContent: "center", lineHeight: 1,
                }}>+</button>
              </div>
            </div>
          </div>
        ))}
      </div>

      {/* Order status */}
      <div style={{ margin: "32px 40px", background: "#FDF4ED", borderRadius: 20, padding: "28px 32px", display: "flex", justifyContent: "space-between", alignItems: "center", border: "1.5px solid rgba(196,98,45,0.15)" }}>
        <div>
          <p style={{ fontSize: 12, fontWeight: 700, letterSpacing: "0.12em", textTransform: "uppercase", color: "#C4622D", margin: "0 0 12px" }}>Your Order</p>
          <div style={{ display: "flex", gap: 0, alignItems: "center" }}>
            {STATUS_STEPS.map((s, i) => (
              <div key={s} style={{ display: "flex", alignItems: "center" }}>
                <div style={{ textAlign: "center" }}>
                  <div style={{
                    width: 28, height: 28, borderRadius: "50%", margin: "0 auto 6px",
                    background: i < ORDER_STATUS.step ? "#C4622D" : i === ORDER_STATUS.step ? "rgba(196,98,45,0.15)" : "rgba(28,18,8,0.06)",
                    border: i === ORDER_STATUS.step ? "2px solid #C4622D" : "none",
                    display: "flex", alignItems: "center", justifyContent: "center",
                  }}>
                    {i < ORDER_STATUS.step && <span style={{ color: "white", fontSize: 12 }}>✓</span>}
                    {i === ORDER_STATUS.step && <span style={{ width: 8, height: 8, borderRadius: "50%", background: "#C4622D", display: "block" }} />}
                  </div>
                  <span style={{ fontSize: 11, fontWeight: 500, color: i <= ORDER_STATUS.step ? "#1C1208" : "rgba(28,18,8,0.35)", whiteSpace: "nowrap" }}>{s}</span>
                </div>
                {i < STATUS_STEPS.length - 1 && <div style={{ width: 60, height: 2, background: i < ORDER_STATUS.step ? "#C4622D" : "rgba(28,18,8,0.1)", margin: "0 4px 18px" }} />}
              </div>
            ))}
          </div>
        </div>
        <div style={{ textAlign: "right" }}>
          <p style={{ fontSize: 13, color: "rgba(28,18,8,0.45)", marginBottom: 4, fontWeight: 500 }}>Ready in</p>
          <p style={{ fontFamily: "'Fraunces', serif", fontSize: 44, fontWeight: 400, color: "#C4622D", margin: 0, lineHeight: 1 }}>{ORDER_STATUS.eta}</p>
        </div>
      </div>
    </div>
  );
}

/* ══════════════════════════════════════════════════════════
   4. MINIMAL APPLE-LIKE
   Pure white · SF-equivalent Plus Jakarta Sans · Zero decoration
   SF-style grouped tables · Hairline dividers · System blue
   Everything earns its space · Massive negative space
══════════════════════════════════════════════════════════ */
function MinimalTheme() {
  const [selected, setSelected] = useState<string | null>(null);
  return (
    <div style={{ fontFamily: "'Plus Jakarta Sans', sans-serif", background: "#FFFFFF", color: "#000000", minHeight: "100vh", maxWidth: 430, margin: "0 auto" }}>
      {/* Status bar simulation */}
      <div style={{ height: 44, background: "white", display: "flex", alignItems: "center", justifyContent: "space-between", padding: "0 24px" }}>
        <span style={{ fontSize: 15, fontWeight: 600 }}>9:41</span>
        <span style={{ fontSize: 12, color: "#6E6E73" }}>Table 7</span>
      </div>

      {/* Large title nav */}
      <div style={{ padding: "8px 20px 0" }}>
        <div style={{ display: "flex", alignItems: "flex-end", justifyContent: "space-between", marginBottom: 16 }}>
          <h1 style={{ fontSize: 34, fontWeight: 700, letterSpacing: "-0.4px", margin: 0, lineHeight: 1.1 }}>Marea</h1>
          <button style={{ background: "none", border: "none", cursor: "pointer", padding: 0 }}>
            <div style={{ width: 32, height: 32, borderRadius: "50%", background: "#F2F2F7", display: "flex", alignItems: "center", justifyContent: "center", fontSize: 16 }}>
              🛒
            </div>
          </button>
        </div>

        {/* Search */}
        <div style={{ background: "#F2F2F7", borderRadius: 10, padding: "8px 12px", display: "flex", alignItems: "center", gap: 8, marginBottom: 20 }}>
          <span style={{ fontSize: 16, color: "#6E6E73" }}>🔍</span>
          <span style={{ fontSize: 15, color: "#6E6E73" }}>Search menu</span>
        </div>

        {/* Category scroll */}
        <div style={{ display: "flex", gap: 8, overflowX: "auto", paddingBottom: 4, scrollbarWidth: "none" }}>
          {["All", "Starters", "Mains", "Desserts", "Drinks"].map((c, i) => (
            <button key={c} style={{
              flexShrink: 0,
              background: i === 2 ? "#007AFF" : "#F2F2F7",
              color: i === 2 ? "white" : "#000000",
              border: "none", padding: "6px 16px", borderRadius: 99, fontSize: 14, fontWeight: 500, cursor: "pointer",
            }}>{c}</button>
          ))}
        </div>
      </div>

      {/* Section */}
      <div style={{ padding: "28px 20px 0" }}>
        <h2 style={{ fontSize: 22, fontWeight: 700, letterSpacing: "-0.3px", margin: "0 0 16px" }}>Main Courses</h2>

        {/* Grouped list — Apple Settings style */}
        <div style={{ background: "#F2F2F7", borderRadius: 16, overflow: "hidden" }}>
          {MENU_ITEMS.map((item, i) => (
            <div key={item.name}>
              <div
                onClick={() => setSelected(selected === item.name ? null : item.name)}
                style={{ display: "flex", gap: 12, padding: "12px 16px", background: selected === item.name ? "#E8F0FE" : "white", cursor: "pointer", alignItems: "center" }}
              >
                <div style={{ width: 64, height: 64, borderRadius: 12, overflow: "hidden", background: "#F2F2F7", flexShrink: 0 }}>
                  <img src={item.img} alt={item.name} style={{ width: "100%", height: "100%", objectFit: "cover" }} />
                </div>
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div style={{ fontSize: 15, fontWeight: 500, color: "#000", marginBottom: 3, letterSpacing: "-0.1px" }}>{item.name}</div>
                  <div style={{ fontSize: 13, color: "#6E6E73", lineHeight: 1.4, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{item.desc}</div>
                </div>
                <div style={{ display: "flex", flexDirection: "column", alignItems: "flex-end", gap: 6, flexShrink: 0 }}>
                  <span style={{ fontSize: 15, fontWeight: 600, letterSpacing: "-0.1px" }}>{item.price}</span>
                  <div style={{ width: 22, height: 22, borderRadius: "50%", background: "#007AFF", display: "flex", alignItems: "center", justifyContent: "center" }}>
                    <span style={{ color: "white", fontSize: 14, fontWeight: 500, lineHeight: 1 }}>+</span>
                  </div>
                </div>
              </div>
              {i < MENU_ITEMS.length - 1 && <div style={{ height: 1, background: "#E5E5EA", marginLeft: 92 }} />}
            </div>
          ))}
        </div>
      </div>

      {/* Order status — minimal card */}
      <div style={{ margin: "24px 20px", background: "#F2F2F7", borderRadius: 16, padding: "16px 20px" }}>
        <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 12 }}>
          <span style={{ fontSize: 13, fontWeight: 600, color: "#6E6E73" }}>ORDER STATUS</span>
          <span style={{ fontSize: 13, fontWeight: 600, color: "#007AFF" }}>Preparing</span>
        </div>
        <div style={{ height: 4, background: "#E5E5EA", borderRadius: 99, marginBottom: 12 }}>
          <div style={{ width: "55%", height: "100%", background: "#007AFF", borderRadius: 99 }} />
        </div>
        <div style={{ display: "flex", justifyContent: "space-between" }}>
          <span style={{ fontSize: 13, color: "#6E6E73" }}>{ORDER_STATUS.items} items</span>
          <span style={{ fontSize: 13, fontWeight: 500, color: "#000" }}>Ready in {ORDER_STATUS.eta}</span>
        </div>
      </div>

      {/* Bottom CTA */}
      <div style={{ padding: "0 20px 40px" }}>
        <button style={{
          width: "100%", background: "#007AFF", color: "white", border: "none",
          borderRadius: 14, padding: "16px", fontSize: 17, fontWeight: 600, cursor: "pointer",
          letterSpacing: "-0.2px",
        }}>
          View Bill · ₺1,440
        </button>
      </div>
    </div>
  );
}

/* ══════════════════════════════════════════════════════════
   5. MODERN RESTAURANT OPERATIONS
   Charcoal-950 canvas · Electric green accent · JetBrains Mono data
   Outfit for UI · Thick status bands · Dense operational info
   Feels like a pro tool built for kitchens and managers
══════════════════════════════════════════════════════════ */
function OpsTheme() {
  const [activeFloor, setActiveFloor] = useState(1);
  const tables = [
    { n: 1, status: "available", covers: 0 },
    { n: 2, status: "seated", covers: 4 },
    { n: 3, status: "ordering", covers: 2 },
    { n: 4, status: "preparing", covers: 6 },
    { n: 5, status: "ready", covers: 3 },
    { n: 6, status: "billed", covers: 4 },
    { n: 7, status: "preparing", covers: 4 },
    { n: 8, status: "available", covers: 0 },
  ];
  const statusColor: Record<string, string> = {
    available: "#1C2A1E",
    seated: "#1E2A3A",
    ordering: "#2A2A1A",
    preparing: "#F59E0B",
    ready: "#10B981",
    billed: "#3B82F6",
  };
  const statusText: Record<string, string> = {
    available: "#4ADE80",
    seated: "#60A5FA",
    ordering: "#FACC15",
    preparing: "#1C1208",
    ready: "#FFFFFF",
    billed: "#FFFFFF",
  };

  return (
    <div style={{ fontFamily: "'Outfit', sans-serif", background: "#0A0A0A", color: "#E4E4E7", minHeight: "100vh", display: "flex", flexDirection: "column" }}>
      {/* Top bar */}
      <header style={{ height: 52, background: "#111111", borderBottom: "1px solid #1F1F1F", display: "flex", alignItems: "center", padding: "0 20px", gap: 24, flexShrink: 0 }}>
        <span style={{ fontFamily: "'Outfit', sans-serif", fontSize: 14, fontWeight: 700, letterSpacing: "0.05em", color: "#4ADE80" }}>RESTAURANT OS</span>
        <div style={{ width: 1, height: 20, background: "#2A2A2A" }} />
        <div style={{ display: "flex", gap: 4 }}>
          {["Floor View", "Orders", "Menu", "Kitchen", "Reports"].map((t, i) => (
            <button key={t} style={{
              background: i === 0 ? "#1A1A1A" : "none", border: i === 0 ? "1px solid #2A2A2A" : "none",
              color: i === 0 ? "#E4E4E7" : "#52525B", padding: "5px 12px", borderRadius: 6, fontSize: 13, fontWeight: 500, cursor: "pointer",
            }}>{t}</button>
          ))}
        </div>
        <div style={{ marginLeft: "auto", display: "flex", gap: 16, alignItems: "center" }}>
          <div style={{ display: "flex", gap: 12 }}>
            {[
              { label: "Active Orders", val: "12", color: "#F59E0B" },
              { label: "Ready", val: "3", color: "#10B981" },
              { label: "Revenue", val: "₺18.4k", color: "#E4E4E7" },
            ].map(m => (
              <div key={m.label} style={{ textAlign: "right" }}>
                <div style={{ fontFamily: "'JetBrains Mono', monospace", fontSize: 16, fontWeight: 600, color: m.color, lineHeight: 1 }}>{m.val}</div>
                <div style={{ fontSize: 10, color: "#52525B", marginTop: 2, letterSpacing: "0.05em" }}>{m.label.toUpperCase()}</div>
              </div>
            ))}
          </div>
          <div style={{ width: 1, height: 28, background: "#2A2A2A" }} />
          <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
            <div style={{ width: 8, height: 8, borderRadius: "50%", background: "#4ADE80" }} />
            <span style={{ fontSize: 12, color: "#71717A" }}>Shift manager: Ayşe K.</span>
          </div>
        </div>
      </header>

      <div style={{ display: "flex", flex: 1, overflow: "hidden" }}>
        {/* Left panel */}
        <aside style={{ width: 56, background: "#111111", borderRight: "1px solid #1F1F1F", display: "flex", flexDirection: "column", alignItems: "center", padding: "16px 0", gap: 8, flexShrink: 0 }}>
          {["⊞", "◱", "⚡", "◈", "↗"].map((icon, i) => (
            <button key={i} style={{
              width: 36, height: 36, borderRadius: 8,
              background: i === 0 ? "#1F1F1F" : "transparent", border: i === 0 ? "1px solid #2A2A2A" : "none",
              color: i === 0 ? "#4ADE80" : "#3F3F46", fontSize: 16, cursor: "pointer", display: "flex", alignItems: "center", justifyContent: "center",
            }}>{icon}</button>
          ))}
        </aside>

        {/* Main floor view */}
        <main style={{ flex: 1, padding: "20px 24px", overflow: "auto" }}>
          <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 20 }}>
            <div style={{ display: "flex", gap: 8 }}>
              {["Ground Floor", "Terrace", "Private"].map((f, i) => (
                <button key={f} onClick={() => setActiveFloor(i)} style={{
                  background: activeFloor === i ? "#1A1A1A" : "none",
                  border: `1px solid ${activeFloor === i ? "#2A2A2A" : "transparent"}`,
                  color: activeFloor === i ? "#E4E4E7" : "#52525B",
                  padding: "6px 14px", borderRadius: 6, fontSize: 13, fontWeight: 500, cursor: "pointer",
                }}>{f}</button>
              ))}
            </div>
            <div style={{ display: "flex", gap: 16 }}>
              {[
                { dot: "#4ADE80", label: "Available" },
                { dot: "#60A5FA", label: "Seated" },
                { dot: "#F59E0B", label: "Preparing" },
                { dot: "#10B981", label: "Ready" },
              ].map(l => (
                <div key={l.label} style={{ display: "flex", alignItems: "center", gap: 6 }}>
                  <div style={{ width: 6, height: 6, borderRadius: "50%", background: l.dot }} />
                  <span style={{ fontSize: 12, color: "#52525B" }}>{l.label}</span>
                </div>
              ))}
            </div>
          </div>

          {/* Table grid */}
          <div style={{ display: "grid", gridTemplateColumns: "repeat(4, 1fr)", gap: 12 }}>
            {tables.map(t => (
              <div key={t.n} style={{
                background: statusColor[t.status],
                border: `1px solid ${t.status === "preparing" || t.status === "ready" ? statusColor[t.status] : "#1F1F1F"}`,
                borderRadius: 10, padding: "16px", cursor: "pointer", position: "relative",
                boxShadow: t.status === "ready" ? "0 0 0 1px #10B981" : "none",
                transition: "all 0.15s",
              }}>
                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", marginBottom: 12 }}>
                  <div>
                    <div style={{ fontFamily: "'JetBrains Mono', monospace", fontSize: 18, fontWeight: 600, color: statusText[t.status], lineHeight: 1 }}>T{t.n}</div>
                    <div style={{ fontSize: 11, color: t.status === "preparing" || t.status === "ready" || t.status === "billed" ? "rgba(28,18,8,0.5)" : "#52525B", marginTop: 4, textTransform: "uppercase", letterSpacing: "0.08em" }}>
                      {t.status}
                    </div>
                  </div>
                  {t.covers > 0 && (
                    <div style={{ display: "flex", alignItems: "center", gap: 4 }}>
                      <span style={{ fontFamily: "'JetBrains Mono', monospace", fontSize: 13, color: statusText[t.status], opacity: 0.7 }}>{t.covers}×</span>
                      <span style={{ fontSize: 13 }}>👤</span>
                    </div>
                  )}
                </div>
                {t.n === 7 && (
                  <div>
                    <div style={{ height: 3, background: "rgba(0,0,0,0.15)", borderRadius: 99, marginBottom: 6 }}>
                      <div style={{ width: "55%", height: "100%", background: "rgba(0,0,0,0.3)", borderRadius: 99 }} />
                    </div>
                    <div style={{ fontFamily: "'JetBrains Mono', monospace", fontSize: 11, color: "rgba(28,18,8,0.55)" }}>ETA 12 min</div>
                  </div>
                )}
              </div>
            ))}
          </div>

          {/* Active orders panel */}
          <div style={{ marginTop: 24 }}>
            <div style={{ fontSize: 11, fontWeight: 700, letterSpacing: "0.12em", textTransform: "uppercase", color: "#52525B", marginBottom: 12 }}>Live Orders</div>
            <div style={{ display: "grid", gridTemplateColumns: "repeat(3, 1fr)", gap: 10 }}>
              {MENU_ITEMS.map((item, i) => (
                <div key={item.name} style={{
                  background: "#111111", border: "1px solid #1F1F1F",
                  borderLeft: `3px solid ${i === 0 ? "#F59E0B" : i === 1 ? "#10B981" : "#3B82F6"}`,
                  borderRadius: "0 8px 8px 0", padding: "14px 16px",
                }}>
                  <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 6 }}>
                    <span style={{ fontFamily: "'JetBrains Mono', monospace", fontSize: 12, fontWeight: 600, color: "#E4E4E7" }}>
                      T{i + 3} · {i === 0 ? "PREPARING" : i === 1 ? "READY" : "IN QUEUE"}
                    </span>
                    <span style={{ fontFamily: "'JetBrains Mono', monospace", fontSize: 11, color: i === 0 ? "#F59E0B" : "#52525B" }}>
                      {i === 0 ? "12m" : i === 1 ? "0m" : "4m"}
                    </span>
                  </div>
                  <div style={{ fontSize: 13, color: "#A1A1AA", lineHeight: 1.4 }}>{item.name}</div>
                  <div style={{ fontFamily: "'JetBrains Mono', monospace", fontSize: 13, fontWeight: 600, color: "#E4E4E7", marginTop: 8 }}>{item.price}</div>
                </div>
              ))}
            </div>
          </div>
        </main>
      </div>
    </div>
  );
}

/* ─── selector ─── */
function ThemeSelector({ active, onChange }: { active: string; onChange: (id: string) => void }) {
  return (
    <div style={{
      position: "fixed", top: 16, left: "50%", transform: "translateX(-50%)",
      zIndex: 9999, display: "flex", gap: 6,
      background: "rgba(10,10,10,0.85)", backdropFilter: "blur(12px)",
      padding: "8px 10px", borderRadius: 14,
      border: "1px solid rgba(255,255,255,0.1)",
    }}>
      {THEMES.map((t) => (
        <button
          key={t.id}
          onClick={() => onChange(t.id)}
          style={{
            padding: "6px 14px",
            borderRadius: 8,
            border: "none",
            cursor: "pointer",
            fontSize: 12,
            fontFamily: "'Inter', sans-serif",
            fontWeight: active === t.id ? 600 : 400,
            background: active === t.id ? "white" : "transparent",
            color: active === t.id ? "#0A0A0A" : "rgba(255,255,255,0.5)",
            transition: "all 0.15s",
            whiteSpace: "nowrap",
          }}
        >
          {t.label}
        </button>
      ))}
    </div>
  );
}

export default function App() {
  const [theme, setTheme] = useState("luxury");

  return (
    <div style={{ position: "relative", minHeight: "100vh" }}>
      <ThemeSelector active={theme} onChange={setTheme} />
      {theme === "luxury" && <LuxuryTheme />}
      {theme === "saas" && <SaasTheme />}
      {theme === "food" && <FoodTheme />}
      {theme === "minimal" && <MinimalTheme />}
      {theme === "ops" && <OpsTheme />}
    </div>
  );
}
