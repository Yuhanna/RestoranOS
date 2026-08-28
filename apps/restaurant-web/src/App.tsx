import { FormEvent, useCallback, useEffect, useMemo, useState } from "react";
import { Button } from "@restaurant-os/design-system";
import { ApiError, api as defaultApi, type ManagementApi } from "./api";
import {
  nextStatuses,
  type DiningTable,
  type GeneratedQr,
  type LoginInput,
  type MenuDetail,
  type MenuProductRecord,
  type MenuSummary,
  type Order,
  type OrderStatus,
  type QrPrint,
  type Session,
  type TableQrCode,
} from "./domain";
import { realtime as defaultRealtime, type RealtimeClient, type RealtimeState } from "./realtime";

const statusLabel: Record<OrderStatus, string> = {
  submitted: "YENİ",
  accepted: "ONAYLANDI",
  preparing: "HAZIRLANIYOR",
  ready: "PASA ÇIKTI",
  completed: "SERVİS EDİLDİ",
  cancelled: "İPTAL",
};

const actionLabel: Record<OrderStatus, string> = {
  submitted: "Yeni",
  accepted: "Siparişe al",
  preparing: "Hazırlamaya başla",
  ready: "Pasa çıkar",
  completed: "Servis edildi",
  cancelled: "İptal",
};

const stationFor = (status: OrderStatus) =>
  status === "submitted" ? "Giriş" : status === "ready" ? "Pas" : "Mutfak";

const errorMessage = (error: unknown) => {
  if (!(error instanceof ApiError)) return "Beklenmeyen bir hata oluştu.";
  if (error.status === 403) return "Bu işlem için Order.Modify yetkiniz bulunmuyor.";
  if (error.status === 409) return "Sipariş başka bir ekranda güncellendi; liste yenilendi.";
  if (error.status === 429) {
    return error.retryAfterSeconds
      ? `${error.retryAfterSeconds} saniye sonra tekrar deneyin.`
      : "Çok fazla istek gönderildi; kısa süre sonra tekrar deneyin.";
  }
  return error.message;
};

const mergeOrder = (orders: Order[], incoming: Order) => {
  if (incoming.status === "completed" || incoming.status === "cancelled") {
    return orders.filter((order) => order.id !== incoming.id);
  }
  const current = orders.find((order) => order.id === incoming.id);
  const merged = current
    ? { ...current, ...incoming, createdAtUtc: incoming.createdAtUtc ?? current.createdAtUtc }
    : incoming;
  return current
    ? orders.map((order) => (order.id === incoming.id ? merged : order))
    : [...orders, merged];
};

type AppProps = {
  managementApi?: ManagementApi;
  realtimeClient?: RealtimeClient;
};

export function App({ managementApi = defaultApi, realtimeClient = defaultRealtime }: AppProps) {
  const [session, setSession] = useState<Session | null>(null);
  const [booting, setBooting] = useState(true);
  const [orders, setOrders] = useState<Order[]>([]);
  const [error, setError] = useState("");
  const [loadingOrders, setLoadingOrders] = useState(false);
  const [pendingOrder, setPendingOrder] = useState<string | null>(null);
  const [realtimeState, setRealtimeState] = useState<RealtimeState>("offline");
  const [statusFilter, setStatusFilter] = useState<"all" | OrderStatus>("all");
  const [stationFilter, setStationFilter] = useState("Tümü");
  const [now, setNow] = useState(() => Date.now());
  const [view, setView] = useState<"orders" | "tables" | "menus">("orders");

  const loadOrders = useCallback(async () => {
    setLoadingOrders(true);
    try {
      setOrders(await managementApi.getActiveOrders());
      setError("");
    } catch (loadError) {
      if (loadError instanceof ApiError && loadError.status === 401) setSession(null);
      setError(errorMessage(loadError));
    } finally {
      setLoadingOrders(false);
    }
  }, [managementApi]);

  useEffect(() => {
    let active = true;
    managementApi
      .restore()
      .then(async (restored) => {
        if (!active) return;
        setSession(restored);
        await loadOrders();
      })
      .catch(() => {
        if (active) setSession(null);
      })
      .finally(() => {
        if (active) setBooting(false);
      });
    return () => {
      active = false;
    };
  }, [loadOrders, managementApi]);

  useEffect(() => {
    if (!session) return;
    let stop: (() => Promise<void>) | undefined;
    let cancelled = false;
    realtimeClient
      .start(
        (incoming) => setOrders((current) => mergeOrder(current, incoming)),
        setOrders,
        (state) => {
          setRealtimeState(state);
          if (state === "offline" && !managementApi.getSession()) setSession(null);
        },
      )
      .then((stopClient) => {
        if (cancelled) void stopClient();
        else stop = stopClient;
      })
      .catch(() => setRealtimeState("offline"));
    return () => {
      cancelled = true;
      if (stop) void stop();
    };
  }, [realtimeClient, session]);

  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 30_000);
    return () => window.clearInterval(timer);
  }, []);

  const login = async (input: LoginInput) => {
    try {
      const authenticated = await managementApi.login(input);
      setSession(authenticated);
      setError("");
      await loadOrders();
    } catch (loginError) {
      setError(errorMessage(loginError));
    }
  };

  const logout = async () => {
    await managementApi.logout();
    setSession(null);
    setOrders([]);
  };

  const changeStatus = async (order: Order, status: OrderStatus) => {
    setPendingOrder(order.id);
    try {
      const changed = await managementApi.changeStatus(order, status);
      setOrders((current) => mergeOrder(current, changed));
      setError("");
    } catch (statusError) {
      setError(errorMessage(statusError));
      if (statusError instanceof ApiError && statusError.status === 409) await loadOrders();
      if (statusError instanceof ApiError && statusError.status === 401) setSession(null);
    } finally {
      setPendingOrder(null);
    }
  };

  const visibleOrders = useMemo(
    () =>
      orders
        .filter((order) => statusFilter === "all" || order.status === statusFilter)
        .filter((order) => stationFilter === "Tümü" || stationFor(order.status) === stationFilter)
        .sort(
          (left, right) =>
            new Date(left.createdAtUtc ?? left.statusChangedAtUtc).getTime() -
            new Date(right.createdAtUtc ?? right.statusChangedAtUtc).getTime(),
        ),
    [orders, stationFilter, statusFilter],
  );

  if (booting) {
    return (
      <main className="center-state" aria-live="polite">
        <span className="brand-mark">R</span>
        <p>Güvenli oturum geri yükleniyor…</p>
      </main>
    );
  }

  if (!session) return <LoginScreen onLogin={login} error={error} />;

  return (
    <main className="operations">
      <header className="topbar">
        <div className="identity">
          <span className="brand-mark brand-mark--small">R</span>
          <div>
            <p className="eyebrow">RESTAURANT OS</p>
            <h1>Operasyon Merkezi</h1>
          </div>
        </div>
        <div className="topbar__metrics" aria-label="Sipariş özeti">
          <Metric
            value={orders.filter((order) => order.status === "submitted").length}
            label="YENİ"
          />
          <Metric
            value={orders.filter((order) => order.status === "ready").length}
            label="PASA ÇIKTI"
          />
          <span className={`connection connection--${realtimeState}`}>{realtimeState}</span>
          <nav className="view-switch" aria-label="Ekranlar">
            <button
              className={view === "orders" ? "filter active" : "filter"}
              onClick={() => setView("orders")}
            >
              Siparişler
            </button>
            <button
              className={view === "tables" ? "filter active" : "filter"}
              onClick={() => setView("tables")}
            >
              Masalar
            </button>
            <button
              className={view === "menus" ? "filter active" : "filter"}
              onClick={() => setView("menus")}
            >
              Menü
            </button>
          </nav>
          <Button variant="ghost" onClick={logout}>
            Çıkış
          </Button>
        </div>
      </header>

      {error ? (
        <div className="notice" role="alert">
          {error}
          <button onClick={() => setError("")} aria-label="Bildirimi kapat">
            ×
          </button>
        </div>
      ) : null}

      {view === "tables" ? (
        <TablesPanel managementApi={managementApi} onError={setError} />
      ) : view === "menus" ? (
        <MenuPanel managementApi={managementApi} onError={setError} />
      ) : (
        <>
      <section className="filters" aria-label="Sipariş filtreleri">
        <div className="filter-group">
          {["Tümü", "Giriş", "Mutfak", "Pas"].map((station) => (
            <button
              key={station}
              className={stationFilter === station ? "filter active" : "filter"}
              onClick={() => setStationFilter(station)}
            >
              {station}
            </button>
          ))}
        </div>
        <label>
          Durum
          <select
            value={statusFilter}
            onChange={(event) => setStatusFilter(event.target.value as "all" | OrderStatus)}
          >
            <option value="all">Tüm aktif durumlar</option>
            {(["submitted", "accepted", "preparing", "ready"] as OrderStatus[]).map((status) => (
              <option key={status} value={status}>
                {statusLabel[status]}
              </option>
            ))}
          </select>
        </label>
        <Button variant="secondary" onClick={loadOrders} disabled={loadingOrders}>
          {loadingOrders ? "Yenileniyor…" : "REST eşitle"}
        </Button>
      </section>

      <section className="order-grid" aria-live="polite" aria-busy={loadingOrders}>
        {visibleOrders.map((order) => (
          <OrderCard
            key={order.id}
            order={order}
            now={now}
            pending={pendingOrder === order.id}
            onStatus={changeStatus}
          />
        ))}
        {!loadingOrders && visibleOrders.length === 0 ? (
          <div className="all-clear">
            <span>✓</span>
            <h2>İSTASYON TEMİZ</h2>
            <p>Bu filtrede aktif sipariş yok.</p>
          </div>
        ) : null}
      </section>
        </>
      )}
    </main>
  );
}

function LoginScreen({
  onLogin,
  error,
}: {
  onLogin: (input: LoginInput) => Promise<void>;
  error: string;
}) {
  const [submitting, setSubmitting] = useState(false);
  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setSubmitting(true);
    const data = new FormData(event.currentTarget);
    await onLogin({
      email: String(data.get("email")),
      password: String(data.get("password")),
      tenantId: String(data.get("tenantId")),
      branchId: String(data.get("branchId")),
    });
    setSubmitting(false);
  };

  return (
    <main className="login-shell">
      <section className="login-copy">
        <p className="eyebrow">CANLI RESTORAN OPERASYONU</p>
        <h1>Servisin ritmini tek ekrandan yönetin.</h1>
        <p>Yeni siparişten pasa, pastan servise; yetkiniz ve şubeniz sınırları içinde.</p>
      </section>
      <form className="login-card" onSubmit={submit}>
        <span className="brand-mark">R</span>
        <div>
          <p className="eyebrow">YÖNETİM GİRİŞİ</p>
          <h2>Vardiyaya bağlan</h2>
        </div>
        <label>
          E-posta
          <input name="email" type="email" autoComplete="username" required />
        </label>
        <label>
          Parola
          <input name="password" type="password" autoComplete="current-password" required />
        </label>
        <label>
          Tenant kimliği
          <input name="tenantId" inputMode="text" required />
        </label>
        <label>
          Şube kimliği
          <input name="branchId" inputMode="text" required />
        </label>
        {error ? (
          <p className="form-error" role="alert">
            {error}
          </p>
        ) : null}
        <Button type="submit" fullWidth disabled={submitting}>
          {submitting ? "Bağlanıyor…" : "Operasyon ekranını aç"}
        </Button>
        <p className="security-note">
          Erişim anahtarı yalnızca bellekte tutulur. Kalıcı oturum HttpOnly yenileme çereziyle
          sağlanır.
        </p>
      </form>
    </main>
  );
}

function Metric({ value, label }: { value: number; label: string }) {
  return (
    <span className="metric">
      <strong>{value}</strong>
      {label}
    </span>
  );
}

function OrderCard({
  order,
  now,
  pending,
  onStatus,
}: {
  order: Order;
  now: number;
  pending: boolean;
  onStatus: (order: Order, status: OrderStatus) => void;
}) {
  const ageMinutes = Math.max(
    0,
    Math.floor((now - new Date(order.createdAtUtc ?? order.statusChangedAtUtc).getTime()) / 60_000),
  );
  const late = new Date(order.estimatedReadyAtUtc).getTime() < now && order.status !== "ready";
  return (
    <article className={`order-card order-card--${order.status}${late ? " is-late" : ""}`}>
      <header>
        <div>
          <span className="status">{late ? "GECİKTİ" : statusLabel[order.status]}</span>
          <h2>{order.displayNumber}</h2>
        </div>
        <time>{ageMinutes} dk</time>
      </header>
      <dl>
        <div>
          <dt>İSTASYON</dt>
          <dd>{stationFor(order.status)}</dd>
        </div>
        <div>
          <dt>DURUM DEĞİŞİMİ</dt>
          <dd>
            {new Date(order.statusChangedAtUtc).toLocaleTimeString("tr-TR", {
              hour: "2-digit",
              minute: "2-digit",
            })}
          </dd>
        </div>
        <div>
          <dt>HEDEF</dt>
          <dd>
            {new Date(order.estimatedReadyAtUtc).toLocaleTimeString("tr-TR", {
              hour: "2-digit",
              minute: "2-digit",
            })}
          </dd>
        </div>
      </dl>
      <footer>
        {nextStatuses[order.status].map((next) => (
          <Button
            key={next}
            variant={next === "cancelled" ? "ghost" : "primary"}
            disabled={pending}
            onClick={() => onStatus(order, next)}
          >
            {pending ? "İşleniyor…" : actionLabel[next]}
          </Button>
        ))}
      </footer>
    </article>
  );
}

function TablesPanel({
  managementApi,
  onError,
}: {
  managementApi: ManagementApi;
  onError: (message: string) => void;
}) {
  const [tables, setTables] = useState<DiningTable[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [codes, setCodes] = useState<TableQrCode[]>([]);
  const [print, setPrint] = useState<GeneratedQr | QrPrint | null>(null);
  const [label, setLabel] = useState("");
  const [busy, setBusy] = useState(false);

  const loadTables = useCallback(async () => {
    try {
      const next = await managementApi.listTables();
      setTables(next);
      onError("");
    } catch (loadError) {
      onError(errorMessage(loadError));
    }
  }, [managementApi, onError]);

  useEffect(() => {
    void loadTables();
  }, [loadTables]);

  const selectTable = async (tableId: string) => {
    setSelectedId(tableId);
    setPrint(null);
    try {
      setCodes(await managementApi.listQrCodes(tableId));
      onError("");
    } catch (loadError) {
      onError(errorMessage(loadError));
    }
  };

  const createTable = async () => {
    setBusy(true);
    try {
      const created = await managementApi.createTable(label);
      setLabel("");
      await loadTables();
      await selectTable(created.id);
    } catch (createError) {
      onError(errorMessage(createError));
    } finally {
      setBusy(false);
    }
  };

  const generate = async (tableId: string) => {
    setBusy(true);
    try {
      const generated = await managementApi.generateQr(tableId);
      setPrint(generated);
      await loadTables();
      setCodes(await managementApi.listQrCodes(tableId));
      onError("");
    } catch (generateError) {
      onError(errorMessage(generateError));
    } finally {
      setBusy(false);
    }
  };

  const changeStatus = async (qrCodeId: string, action: "activate" | "deactivate" | "revoke") => {
    if (!selectedId) return;
    setBusy(true);
    try {
      await managementApi.changeQrStatus(qrCodeId, action);
      setCodes(await managementApi.listQrCodes(selectedId));
      await loadTables();
      if (action === "revoke") setPrint(null);
      onError("");
    } catch (statusError) {
      onError(errorMessage(statusError));
    } finally {
      setBusy(false);
    }
  };

  const reprint = async (qrCodeId: string) => {
    setBusy(true);
    try {
      setPrint(await managementApi.printQr(qrCodeId));
      onError("");
    } catch (printError) {
      onError(errorMessage(printError));
    } finally {
      setBusy(false);
    }
  };

  return (
    <section className="tables-panel" aria-label="Masa ve QR yönetimi">
      <header className="tables-panel__header">
        <div>
          <p className="eyebrow">KALICI MASA GEÇİDİ</p>
          <h2>Masalar ve QR kodları</h2>
        </div>
        <form
          className="table-create"
          onSubmit={(event) => {
            event.preventDefault();
            void createTable();
          }}
        >
          <label>
            Yeni masa etiketi
            <input
              value={label}
              onChange={(event) => setLabel(event.target.value)}
              required
              maxLength={80}
            />
          </label>
          <Button type="submit" disabled={busy}>
            Masa oluştur
          </Button>
        </form>
      </header>
      <div className="tables-layout">
        <ul className="table-list">
          {tables.map((table) => (
            <li key={table.id}>
              <button
                className={selectedId === table.id ? "table-item is-selected" : "table-item"}
                onClick={() => void selectTable(table.id)}
              >
                <strong>{table.label}</strong>
                <span>
                  {table.isActive ? "Aktif" : "Kapalı"} · {table.activeQrCount} QR
                </span>
              </button>
            </li>
          ))}
        </ul>
        <div className="qr-board">
          {selectedId ? (
            <>
              <div className="qr-board__actions">
                <Button onClick={() => void generate(selectedId)} disabled={busy}>
                  Yeni QR üret
                </Button>
              </div>
              {print ? (
                <article className="qr-print" aria-label="Yazdırılacak QR">
                  <p className="qr-print__sub">Masaya özel sipariş QR kodu</p>
                  <h2 className="qr-print__table">{print.tableLabel || "Masa"}</h2>
                  <div
                    className="qr-print__svg"
                    dangerouslySetInnerHTML={{ __html: print.svgMarkup }}
                  />
                  <p className="qr-print__url">{print.entryUrl}</p>
                  {"token" in print ? (
                    <p className="security-note">
                      Ham token yalnızca bu üretim yanıtında gösterilir; listelerde saklanmaz.
                    </p>
                  ) : null}
                </article>
              ) : null}
              <ul className="qr-list">
                {codes.map((code) => (
                  <li key={code.id} className={`qr-row qr-row--${code.status}`}>
                    <div>
                      <strong>{code.status}</strong>
                      <time>{new Date(code.createdAtUtc).toLocaleString("tr-TR")}</time>
                    </div>
                    <div className="qr-row__actions">
                      {code.status === "inactive" ? (
                        <Button variant="secondary" onClick={() => void changeStatus(code.id, "activate")}>
                          Aktifleştir
                        </Button>
                      ) : null}
                      {code.status === "active" ? (
                        <Button variant="secondary" onClick={() => void changeStatus(code.id, "deactivate")}>
                          Pasifleştir
                        </Button>
                      ) : null}
                      {code.status !== "revoked" ? (
                        <>
                          <Button variant="ghost" onClick={() => void reprint(code.id)}>
                            Yazdır
                          </Button>
                          <Button variant="ghost" onClick={() => void changeStatus(code.id, "revoke")}>
                            İptal et
                          </Button>
                        </>
                      ) : null}
                    </div>
                  </li>
                ))}
              </ul>
            </>
          ) : (
            <p className="muted">QR üretmek için bir masa seçin.</p>
          )}
        </div>
      </div>
    </section>
  );
}

function MenuPanel({
  managementApi,
  onError,
}: {
  managementApi: ManagementApi;
  onError: (message: string) => void;
}) {
  const [menus, setMenus] = useState<MenuSummary[]>([]);
  const [detail, setDetail] = useState<MenuDetail | null>(null);
  const [name, setName] = useState("");
  const [categoryName, setCategoryName] = useState("");
  const [productName, setProductName] = useState("");
  const [price, setPrice] = useState("120");
  const [busy, setBusy] = useState(false);

  const loadMenus = useCallback(async () => {
    try {
      setMenus(await managementApi.listMenus());
      onError("");
    } catch (loadError) {
      onError(errorMessage(loadError));
    }
  }, [managementApi, onError]);

  useEffect(() => {
    void loadMenus();
  }, [loadMenus]);

  const openMenu = async (menuId: string) => {
    try {
      setDetail(await managementApi.getMenu(menuId));
      onError("");
    } catch (loadError) {
      onError(errorMessage(loadError));
    }
  };

  const create = async () => {
    setBusy(true);
    try {
      const created = await managementApi.createMenu(name);
      setName("");
      await loadMenus();
      await openMenu(created.id);
    } catch (createError) {
      onError(errorMessage(createError));
    } finally {
      setBusy(false);
    }
  };

  const addCategory = async () => {
    if (!detail) return;
    setBusy(true);
    try {
      await managementApi.addCategory(detail.id, categoryName, detail.categories.length + 1);
      setCategoryName("");
      await openMenu(detail.id);
      await loadMenus();
    } catch (createError) {
      onError(errorMessage(createError));
    } finally {
      setBusy(false);
    }
  };

  const addProduct = async () => {
    if (!detail?.categories[0]) return;
    setBusy(true);
    try {
      const amountMinor = Math.round(Number.parseFloat(price.replace(",", ".")) * 100);
      await managementApi.addItem(detail.id, {
        categoryId: detail.categories[0].id,
        name: productName,
        description: "",
        amountMinor,
        isAvailable: true,
        sortOrder: detail.items.length + 1,
      });
      setProductName("");
      await openMenu(detail.id);
      await loadMenus();
    } catch (createError) {
      onError(errorMessage(createError));
    } finally {
      setBusy(false);
    }
  };

  const lifecycle = async (action: "publish" | "unpublish" | "archive") => {
    if (!detail) return;
    setBusy(true);
    try {
      if (action === "publish") await managementApi.publishMenu(detail.id);
      if (action === "unpublish") await managementApi.unpublishMenu(detail.id);
      if (action === "archive") await managementApi.archiveMenu(detail.id);
      await openMenu(detail.id);
      await loadMenus();
    } catch (statusError) {
      onError(errorMessage(statusError));
    } finally {
      setBusy(false);
    }
  };

  return (
    <section className="tables-panel" aria-label="Menü yönetimi">
      <header className="tables-panel__header">
        <div>
          <p className="eyebrow">YAYINLANAN MÜŞTERİ MENÜSÜ</p>
          <h2>Menü taslağı ve yayın</h2>
        </div>
        <form
          className="table-create"
          onSubmit={(event) => {
            event.preventDefault();
            void create();
          }}
        >
          <label>
            Yeni menü adı
            <input value={name} onChange={(event) => setName(event.target.value)} required maxLength={160} />
          </label>
          <Button type="submit" disabled={busy}>
            Taslak oluştur
          </Button>
        </form>
      </header>
      <div className="tables-layout">
        <ul className="table-list">
          {menus.map((menu) => (
            <li key={menu.id}>
              <button
                className={detail?.id === menu.id ? "table-item is-selected" : "table-item"}
                onClick={() => void openMenu(menu.id)}
              >
                <strong>{menu.name}</strong>
                <span>
                  {menu.lifecycle} · {menu.itemCount} ürün
                </span>
              </button>
            </li>
          ))}
        </ul>
        <div className="qr-board">
          {detail ? (
            <>
              <div className="qr-board__actions">
                {detail.lifecycle !== "archived" ? (
                  <Button onClick={() => void lifecycle("publish")} disabled={busy}>
                    Yayınla
                  </Button>
                ) : null}
                {detail.lifecycle === "published" ? (
                  <Button variant="secondary" onClick={() => void lifecycle("unpublish")} disabled={busy}>
                    Yayından al
                  </Button>
                ) : null}
                {detail.lifecycle !== "archived" ? (
                  <Button variant="ghost" onClick={() => void lifecycle("archive")} disabled={busy}>
                    Arşivle
                  </Button>
                ) : null}
              </div>
              {detail.lifecycle !== "archived" ? (
                <div className="table-create">
                  <label>
                    Kategori
                    <input value={categoryName} onChange={(event) => setCategoryName(event.target.value)} />
                  </label>
                  <Button variant="secondary" onClick={() => void addCategory()} disabled={busy || !categoryName}>
                    Kategori ekle
                  </Button>
                  <label>
                    Ürün
                    <input value={productName} onChange={(event) => setProductName(event.target.value)} />
                  </label>
                  <label>
                    Fiyat (₺)
                    <input value={price} onChange={(event) => setPrice(event.target.value)} inputMode="decimal" />
                  </label>
                  <Button
                    variant="secondary"
                    onClick={() => void addProduct()}
                    disabled={busy || !productName || detail.categories.length === 0}
                  >
                    Ürün ekle
                  </Button>
                </div>
              ) : (
                <p className="muted">Arşivlenmiş menü değiştirilemez.</p>
              )}
              <ul className="qr-list">
                {detail.items.map((item) => (
                  <li key={item.id} className="qr-row">
                    <div>
                      <strong>{item.name}</strong>
                      <span className="muted">
                        {(item.amountMinor / 100).toLocaleString("tr-TR", {
                          style: "currency",
                          currency: item.currency,
                        })}{" "}
                        · {item.isAvailable ? "Satışta" : "Kapalı"}
                      </span>
                    </div>
                    {detail.lifecycle !== "archived" ? (
                      <EnglishNameEditor
                        item={item}
                        disabled={busy}
                        onSave={async (englishName) => {
                          try {
                            await managementApi.upsertItemTranslation(
                              item.id,
                              "en",
                              englishName,
                              item.translations.find((translation) => translation.locale === "en")
                                ?.description ?? "",
                            );
                            await openMenu(detail.id);
                          } catch (saveError) {
                            onError(errorMessage(saveError));
                          }
                        }}
                        onToggle={() =>
                          void managementApi
                            .updateItemAvailability(item.id, !item.isAvailable)
                            .then(() => openMenu(detail.id))
                            .catch((availabilityError: unknown) => onError(errorMessage(availabilityError)))
                        }
                      />
                    ) : null}
                  </li>
                ))}
              </ul>
            </>
          ) : (
            <p className="muted">Düzenlemek için bir menü seçin.</p>
          )}
        </div>
      </div>
    </section>
  );
}

function EnglishNameEditor({
  item,
  disabled,
  onSave,
  onToggle,
}: {
  item: MenuProductRecord;
  disabled: boolean;
  onSave: (name: string) => Promise<void>;
  onToggle: () => void;
}) {
  const current = item.translations.find((translation) => translation.locale === "en")?.name ?? "";
  const [englishName, setEnglishName] = useState(current);
  useEffect(() => {
    setEnglishName(current);
  }, [current]);
  return (
    <div className="qr-row__actions">
      <label>
        EN
        <input
          value={englishName}
          onChange={(event) => setEnglishName(event.target.value)}
          disabled={disabled}
        />
      </label>
      <Button variant="secondary" disabled={disabled || !englishName} onClick={() => void onSave(englishName)}>
        EN kaydet
      </Button>
      <Button variant="ghost" onClick={onToggle}>
        {item.isAvailable ? "Durdur" : "Aç"}
      </Button>
    </div>
  );
}
