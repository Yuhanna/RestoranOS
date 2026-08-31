import { useState, ReactNode } from "react";

// ─── Types ────────────────────────────────────────────────────────────────────
type NetworkStatus = "online" | "offline" | "syncing";
type TableStatus = "available" | "occupied" | "mine" | "reserved";
type ItemStatus = "pending" | "sent" | "preparing" | "ready" | "served";
type Tab = "tables" | "orders" | "notif";
type ViewMode = "map" | "list";
type TableFilter = "all" | "mine" | "available";

interface Table {
  id: string; label: string; seats: number;
  x: number; y: number;
  status: TableStatus; covers?: number; openMinutes?: number; section: string;
}
interface OrderItem {
  id: string; name: string; modifier?: string;
  qty: number; price: number; status: ItemStatus;
}
interface MenuItem {
  id: string; categoryId: string; name: string;
  description: string; price: number; popular?: boolean;
}
interface Notif {
  id: string; tableId: string; message: string; time: string; read: boolean;
}

// ─── Data ─────────────────────────────────────────────────────────────────────
const TABLES: Table[] = [
  { id: "T01", label: "T01", seats: 4, x: 13, y: 11, status: "mine",     covers: 2, openMinutes: 42, section: "İç Mekan" },
  { id: "T02", label: "T02", seats: 4, x: 38, y: 11, status: "occupied", covers: 4, openMinutes: 78, section: "İç Mekan" },
  { id: "T03", label: "T03", seats: 2, x: 62, y: 11, status: "available",                              section: "İç Mekan" },
  { id: "T04", label: "T04", seats: 2, x: 83, y: 11, status: "available",                              section: "İç Mekan" },
  { id: "T05", label: "T05", seats: 6, x: 13, y: 34, status: "mine",     covers: 5, openMinutes: 18, section: "İç Mekan" },
  { id: "T06", label: "T06", seats: 4, x: 38, y: 34, status: "reserved",                              section: "İç Mekan" },
  { id: "T07", label: "T07", seats: 4, x: 62, y: 34, status: "occupied", covers: 3, openMinutes: 55, section: "İç Mekan" },
  { id: "T08", label: "T08", seats: 2, x: 83, y: 34, status: "mine",     covers: 2, openMinutes: 5,  section: "İç Mekan" },
  { id: "T09", label: "T09", seats: 4, x: 11, y: 61, status: "available",                              section: "Teras" },
  { id: "T10", label: "T10", seats: 4, x: 36, y: 61, status: "mine",     covers: 3, openMinutes: 31, section: "Teras" },
  { id: "T11", label: "T11", seats: 6, x: 61, y: 61, status: "occupied", covers: 6, openMinutes: 90, section: "Teras" },
  { id: "T12", label: "T12", seats: 2, x: 83, y: 61, status: "available",                              section: "Teras" },
  { id: "B01", label: "B01", seats: 2, x: 14, y: 84, status: "available",                              section: "Bar" },
  { id: "B02", label: "B02", seats: 2, x: 40, y: 84, status: "mine",     covers: 1, openMinutes: 12, section: "Bar" },
  { id: "B03", label: "B03", seats: 2, x: 64, y: 84, status: "available",                              section: "Bar" },
  { id: "B04", label: "B04", seats: 2, x: 84, y: 84, status: "occupied", covers: 2, openMinutes: 22, section: "Bar" },
];

const INITIAL_ORDERS: Record<string, OrderItem[]> = {
  T01: [
    { id: "o1", name: "Humus",        qty: 1, price: 65,  status: "served" },
    { id: "o2", name: "Kuzu Izgara",  modifier: "az pişmiş", qty: 1, price: 285, status: "ready" },
    { id: "o3", name: "Tavuk Şiş",    qty: 1, price: 195, status: "preparing" },
    { id: "o4", name: "Ayran",        qty: 2, price: 35,  status: "ready" },
  ],
  T05: [
    { id: "o5", name: "Sigara Böreği",    qty: 2, price: 75,  status: "served" },
    { id: "o6", name: "Mercimek Çorbası", qty: 5, price: 55,  status: "preparing" },
    { id: "o7", name: "Karışık Pizza",    qty: 1, price: 225, status: "sent" },
    { id: "o9", name: "Çay",              qty: 5, price: 25,  status: "sent" },
  ],
  T08: [],
  T10: [
    { id: "o10", name: "Margherita Pizza", qty: 1, price: 175, status: "ready" },
    { id: "o11", name: "Kola",  modifier: "buzlu", qty: 3, price: 45, status: "preparing" },
  ],
  B02: [
    { id: "o12", name: "Baklava",       qty: 1, price: 95, status: "preparing" },
    { id: "o13", name: "Türk Kahvesi",  qty: 1, price: 45, status: "sent" },
  ],
};

const CATS = [
  { id: "starters", name: "Başlangıçlar", icon: "🥗" },
  { id: "mains",    name: "Ana Yemek",    icon: "🍖" },
  { id: "pizza",    name: "Pizza",        icon: "🍕" },
  { id: "drinks",   name: "İçecekler",   icon: "🥤" },
  { id: "desserts", name: "Tatlılar",    icon: "🍮" },
];

const ITEMS: MenuItem[] = [
  { id: "m1", categoryId: "starters", name: "Humus",            description: "Tahin, limon, zeytinyağı",       price: 65,  popular: true },
  { id: "m2", categoryId: "starters", name: "Sigara Böreği",    description: "Beyaz peynirli, 4 adet",         price: 75  },
  { id: "m3", categoryId: "starters", name: "Mercimek Çorbası", description: "Taze nane, limon",               price: 55,  popular: true },
  { id: "m4", categoryId: "mains",    name: "Kuzu Izgara",      description: "Sebzeli pilav, sos",              price: 285, popular: true },
  { id: "m5", categoryId: "mains",    name: "Tavuk Şiş",        description: "Közlenmiş sebze, pilav",          price: 195 },
  { id: "m6", categoryId: "mains",    name: "Rosto",             description: "Patates püresi, mantar sos",      price: 265 },
  { id: "m7", categoryId: "pizza",    name: "Margherita",        description: "Domates, mozzarella, fesleğen",   price: 175 },
  { id: "m8", categoryId: "pizza",    name: "Karışık Pizza",     description: "Salam, mantar, biber, zeytin",    price: 225, popular: true },
  { id: "m9", categoryId: "drinks",   name: "Ayran",             description: "Taze çırpılmış",                  price: 35  },
  { id: "m10", categoryId: "drinks",  name: "Limonata",          description: "Taze sıkılmış, nane",             price: 55  },
  { id: "m11", categoryId: "drinks",  name: "Çay",               description: "Türk çayı",                       price: 25  },
  { id: "m12", categoryId: "drinks",  name: "Su",                description: "500ml",                           price: 20  },
  { id: "m13", categoryId: "drinks",  name: "Kola",              description: "330ml",                           price: 45  },
  { id: "m14", categoryId: "desserts", name: "Baklava",          description: "Fıstıklı, 3 dilim",               price: 95,  popular: true },
  { id: "m15", categoryId: "desserts", name: "Dondurma",         description: "2 top, çeşit seçimi",             price: 75  },
  { id: "m16", categoryId: "desserts", name: "Türk Kahvesi",     description: "İstek üzere şekerli",             price: 45  },
];

const INITIAL_NOTIFS: Notif[] = [
  { id: "n1", tableId: "T01", message: "Kuzu Izgara — Servis için hazır",   time: "2 dk önce", read: false },
  { id: "n2", tableId: "T01", message: "Ayran × 2 — Servis için hazır",     time: "3 dk önce", read: false },
  { id: "n3", tableId: "T10", message: "Margherita Pizza — Hazır",          time: "5 dk önce", read: true  },
  { id: "n4", tableId: "B02", message: "Türk Kahvesi — Hazır",              time: "8 dk önce", read: true  },
];

// ─── Style maps ───────────────────────────────────────────────────────────────
const TS: Record<TableStatus, { bg: string; border: string; text: string; dot: string; label: string }> = {
  available: { bg: "#F0FDF4", border: "#86EFAC", text: "#15803D", dot: "#22C55E",                     label: "Boş"     },
  mine:      { bg: "#FFF7ED", border: "#F97316", text: "#9A3412", dot: "#F97316",                     label: "Benim"   },
  occupied:  { bg: "#F8FAFC", border: "#CBD5E1", text: "#64748B", dot: "#94A3B8",                     label: "Dolu"    },
  reserved:  { bg: "#FEF2F2", border: "#FCA5A5", text: "#B91C1C", dot: "#EF4444",                     label: "Rezerve" },
};

const IS: Record<ItemStatus, { label: string; bg: string; color: string }> = {
  pending:   { label: "Kuyrukta",      bg: "#F1EFE9", color: "#7A7670" },
  sent:      { label: "Gönderildi",    bg: "#DBEAFE", color: "#1D4ED8" },
  preparing: { label: "Hazırlanıyor",  bg: "#FEF3C7", color: "#92400E" },
  ready:     { label: "Hazır ✓",       bg: "#DCFCE7", color: "#166534" },
  served:    { label: "Servis edildi", bg: "#F1EFE9", color: "#B5B0A8" },
};

// ─── Helpers ──────────────────────────────────────────────────────────────────
const fmt   = (n: number) => `₺${n.toLocaleString("tr-TR")}`;
const fmtT  = (m: number) => m < 60 ? `${m}d` : `${Math.floor(m / 60)}s ${m % 60}d`;
const sum   = (items: OrderItem[]) => items.reduce((s, i) => s + i.price * i.qty, 0);

// ─── NetworkBanner ────────────────────────────────────────────────────────────
function NetworkBanner({ status, count }: { status: NetworkStatus; count: number }) {
  if (status === "online") return null;
  const offline = status === "offline";
  return (
    <div style={{
      position: "fixed", top: 0, left: 0, right: 0, zIndex: 500,
      background: offline ? "#7F1D1D" : "#78350F",
      color: "white", padding: "10px 16px",
      display: "flex", alignItems: "center", gap: 8,
      fontSize: 13, fontWeight: 600, letterSpacing: "-0.01em",
    }}>
      <span style={{ fontSize: 16 }}>{offline ? "⚡" : "↻"}</span>
      <span>
        {offline
          ? `Çevrimdışı — ${count} sipariş yerel kuyrukta`
          : `Senkronize ediliyor... (${count} bekliyor)`}
      </span>
      {offline && (
        <span style={{ marginLeft: "auto", fontSize: 10, opacity: 0.55, fontFamily: "var(--font-mono)" }}>
          Tekrar deniyor
        </span>
      )}
    </div>
  );
}

// ─── StatusPill ───────────────────────────────────────────────────────────────
function StatusPill({ status }: { status: ItemStatus }) {
  const s = IS[status];
  return (
    <span style={{
      display: "inline-block", padding: "2px 7px", borderRadius: 4,
      fontSize: 10, fontWeight: 600, letterSpacing: "0.03em",
      background: s.bg, color: s.color,
      fontFamily: "var(--font-mono)", whiteSpace: "nowrap",
    }}>
      {s.label}
    </span>
  );
}

// ─── TableNode (floor map) ────────────────────────────────────────────────────
function TableNode({ table, onSelect }: { table: Table; onSelect: () => void }) {
  const s = TS[table.status];
  const size = table.seats >= 6 ? 54 : table.seats >= 4 ? 46 : 38;
  const tappable = table.status === "mine" || table.status === "available";
  const orders = INITIAL_ORDERS[table.id] || [];
  const hasReady = orders.some((i) => i.status === "ready");

  return (
    <button
      onClick={tappable ? onSelect : undefined}
      style={{
        position: "absolute",
        left: `${table.x}%`, top: `${table.y}%`,
        transform: "translate(-50%, -50%)",
        width: size, height: size,
        borderRadius: table.seats >= 4 ? 8 : "50%",
        background: s.bg,
        border: `2px solid ${s.border}`,
        display: "flex", flexDirection: "column",
        alignItems: "center", justifyContent: "center",
        cursor: tappable ? "pointer" : "default",
        gap: 1, padding: 0,
        boxShadow: table.status === "mine"
          ? "0 0 0 3px rgba(249,115,22,0.2)"
          : "none",
        opacity: !tappable ? 0.7 : 1,
      }}
    >
      <span style={{
        fontSize: 10, fontWeight: 700, color: s.text,
        fontFamily: "var(--font-mono)", lineHeight: 1,
      }}>
        {table.label}
      </span>
      {table.covers && (
        <span style={{ fontSize: 9, color: s.text, opacity: 0.7, lineHeight: 1 }}>
          {table.covers}k
        </span>
      )}
      {hasReady && (
        <div style={{
          position: "absolute", top: -4, right: -4,
          width: 10, height: 10, borderRadius: "50%",
          background: "#22C55E", border: "2px solid white",
        }} />
      )}
    </button>
  );
}

// ─── FloorMap ─────────────────────────────────────────────────────────────────
function FloorMap({ filter, onSelectTable }: { filter: TableFilter; onSelectTable: (id: string) => void }) {
  const filtered = TABLES.filter((t) =>
    filter === "mine" ? t.status === "mine" :
    filter === "available" ? t.status === "available" : true
  );

  return (
    <div style={{ padding: "12px 16px 16px" }}>
      <div style={{ display: "flex", gap: 14, marginBottom: 12, flexWrap: "wrap" }}>
        {(["mine", "available", "occupied", "reserved"] as TableStatus[]).map((s) => (
          <div key={s} style={{ display: "flex", alignItems: "center", gap: 5 }}>
            <div style={{ width: 7, height: 7, borderRadius: "50%", background: TS[s].dot }} />
            <span style={{ fontSize: 11, color: "var(--color-ink-3)", fontFamily: "var(--font-mono)" }}>
              {TS[s].label}
            </span>
          </div>
        ))}
      </div>

      <div style={{
        position: "relative", width: "100%", paddingTop: "115%",
        background: "#FAFAF8", borderRadius: 14,
        border: "1px solid var(--color-paper-3)", overflow: "hidden",
      }}>
        {[
          { label: "İç Mekan", top: "3%" },
          { label: "Teras",    top: "52%" },
          { label: "Bar",      top: "76%" },
        ].map(({ label, top }) => (
          <div key={label} style={{
            position: "absolute", top, left: "50%", transform: "translateX(-50%)",
            fontSize: 9, fontFamily: "var(--font-mono)", color: "var(--color-ink-4)",
            letterSpacing: "0.1em", textTransform: "uppercase", whiteSpace: "nowrap",
          }}>
            {label}
          </div>
        ))}
        <div style={{ position: "absolute", top: "50%", left: "4%", right: "4%", height: 1, background: "var(--color-paper-3)" }} />
        <div style={{ position: "absolute", top: "74%", left: "4%", right: "4%", height: 1, background: "var(--color-paper-3)" }} />

        {filtered.map((t) => (
          <TableNode key={t.id} table={t} onSelect={() => onSelectTable(t.id)} />
        ))}
      </div>
    </div>
  );
}

// ─── TableList ────────────────────────────────────────────────────────────────
function TableList({ filter, onSelectTable }: { filter: TableFilter; onSelectTable: (id: string) => void }) {
  const filtered = TABLES.filter((t) =>
    filter === "mine" ? t.status === "mine" :
    filter === "available" ? t.status === "available" : true
  );

  return (
    <div>
      {["İç Mekan", "Teras", "Bar"].map((section) => {
        const rows = filtered.filter((t) => t.section === section);
        if (rows.length === 0) return null;
        return (
          <div key={section}>
            <div style={{
              padding: "7px 16px", background: "var(--color-paper-2)",
              fontSize: 10, fontWeight: 600, color: "var(--color-ink-3)",
              letterSpacing: "0.07em", textTransform: "uppercase", fontFamily: "var(--font-mono)",
            }}>
              {section}
            </div>
            {rows.map((t) => {
              const s = TS[t.status];
              const items = INITIAL_ORDERS[t.id] || [];
              const readyCount = items.filter((i) => i.status === "ready").length;
              const tappable = t.status === "mine" || t.status === "available";
              return (
                <button
                  key={t.id}
                  onClick={tappable ? () => onSelectTable(t.id) : undefined}
                  style={{
                    width: "100%", display: "flex", alignItems: "center", gap: 12,
                    padding: "12px 16px", background: "white", border: "none",
                    borderBottom: "1px solid var(--color-paper-3)",
                    cursor: tappable ? "pointer" : "default", textAlign: "left",
                    opacity: !tappable ? 0.65 : 1,
                  }}
                >
                  <div style={{
                    width: 46, height: 46, borderRadius: 10, flexShrink: 0,
                    background: s.bg, border: `2px solid ${s.border}`,
                    display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center",
                  }}>
                    <span style={{ fontSize: 11, fontWeight: 800, color: s.text, fontFamily: "var(--font-mono)" }}>
                      {t.label}
                    </span>
                    {t.covers && (
                      <span style={{ fontSize: 9, color: s.text, opacity: 0.7 }}>{t.covers}k</span>
                    )}
                  </div>

                  <div style={{ flex: 1, minWidth: 0 }}>
                    <div style={{ display: "flex", alignItems: "center", gap: 5, marginBottom: 3 }}>
                      <span style={{
                        padding: "1px 6px", borderRadius: 4, fontSize: 10, fontWeight: 700,
                        background: s.bg, color: s.text, fontFamily: "var(--font-mono)",
                      }}>
                        {s.label}
                      </span>
                      {readyCount > 0 && (
                        <span style={{
                          padding: "1px 6px", borderRadius: 4, fontSize: 10, fontWeight: 700,
                          background: "#DCFCE7", color: "#166534", fontFamily: "var(--font-mono)",
                        }}>
                          {readyCount} hazır
                        </span>
                      )}
                    </div>
                    <span style={{ fontSize: 12, color: "var(--color-ink-3)" }}>
                      {t.seats} kişilik
                      {t.openMinutes ? ` · ${fmtT(t.openMinutes)} açık` : ""}
                      {items.length > 0 ? ` · ${items.length} kalem` : ""}
                    </span>
                  </div>

                  {tappable && (
                    <svg width="16" height="16" viewBox="0 0 16 16" fill="none" style={{ flexShrink: 0 }}>
                      <path d="M6 4l4 4-4 4" stroke="#B5B0A8" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
                    </svg>
                  )}
                </button>
              );
            })}
          </div>
        );
      })}
    </div>
  );
}

// ─── SessionSheet ─────────────────────────────────────────────────────────────
function SessionSheet({
  tableId, orders, onClose, onAddItems, onSendToKitchen, networkStatus,
}: {
  tableId: string; orders: OrderItem[];
  onClose: () => void; onAddItems: () => void; onSendToKitchen: () => void;
  networkStatus: NetworkStatus;
}) {
  const table = TABLES.find((t) => t.id === tableId)!;
  const s = TS[table.status];
  const pending = orders.filter((i) => i.status === "pending");
  const isEmpty = orders.length === 0;

  return (
    <div style={{ position: "fixed", inset: 0, zIndex: 200, display: "flex", flexDirection: "column" }}>
      <div onClick={onClose} style={{ flex: 1, background: "rgba(15,14,12,0.5)", cursor: "pointer" }} />

      <div style={{
        background: "var(--color-paper)",
        borderRadius: "20px 20px 0 0",
        maxHeight: "88vh", display: "flex", flexDirection: "column",
        boxShadow: "0 -12px 40px rgba(0,0,0,0.15)",
        animation: "slideUp 0.22s ease-out",
      }}>
        {/* Handle */}
        <div style={{ display: "flex", justifyContent: "center", padding: "10px 0 0" }}>
          <div style={{ width: 36, height: 4, borderRadius: 2, background: "var(--color-paper-3)" }} />
        </div>

        {/* Header */}
        <div style={{
          padding: "10px 18px 14px",
          borderBottom: "1px solid var(--color-paper-3)",
          display: "flex", alignItems: "flex-start", justifyContent: "space-between",
        }}>
          <div>
            <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 3 }}>
              <span style={{ fontSize: 22, fontWeight: 900, letterSpacing: "-0.03em", color: "var(--color-ink)", fontFamily: "var(--font-mono)" }}>
                {table.label}
              </span>
              <span style={{
                padding: "3px 8px", borderRadius: 5, fontSize: 11, fontWeight: 700,
                background: s.bg, color: s.text, fontFamily: "var(--font-mono)",
              }}>
                {isEmpty ? "Yeni Oturum" : `${table.covers}k · ${fmtT(table.openMinutes || 0)} açık`}
              </span>
            </div>
            <span style={{ fontSize: 12, color: "var(--color-ink-3)" }}>
              {table.section} · {table.seats} kişilik
            </span>
          </div>
          <button
            onClick={onClose}
            style={{
              width: 32, height: 32, borderRadius: "50%", background: "var(--color-paper-2)",
              border: "none", cursor: "pointer", display: "flex", alignItems: "center", justifyContent: "center",
              flexShrink: 0,
            }}
          >
            <svg width="12" height="12" viewBox="0 0 12 12" fill="none">
              <path d="M1 1l10 10M11 1L1 11" stroke="var(--color-ink-3)" strokeWidth="1.5" strokeLinecap="round" />
            </svg>
          </button>
        </div>

        {/* Offline notice */}
        {networkStatus !== "online" && (
          <div style={{
            padding: "8px 18px",
            background: networkStatus === "offline" ? "#FEF2F2" : "#FFFBEB",
            borderBottom: "1px solid var(--color-paper-3)",
            fontSize: 12, fontWeight: 500,
            color: networkStatus === "offline" ? "#B91C1C" : "#92400E",
            display: "flex", alignItems: "center", gap: 6,
          }}>
            <span>{networkStatus === "offline" ? "⚡" : "↻"}</span>
            <span>
              {networkStatus === "offline"
                ? "Çevrimdışı — siparişler yerel olarak kaydedildi"
                : "Senkronize ediliyor..."}
            </span>
          </div>
        )}

        {/* Body */}
        <div style={{ flex: 1, overflowY: "auto" }}>
          {isEmpty ? (
            <div style={{ padding: "48px 20px", textAlign: "center" }}>
              <div style={{ fontSize: 36, marginBottom: 10 }}>🪑</div>
              <div style={{ fontSize: 15, fontWeight: 700, color: "var(--color-ink)", marginBottom: 4 }}>
                Oturum açıldı
              </div>
              <div style={{ fontSize: 13, color: "var(--color-ink-3)" }}>
                İlk siparişi eklemek için aşağıdaki butona dokunun
              </div>
            </div>
          ) : (
            (["ready", "preparing", "sent", "pending", "served"] as ItemStatus[]).map((status) => {
              const group = orders.filter((i) => i.status === status);
              if (group.length === 0) return null;
              const cfg = IS[status];
              return (
                <div key={status}>
                  <div style={{
                    padding: "5px 18px",
                    fontSize: 10, fontWeight: 700, letterSpacing: "0.06em",
                    color: cfg.color, background: cfg.bg,
                    fontFamily: "var(--font-mono)", textTransform: "uppercase",
                    display: "flex", justifyContent: "space-between",
                  }}>
                    <span>{cfg.label}</span>
                    <span>{group.length} kalem</span>
                  </div>
                  {group.map((item) => (
                    <div key={item.id} style={{
                      padding: "11px 18px",
                      borderBottom: "1px solid var(--color-paper-3)",
                      display: "flex", alignItems: "center", gap: 10,
                      background: status === "served" ? "var(--color-paper-2)" : "white",
                      opacity: status === "served" ? 0.65 : 1,
                    }}>
                      <div style={{
                        width: 28, height: 28, borderRadius: 6,
                        background: status === "ready" ? "#DCFCE7" : "var(--color-paper-2)",
                        display: "flex", alignItems: "center", justifyContent: "center",
                        fontSize: 11, fontWeight: 800, color: status === "ready" ? "#166534" : "var(--color-ink-2)",
                        fontFamily: "var(--font-mono)", flexShrink: 0,
                      }}>
                        {item.qty}×
                      </div>
                      <div style={{ flex: 1, minWidth: 0 }}>
                        <div style={{
                          fontSize: 14, fontWeight: status === "ready" ? 700 : 500,
                          color: "var(--color-ink)", whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis",
                        }}>
                          {item.name}
                        </div>
                        {item.modifier && (
                          <div style={{ fontSize: 11, color: "var(--color-ink-3)", fontStyle: "italic" }}>
                            {item.modifier}
                          </div>
                        )}
                      </div>
                      <div style={{
                        fontSize: 13, fontWeight: 700, color: "var(--color-ink)",
                        fontFamily: "var(--font-mono)", flexShrink: 0,
                      }}>
                        {fmt(item.price * item.qty)}
                      </div>
                    </div>
                  ))}
                </div>
              );
            })
          )}
        </div>

        {/* Footer */}
        <div style={{
          borderTop: "1px solid var(--color-paper-3)",
          padding: "12px 16px 24px", background: "white",
        }}>
          {!isEmpty && (
            <div style={{
              display: "flex", justifyContent: "space-between", alignItems: "baseline",
              marginBottom: 12, padding: "0 2px",
            }}>
              <span style={{ fontSize: 14, color: "var(--color-ink-2)" }}>Toplam</span>
              <span style={{
                fontSize: 20, fontWeight: 900, color: "var(--color-ink)",
                fontFamily: "var(--font-mono)", letterSpacing: "-0.03em",
              }}>
                {fmt(sum(orders))}
              </span>
            </div>
          )}

          <div style={{ display: "flex", gap: 8 }}>
            {pending.length > 0 && (
              <button
                onClick={onSendToKitchen}
                style={{
                  flex: 1, padding: 14, borderRadius: 13,
                  background: "var(--color-ink)", color: "white",
                  border: "none", fontSize: 14, fontWeight: 700, cursor: "pointer",
                }}
              >
                Mutfağa Gönder ({pending.length})
              </button>
            )}
            <button
              onClick={onAddItems}
              style={{
                flex: pending.length > 0 ? "0 0 auto" : 1,
                padding: pending.length > 0 ? "14px 18px" : 14,
                borderRadius: 13,
                background: "#F97316", color: "white",
                border: "none", fontSize: 14, fontWeight: 700, cursor: "pointer",
              }}
            >
              {pending.length > 0 ? "＋ Ekle" : "＋ Sipariş Ekle"}
            </button>
            {!isEmpty && (
              <button
                style={{
                  padding: "14px 14px", borderRadius: 13,
                  background: "var(--color-paper-2)", color: "var(--color-ink)",
                  border: "none", fontSize: 13, fontWeight: 600, cursor: "pointer",
                  flexShrink: 0,
                }}
              >
                Adisyon
              </button>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}

// ─── MenuSheet ────────────────────────────────────────────────────────────────
function MenuSheet({ onClose, onAddItem }: {
  onClose: () => void;
  onAddItem: (item: MenuItem, qty: number) => void;
}) {
  const [cat, setCat] = useState("starters");
  const [qtys, setQtys] = useState<Record<string, number>>({});

  const catItems = ITEMS.filter((i) => i.categoryId === cat);
  const totalAdded = Object.values(qtys).reduce((s, q) => s + q, 0);
  const totalPrice = Object.entries(qtys).reduce((s, [id, q]) => {
    const item = ITEMS.find((i) => i.id === id);
    return s + (item?.price || 0) * q;
  }, 0);

  const adjust = (id: string, delta: number) => {
    setQtys((prev) => {
      const next = Math.max(0, (prev[id] || 0) + delta);
      const updated = { ...prev, [id]: next };
      if (next === 0) delete updated[id];
      return updated;
    });
  };

  const handleAdd = () => {
    Object.entries(qtys).forEach(([id, qty]) => {
      if (qty > 0) {
        const item = ITEMS.find((i) => i.id === id)!;
        onAddItem(item, qty);
      }
    });
    onClose();
  };

  return (
    <div style={{ position: "fixed", inset: 0, zIndex: 300, display: "flex", flexDirection: "column" }}>
      <div onClick={onClose} style={{ flex: "0 0 15%", background: "rgba(15,14,12,0.6)", cursor: "pointer" }} />

      <div style={{
        background: "white", borderRadius: "20px 20px 0 0",
        flex: 1, display: "flex", flexDirection: "column",
        boxShadow: "0 -12px 40px rgba(0,0,0,0.2)",
        overflow: "hidden",
        animation: "slideUp 0.22s ease-out",
      }}>
        {/* Handle + header */}
        <div style={{ padding: "10px 18px 0", flexShrink: 0 }}>
          <div style={{ display: "flex", justifyContent: "center", marginBottom: 10 }}>
            <div style={{ width: 36, height: 4, borderRadius: 2, background: "var(--color-paper-3)" }} />
          </div>
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 12 }}>
            <span style={{ fontSize: 20, fontWeight: 900, color: "var(--color-ink)", letterSpacing: "-0.02em" }}>
              Menü
            </span>
            <button
              onClick={onClose}
              style={{
                width: 32, height: 32, borderRadius: "50%", background: "var(--color-paper-2)",
                border: "none", cursor: "pointer", display: "flex", alignItems: "center", justifyContent: "center",
              }}
            >
              <svg width="12" height="12" viewBox="0 0 12 12" fill="none">
                <path d="M1 1l10 10M11 1L1 11" stroke="var(--color-ink-3)" strokeWidth="1.5" strokeLinecap="round" />
              </svg>
            </button>
          </div>
        </div>

        {/* Categories */}
        <div style={{
          display: "flex", gap: 6, padding: "0 18px 12px",
          overflowX: "auto", flexShrink: 0,
        }}>
          {CATS.map((c) => (
            <button
              key={c.id}
              onClick={() => setCat(c.id)}
              style={{
                display: "flex", alignItems: "center", gap: 5,
                padding: "7px 13px", borderRadius: 20, border: "none",
                background: cat === c.id ? "var(--color-ink)" : "var(--color-paper-2)",
                color: cat === c.id ? "white" : "var(--color-ink-2)",
                fontSize: 13, fontWeight: 600, cursor: "pointer",
                whiteSpace: "nowrap", flexShrink: 0,
              }}
            >
              <span>{c.icon}</span>
              <span>{c.name}</span>
            </button>
          ))}
        </div>

        {/* Items */}
        <div style={{ flex: 1, overflowY: "auto" }}>
          {catItems.map((item) => {
            const qty = qtys[item.id] || 0;
            return (
              <div key={item.id} style={{
                padding: "12px 18px",
                borderBottom: "1px solid var(--color-paper-3)",
                display: "flex", alignItems: "center", gap: 12,
                background: qty > 0 ? "rgba(249,115,22,0.04)" : "white",
              }}>
                <div style={{ flex: 1 }}>
                  <div style={{ display: "flex", alignItems: "center", gap: 6, marginBottom: 2 }}>
                    <span style={{ fontSize: 14, fontWeight: 600, color: "var(--color-ink)" }}>
                      {item.name}
                    </span>
                    {item.popular && (
                      <span style={{
                        padding: "1px 5px", borderRadius: 3, fontSize: 9, fontWeight: 800,
                        background: "rgba(249,115,22,0.12)", color: "#9A3412",
                        fontFamily: "var(--font-mono)", letterSpacing: "0.04em",
                      }}>
                        POP
                      </span>
                    )}
                  </div>
                  <div style={{ fontSize: 12, color: "var(--color-ink-3)", marginBottom: 3 }}>
                    {item.description}
                  </div>
                  <div style={{
                    fontSize: 14, fontWeight: 800, color: "var(--color-ink)",
                    fontFamily: "var(--font-mono)",
                  }}>
                    {fmt(item.price)}
                  </div>
                </div>

                <div style={{ display: "flex", alignItems: "center", gap: 2, flexShrink: 0 }}>
                  {qty > 0 ? (
                    <>
                      <button
                        onClick={() => adjust(item.id, -1)}
                        style={{
                          width: 34, height: 34, borderRadius: "50%",
                          background: "var(--color-paper-2)", border: "none",
                          fontSize: 20, color: "var(--color-ink)", cursor: "pointer",
                          display: "flex", alignItems: "center", justifyContent: "center",
                          fontWeight: 300, lineHeight: 1,
                        }}
                      >
                        −
                      </button>
                      <span style={{
                        width: 30, textAlign: "center",
                        fontSize: 16, fontWeight: 800, color: "var(--color-ink)",
                        fontFamily: "var(--font-mono)",
                      }}>
                        {qty}
                      </span>
                      <button
                        onClick={() => adjust(item.id, 1)}
                        style={{
                          width: 34, height: 34, borderRadius: "50%",
                          background: "#F97316", border: "none",
                          fontSize: 20, color: "white", cursor: "pointer",
                          display: "flex", alignItems: "center", justifyContent: "center",
                          fontWeight: 300, lineHeight: 1,
                        }}
                      >
                        ＋
                      </button>
                    </>
                  ) : (
                    <button
                      onClick={() => adjust(item.id, 1)}
                      style={{
                        width: 38, height: 38, borderRadius: "50%",
                        background: "var(--color-paper-2)", border: "none",
                        fontSize: 22, color: "var(--color-ink-3)", cursor: "pointer",
                        display: "flex", alignItems: "center", justifyContent: "center",
                        fontWeight: 300, lineHeight: 1,
                      }}
                    >
                      ＋
                    </button>
                  )}
                </div>
              </div>
            );
          })}
        </div>

        {/* Add CTA */}
        {totalAdded > 0 && (
          <div style={{ padding: "12px 16px 28px", background: "white", borderTop: "1px solid var(--color-paper-3)" }}>
            <button
              onClick={handleAdd}
              style={{
                width: "100%", padding: "15px 18px",
                borderRadius: 14, border: "none",
                background: "#F97316", color: "white",
                fontSize: 15, fontWeight: 800, cursor: "pointer",
                display: "flex", justifyContent: "space-between", alignItems: "center",
              }}
            >
              <span>{totalAdded} kalem ekle</span>
              <span style={{ fontFamily: "var(--font-mono)", letterSpacing: "-0.02em" }}>
                {fmt(totalPrice)}
              </span>
            </button>
          </div>
        )}
      </div>
    </div>
  );
}

// ─── OrdersTab ────────────────────────────────────────────────────────────────
function OrdersTab({ orders, onOpenTable }: {
  orders: Record<string, OrderItem[]>;
  onOpenTable: (id: string) => void;
}) {
  const myTables = TABLES.filter((t) => t.status === "mine");

  return (
    <div style={{ paddingBottom: 8 }}>
      {myTables.map((table) => {
        const items = orders[table.id] || [];
        const active = items.filter((i) => i.status !== "served");
        const ready = items.filter((i) => i.status === "ready");
        if (items.length === 0 && active.length === 0) return null;

        return (
          <div key={table.id} style={{
            margin: "12px 16px 0",
            border: "1px solid var(--color-paper-3)", borderRadius: 14,
            overflow: "hidden", background: "white",
          }}>
            <button
              onClick={() => onOpenTable(table.id)}
              style={{
                width: "100%", padding: "12px 16px",
                background: ready.length > 0 ? "#F0FDF4" : "var(--color-paper-2)",
                border: "none", cursor: "pointer", textAlign: "left",
                display: "flex", alignItems: "center", justifyContent: "space-between",
                borderBottom: "1px solid var(--color-paper-3)",
              }}
            >
              <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                <span style={{
                  fontSize: 15, fontWeight: 900, fontFamily: "var(--font-mono)",
                  color: "var(--color-ink)", letterSpacing: "-0.01em",
                }}>
                  {table.label}
                </span>
                <span style={{ fontSize: 12, color: "var(--color-ink-3)" }}>
                  {table.covers}k · {fmtT(table.openMinutes || 0)}
                </span>
              </div>
              <div style={{ display: "flex", gap: 6, alignItems: "center" }}>
                {ready.length > 0 && (
                  <span style={{
                    padding: "3px 8px", borderRadius: 6, fontSize: 11, fontWeight: 700,
                    background: "#DCFCE7", color: "#166534", fontFamily: "var(--font-mono)",
                  }}>
                    {ready.length} hazır
                  </span>
                )}
                <svg width="14" height="14" viewBox="0 0 14 14" fill="none">
                  <path d="M5 3l4 4-4 4" stroke="#B5B0A8" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
                </svg>
              </div>
            </button>

            {active.slice(0, 5).map((item) => {
              const s = IS[item.status];
              return (
                <div key={item.id} style={{
                  padding: "8px 16px",
                  display: "flex", alignItems: "center", gap: 10,
                  borderBottom: "1px solid var(--color-paper-3)",
                }}>
                  <span style={{
                    fontSize: 11, color: "var(--color-ink-3)", width: 20,
                    fontFamily: "var(--font-mono)", flexShrink: 0,
                  }}>
                    {item.qty}×
                  </span>
                  <span style={{
                    flex: 1, fontSize: 13, color: "var(--color-ink)",
                    fontWeight: item.status === "ready" ? 700 : 400,
                  }}>
                    {item.name}
                  </span>
                  <span style={{
                    padding: "2px 7px", borderRadius: 4, fontSize: 10, fontWeight: 600,
                    background: s.bg, color: s.color, fontFamily: "var(--font-mono)",
                  }}>
                    {s.label}
                  </span>
                </div>
              );
            })}
            {active.length > 5 && (
              <div style={{ padding: "8px 16px", fontSize: 12, color: "var(--color-ink-3)", textAlign: "center" }}>
                +{active.length - 5} kalem daha
              </div>
            )}

            <div style={{
              padding: "10px 16px", display: "flex",
              justifyContent: "space-between", alignItems: "center",
            }}>
              <span style={{ fontSize: 11, color: "var(--color-ink-3)", fontFamily: "var(--font-mono)" }}>
                {items.length} kalem · {sum(items) > 0 ? "" : "boş"}
              </span>
              <span style={{
                fontSize: 16, fontWeight: 900, color: "var(--color-ink)",
                fontFamily: "var(--font-mono)", letterSpacing: "-0.02em",
              }}>
                {fmt(sum(items))}
              </span>
            </div>
          </div>
        );
      })}
    </div>
  );
}

// ─── NotificationsTab ─────────────────────────────────────────────────────────
function NotificationsTab({ notifs, onMarkRead }: {
  notifs: Notif[];
  onMarkRead: (id: string) => void;
}) {
  const unread = notifs.filter((n) => !n.read);
  const read = notifs.filter((n) => n.read);

  if (notifs.length === 0) {
    return (
      <div style={{ padding: "80px 20px", textAlign: "center" }}>
        <div style={{ fontSize: 40, marginBottom: 12 }}>🔔</div>
        <div style={{ fontSize: 15, fontWeight: 700, color: "var(--color-ink)", marginBottom: 4 }}>
          Bildirim yok
        </div>
        <div style={{ fontSize: 13, color: "var(--color-ink-3)" }}>
          Mutfaktan siparişler hazır olduğunda burada görünecek
        </div>
      </div>
    );
  }

  return (
    <div>
      {unread.length > 0 && (
        <div>
          <div style={{
            padding: "7px 16px", fontSize: 10, fontWeight: 700, letterSpacing: "0.07em",
            textTransform: "uppercase", color: "var(--color-ink-3)",
            fontFamily: "var(--font-mono)", background: "var(--color-paper-2)",
          }}>
            Yeni · {unread.length}
          </div>
          {unread.map((n) => (
            <button
              key={n.id}
              onClick={() => onMarkRead(n.id)}
              style={{
                width: "100%", textAlign: "left", padding: "14px 16px",
                background: "rgba(249,115,22,0.05)",
                border: "none", borderBottom: "1px solid var(--color-paper-3)", cursor: "pointer",
                display: "flex", alignItems: "flex-start", gap: 12,
              }}
            >
              <div style={{
                width: 8, height: 8, borderRadius: "50%",
                background: "#22C55E", marginTop: 6, flexShrink: 0,
              }} />
              <div style={{ flex: 1 }}>
                <div style={{ display: "flex", gap: 6, alignItems: "center", marginBottom: 4 }}>
                  <span style={{
                    fontFamily: "var(--font-mono)", fontSize: 11, fontWeight: 800,
                    color: "#9A3412", padding: "1px 5px",
                    background: "rgba(249,115,22,0.12)", borderRadius: 3,
                  }}>
                    {n.tableId}
                  </span>
                  <span style={{ fontSize: 10, color: "var(--color-ink-4)", fontFamily: "var(--font-mono)" }}>
                    {n.time}
                  </span>
                </div>
                <span style={{ fontSize: 13, fontWeight: 700, color: "var(--color-ink)" }}>
                  {n.message}
                </span>
              </div>
              <div style={{
                width: 38, height: 38, borderRadius: 10, background: "#DCFCE7",
                display: "flex", alignItems: "center", justifyContent: "center",
                fontSize: 18, flexShrink: 0,
              }}>
                ✓
              </div>
            </button>
          ))}
        </div>
      )}

      {read.length > 0 && (
        <div>
          <div style={{
            padding: "7px 16px", fontSize: 10, fontWeight: 700, letterSpacing: "0.07em",
            textTransform: "uppercase", color: "var(--color-ink-3)",
            fontFamily: "var(--font-mono)", background: "var(--color-paper-2)",
          }}>
            Önceki
          </div>
          {read.map((n) => (
            <div key={n.id} style={{
              padding: "12px 16px", borderBottom: "1px solid var(--color-paper-3)",
              display: "flex", alignItems: "flex-start", gap: 12, opacity: 0.5,
            }}>
              <div style={{
                width: 8, height: 8, borderRadius: "50%",
                background: "var(--color-paper-3)", marginTop: 6, flexShrink: 0,
              }} />
              <div>
                <div style={{ display: "flex", gap: 6, alignItems: "center", marginBottom: 3 }}>
                  <span style={{ fontFamily: "var(--font-mono)", fontSize: 11, color: "var(--color-ink-3)" }}>
                    {n.tableId}
                  </span>
                  <span style={{ fontSize: 10, color: "var(--color-ink-4)", fontFamily: "var(--font-mono)" }}>
                    {n.time}
                  </span>
                </div>
                <span style={{ fontSize: 13, color: "var(--color-ink-2)" }}>{n.message}</span>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

// ─── BottomNav ────────────────────────────────────────────────────────────────
function BottomNav({ tab, setTab, notifCount }: {
  tab: Tab; setTab: (t: Tab) => void; notifCount: number;
}) {
  const items: Array<{ id: Tab; label: string; icon: ReactNode; badge?: number }> = [
    {
      id: "tables",
      label: "Masalar",
      icon: (
        <svg width="22" height="22" viewBox="0 0 22 22" fill="none">
          <rect x="2" y="2" width="8" height="8" rx="2" fill="currentColor" opacity={tab === "tables" ? 1 : 0.3} />
          <rect x="12" y="2" width="8" height="8" rx="2" fill="currentColor" opacity={tab === "tables" ? 0.5 : 0.15} />
          <rect x="2" y="12" width="8" height="8" rx="2" fill="currentColor" opacity={tab === "tables" ? 0.5 : 0.15} />
          <rect x="12" y="12" width="8" height="8" rx="2" fill="currentColor" opacity={tab === "tables" ? 1 : 0.3} />
        </svg>
      ),
    },
    {
      id: "orders",
      label: "Siparişler",
      icon: (
        <svg width="22" height="22" viewBox="0 0 22 22" fill="none">
          <rect x="3.5" y="2" width="15" height="18" rx="2.5" stroke="currentColor" strokeWidth="1.7"
            fill="none" opacity={tab === "orders" ? 1 : 0.35} />
          <path d="M7 7.5h8M7 11.5h8M7 15.5h5" stroke="currentColor" strokeWidth="1.6"
            strokeLinecap="round" opacity={tab === "orders" ? 1 : 0.35} />
        </svg>
      ),
    },
    {
      id: "notif",
      label: "Bildirimler",
      badge: notifCount,
      icon: (
        <svg width="22" height="22" viewBox="0 0 22 22" fill="none">
          <path d="M11 3C8.24 3 6 5.24 6 8v5l-2 2v1h14v-1l-2-2V8c0-2.76-2.24-5-5-5z"
            stroke="currentColor" strokeWidth="1.7" fill="none" opacity={tab === "notif" ? 1 : 0.35} />
          <path d="M9.27 19.5a2 2 0 003.46 0"
            stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" opacity={tab === "notif" ? 1 : 0.35} />
        </svg>
      ),
    },
  ];

  return (
    <div style={{
      position: "fixed", bottom: 0, left: "50%", transform: "translateX(-50%)",
      width: "100%", maxWidth: 480,
      zIndex: 100, background: "white",
      borderTop: "1px solid var(--color-paper-3)",
      display: "flex",
    }}>
      {items.map((item) => (
        <button
          key={item.id}
          onClick={() => setTab(item.id)}
          style={{
            flex: 1, padding: "10px 4px 14px", border: "none", background: "transparent",
            cursor: "pointer", display: "flex", flexDirection: "column",
            alignItems: "center", gap: 3,
            color: tab === item.id ? "#F97316" : "var(--color-ink-3)",
            position: "relative",
          }}
        >
          {item.icon}
          <span style={{ fontSize: 10, fontWeight: tab === item.id ? 700 : 500, lineHeight: 1 }}>
            {item.label}
          </span>
          {(item.badge ?? 0) > 0 && (
            <div style={{
              position: "absolute", top: 6, right: "calc(50% - 20px)",
              width: 16, height: 16, borderRadius: "50%",
              background: "#EF4444", color: "white",
              fontSize: 9, fontWeight: 800, fontFamily: "var(--font-mono)",
              display: "flex", alignItems: "center", justifyContent: "center",
              border: "2px solid white",
            }}>
              {item.badge}
            </div>
          )}
        </button>
      ))}
    </div>
  );
}

// ─── App ──────────────────────────────────────────────────────────────────────
export default function App() {
  const [tab, setTab]                   = useState<Tab>("tables");
  const [viewMode, setViewMode]         = useState<ViewMode>("map");
  const [filter, setFilter]             = useState<TableFilter>("all");
  const [selectedTable, setSelectedTable] = useState<string | null>(null);
  const [showMenu, setShowMenu]         = useState(false);
  const [orders, setOrders]             = useState<Record<string, OrderItem[]>>(INITIAL_ORDERS);
  const [network, setNetwork]           = useState<NetworkStatus>("online");
  const [notifs, setNotifs]             = useState<Notif[]>(INITIAL_NOTIFS);
  const pendingSync                     = 3;

  const unread = notifs.filter((n) => !n.read).length;
  const bannerShown = network !== "online";
  const bannerH = bannerShown ? 44 : 0;

  const handleSelectTable = (id: string) => {
    setSelectedTable(id);
    setTab("tables");
  };

  const handleAddItem = (item: MenuItem, qty: number) => {
    if (!selectedTable) return;
    const newItem: OrderItem = {
      id: `new-${Date.now()}-${item.id}`,
      name: item.name, qty, price: item.price, status: "pending",
    };
    setOrders((prev) => ({
      ...prev,
      [selectedTable]: [...(prev[selectedTable] || []), newItem],
    }));
  };

  const handleSendToKitchen = () => {
    if (!selectedTable) return;
    setOrders((prev) => ({
      ...prev,
      [selectedTable]: (prev[selectedTable] || []).map((i) =>
        i.status === "pending" ? { ...i, status: "sent" as ItemStatus } : i
      ),
    }));
  };

  const cycleNetwork = () => {
    setNetwork((s) => s === "online" ? "offline" : s === "offline" ? "syncing" : "online");
  };

  return (
    <div style={{
      minHeight: "100vh", background: "var(--color-paper)",
      fontFamily: "var(--font-sans)",
      display: "flex", justifyContent: "center",
    }}>
      <div style={{
        width: "100%", maxWidth: 480, position: "relative",
        display: "flex", flexDirection: "column", minHeight: "100vh",
      }}>
        <NetworkBanner status={network} count={pendingSync} />

        {/* Top bar */}
        <div style={{
          position: "sticky", top: bannerH, zIndex: 90,
          background: "white", borderBottom: "1px solid var(--color-paper-3)",
          transition: "top 0.2s",
        }}>
          {/* Header row */}
          <div style={{
            display: "flex", alignItems: "center", justifyContent: "space-between",
            padding: "0 16px", height: 54,
          }}>
            <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
              <div style={{
                width: 30, height: 30, borderRadius: 8, background: "#F97316",
                display: "flex", alignItems: "center", justifyContent: "center",
              }}>
                <svg width="15" height="15" viewBox="0 0 14 14" fill="none">
                  <rect x="1" y="1" width="5" height="5" rx="1" fill="white" />
                  <rect x="8" y="1" width="5" height="5" rx="1" fill="white" opacity="0.6" />
                  <rect x="1" y="8" width="5" height="5" rx="1" fill="white" opacity="0.6" />
                  <rect x="8" y="8" width="5" height="5" rx="1" fill="white" />
                </svg>
              </div>
              <div>
                <div style={{ fontSize: 13, fontWeight: 800, color: "var(--color-ink)", lineHeight: 1.2, letterSpacing: "-0.01em" }}>
                  Ahmet Y.
                </div>
                <div style={{ fontSize: 10, color: "var(--color-ink-3)", fontFamily: "var(--font-mono)" }}>
                  Garson · Shift 14:00–22:00
                </div>
              </div>
            </div>

            {/* Network toggle (demo) */}
            <button
              onClick={cycleNetwork}
              title="Ağ durumunu değiştir"
              style={{
                padding: "5px 10px", borderRadius: 20, border: "none", cursor: "pointer",
                background: network === "online" ? "#F0FDF4" : network === "offline" ? "#FEF2F2" : "#FFFBEB",
                color: network === "online" ? "#166534" : network === "offline" ? "#B91C1C" : "#92400E",
                fontSize: 11, fontWeight: 700, fontFamily: "var(--font-mono)",
                display: "flex", alignItems: "center", gap: 5,
              }}
            >
              <div style={{
                width: 6, height: 6, borderRadius: "50%",
                background: network === "online" ? "#22C55E" : network === "offline" ? "#EF4444" : "#F59E0B",
              }} />
              {network === "online" ? "Online" : network === "offline" ? "Offline" : "Sync"}
            </button>
          </div>

          {/* Context controls */}
          {tab === "tables" && (
            <div style={{
              display: "flex", justifyContent: "space-between", alignItems: "center",
              padding: "0 16px 10px",
            }}>
              <div style={{ display: "flex", gap: 4 }}>
                {([
                  { id: "all" as TableFilter, label: "Tümü" },
                  { id: "mine" as TableFilter, label: "Benim" },
                  { id: "available" as TableFilter, label: "Boş" },
                ]).map(({ id, label }) => (
                  <button
                    key={id}
                    onClick={() => setFilter(id)}
                    style={{
                      padding: "5px 13px", borderRadius: 20, border: "none", cursor: "pointer",
                      background: filter === id ? "var(--color-ink)" : "var(--color-paper-2)",
                      color: filter === id ? "white" : "var(--color-ink-2)",
                      fontSize: 12, fontWeight: 600,
                    }}
                  >
                    {label}
                  </button>
                ))}
              </div>

              <div style={{
                display: "flex", background: "var(--color-paper-2)", borderRadius: 9, padding: 2, gap: 1,
              }}>
                {(["map", "list"] as ViewMode[]).map((m) => (
                  <button
                    key={m}
                    onClick={() => setViewMode(m)}
                    style={{
                      padding: "5px 10px", borderRadius: 7, border: "none", cursor: "pointer",
                      background: viewMode === m ? "white" : "transparent",
                      color: "var(--color-ink-2)", fontSize: 12, fontWeight: 600,
                      boxShadow: viewMode === m ? "0 1px 3px rgba(0,0,0,0.08)" : "none",
                    }}
                  >
                    {m === "map" ? "Harita" : "Liste"}
                  </button>
                ))}
              </div>
            </div>
          )}

          {tab === "orders" && (
            <div style={{ padding: "0 16px 10px" }}>
              <span style={{ fontSize: 16, fontWeight: 900, color: "var(--color-ink)", letterSpacing: "-0.02em" }}>
                Aktif Siparişler
              </span>
            </div>
          )}

          {tab === "notif" && (
            <div style={{
              padding: "0 16px 10px",
              display: "flex", justifyContent: "space-between", alignItems: "center",
            }}>
              <span style={{ fontSize: 16, fontWeight: 900, color: "var(--color-ink)", letterSpacing: "-0.02em" }}>
                Bildirimler
              </span>
              {unread > 0 && (
                <button
                  onClick={() => setNotifs((prev) => prev.map((n) => ({ ...n, read: true })))}
                  style={{
                    background: "none", border: "none", cursor: "pointer",
                    fontSize: 12, color: "#F97316", fontWeight: 700,
                  }}
                >
                  Tümünü okundu işaretle
                </button>
              )}
            </div>
          )}
        </div>

        {/* Main content */}
        <div style={{ flex: 1, overflowY: "auto", paddingBottom: 72 }}>
          {tab === "tables" && (
            viewMode === "map"
              ? <FloorMap filter={filter} onSelectTable={handleSelectTable} />
              : <TableList filter={filter} onSelectTable={handleSelectTable} />
          )}
          {tab === "orders" && (
            <OrdersTab orders={orders} onOpenTable={handleSelectTable} />
          )}
          {tab === "notif" && (
            <NotificationsTab notifs={notifs} onMarkRead={(id) => setNotifs((prev) => prev.map((n) => n.id === id ? { ...n, read: true } : n))} />
          )}
        </div>

        <BottomNav tab={tab} setTab={setTab} notifCount={unread} />

        {/* Session sheet */}
        {selectedTable && !showMenu && (
          <SessionSheet
            tableId={selectedTable}
            orders={orders[selectedTable] || []}
            onClose={() => setSelectedTable(null)}
            onAddItems={() => setShowMenu(true)}
            onSendToKitchen={handleSendToKitchen}
            networkStatus={network}
          />
        )}

        {/* Menu sheet */}
        {showMenu && (
          <MenuSheet
            onClose={() => setShowMenu(false)}
            onAddItem={handleAddItem}
          />
        )}
      </div>
    </div>
  );
}
