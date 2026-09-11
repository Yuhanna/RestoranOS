import { FormEvent, useCallback, useEffect, useMemo, useRef, useState, type CSSProperties } from "react";
import { Button } from "@restaurant-os/design-system";
import { AnalyticsPanel } from "./AnalyticsPanel";
import { DashboardPanel } from "./DashboardPanel";
import { PromotionsPanel } from "./PromotionsPanel";
import { SettingsPanel } from "./SettingsPanel";
import { ApiError, api as defaultApi, type ManagementApi } from "./api";
import {
  nextStatuses,
  type AudienceNotification,
  type DiningTable,
  type GeneratedQr,
  type LoginInput,
  type MenuDetail,
  type MenuProductRecord,
  type MenuSummary,
  type Order,
  type OrderDetail,
  type OrderStatus,
  type QrPrint,
  type ServiceRequest,
  type Session,
  type SubscriptionOffer,
  type TableQrCode,
  type Workspace,
} from "./domain";
import {
  bindBackgroundAlertRecovery,
  isSoundEnabled,
  notifyAudience,
  playAlertChime,
  requestNotificationPermission,
  setSoundEnabled,
} from "./notifications";
import { realtime as defaultRealtime, type RealtimeClient, type RealtimeState } from "./realtime";
import { ServiceRequestsPanel } from "./ServiceRequestsPanel";
import { ThemePicker } from "./ThemePicker";
import { formatOrderMoney, groupOrdersByTable, roundLabel } from "./tableOrders";
import { resolveManagementMediaUrl } from "./resolveManagementMediaUrl";

const managementApiBase = import.meta.env.VITE_MANAGEMENT_API_BASE_URL ?? "";

const statusLabel: Record<OrderStatus, string> = {
  submitted: "YENİ",
  accepted: "ONAYLANDI",
  preparing: "HAZIRLANIYOR",
  ready: "PASA ÇIKTI",
  served: "SERVİS EDİLDİ",
  completed: "HESAP KAPANDI",
  cancelled: "İPTAL",
};

const actionLabel: Record<OrderStatus, string> = {
  submitted: "Yeni",
  accepted: "Siparişe al",
  preparing: "Hazırlamaya başla",
  ready: "Pasa çıkar",
  served: "Servis edildi",
  completed: "Hesap kapandı",
  cancelled: "İptal",
};

const stationFor = (status: OrderStatus) =>
  status === "submitted" ? "Giriş" : status === "ready" || status === "served" ? "Pas" : "Mutfak";

const tableStatusLabel: Record<string, string> = {
  available: "MÜSAİT",
  occupied: "DOLU",
  has_pending_order: "BEKLEYEN SİPARİŞ",
};

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
    ? {
        ...current,
        ...incoming,
        createdAtUtc: incoming.createdAtUtc ?? current.createdAtUtc,
        tableId: incoming.tableId || current.tableId,
        tableLabel: incoming.tableLabel || current.tableLabel,
      }
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
  const [detailOrder, setDetailOrder] = useState<OrderDetail | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [realtimeState, setRealtimeState] = useState<RealtimeState>("offline");
  const [statusFilter, setStatusFilter] = useState<"all" | OrderStatus>("all");
  const [stationFilter, setStationFilter] = useState("Tümü");
  const [now, setNow] = useState(() => Date.now());
  const [view, setView] = useState<
    "dashboard" | "orders" | "tables" | "menus" | "promotions" | "service" | "analytics" | "settings"
  >("dashboard");
  const [soundOn, setSoundOn] = useState(isSoundEnabled);
  const [incomingServiceRequest, setIncomingServiceRequest] = useState<ServiceRequest | null>(null);
  const [workspaceMessages, setWorkspaceMessages] = useState<{
    notifications: AudienceNotification[];
    subscriptionOffers: SubscriptionOffer[];
  }>({ notifications: [], subscriptionOffers: [] });
  const [branding, setBranding] = useState<{
    logoUrl?: string;
    logoAlt?: string;
    showWatermark: boolean;
    intensity: "soft" | "medium";
  }>({ showWatermark: false, intensity: "soft" });
  const previousOrderIds = useRef<Set<string>>(new Set());
  const [recentOrderIds, setRecentOrderIds] = useState<string[]>([]);

  const loadOrders = useCallback(async () => {
    setLoadingOrders(true);
    try {
      const nextOrders = await managementApi.getActiveOrders();
      previousOrderIds.current = new Set(nextOrders.map((order) => order.id));
      setOrders(nextOrders);
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

  useEffect(() => bindBackgroundAlertRecovery(), []);

  useEffect(() => {
    if (!session) return;
    let stop: (() => Promise<void>) | undefined;
    let cancelled = false;
    realtimeClient
      .start({
        onOrder: (incoming) => {
          setOrders((current) => {
            const merged = mergeOrder(current, incoming);
            const isNew = incoming.status === "submitted" && !previousOrderIds.current.has(incoming.id);
            if (isNew) {
              playAlertChime("order");
              setRecentOrderIds((current) =>
                current.includes(incoming.id) ? current : [...current, incoming.id],
              );
            }
            previousOrderIds.current = new Set(merged.map((order) => order.id));
            return merged;
          });
        },
        onResync: (nextOrders) => {
          previousOrderIds.current = new Set(nextOrders.map((order) => order.id));
          setOrders(nextOrders);
        },
        onState: (state) => {
          setRealtimeState(state);
          if (state === "offline" && !managementApi.getSession()) setSession(null);
        },
        onServiceRequest: (request) => setIncomingServiceRequest(request),
        onAudienceNotification: (notification) => notifyAudience(notification),
        onLivePanelDenied: (payload) => {
          setError(
            payload.message ??
              "Canlı panel oturum limiti doldu. Diğer cihazı kapatın veya Pro’ya geçin.",
          );
          setRealtimeState("offline");
        },
      })
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

  useEffect(() => {
    if (recentOrderIds.length === 0) return;
    const timer = window.setTimeout(() => setRecentOrderIds([]), 45_000);
    return () => window.clearTimeout(timer);
  }, [recentOrderIds]);

  useEffect(() => {
    if (!session) {
      setWorkspaceMessages({ notifications: [], subscriptionOffers: [] });
      setBranding({ showWatermark: false, intensity: "soft" });
      return;
    }
    let active = true;
    managementApi
      .getWorkspace()
      .then((workspace) => {
        if (!active) return;
        setWorkspaceMessages({
          notifications: workspace.notifications ?? [],
          subscriptionOffers: workspace.subscriptionOffers ?? [],
        });
        const logoUrl = workspace.brandLogoUrl
          ? resolveManagementMediaUrl(workspace.brandLogoUrl, managementApiBase)
          : undefined;
        setBranding({
          logoUrl,
          logoAlt: workspace.brandLogoAlt ?? workspace.restaurantName,
          showWatermark: Boolean(workspace.showBrandWatermark && logoUrl),
          intensity: workspace.brandWatermarkIntensity === "medium" ? "medium" : "soft",
        });
      })
      .catch(() => {
        if (active) {
          setWorkspaceMessages({ notifications: [], subscriptionOffers: [] });
          setBranding({ showWatermark: false, intensity: "soft" });
        }
      });
    return () => {
      active = false;
    };
  }, [managementApi, session]);

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
      const latest = orders.find((item) => item.id === order.id) ?? order;
      const changed = await managementApi.changeStatus(latest, status);
      setOrders((current) => mergeOrder(current, changed));
      setError("");
    } catch (statusError) {
      const message = errorMessage(statusError);
      setError(message);
      if (statusError instanceof ApiError && statusError.status === 409) {
        await loadOrders();
        setError(message);
      }
      if (statusError instanceof ApiError && statusError.status === 401) setSession(null);
    } finally {
      setPendingOrder(null);
    }
  };

  const openOrderDetail = async (orderId: string) => {
    setDetailLoading(true);
    try {
      const detail = await managementApi.getOrder(orderId);
      setDetailOrder(detail);
      setError("");
    } catch (detailError) {
      setError(errorMessage(detailError));
      if (detailError instanceof ApiError && detailError.status === 401) setSession(null);
    } finally {
      setDetailLoading(false);
    }
  };

  const visibleTableGroups = useMemo(() => {
    const filtered = orders
      .filter((order) => statusFilter === "all" || order.status === statusFilter)
      .filter((order) => stationFilter === "Tümü" || stationFor(order.status) === stationFilter);
    return groupOrdersByTable(filtered, new Set(recentOrderIds));
  }, [orders, recentOrderIds, stationFilter, statusFilter]);

  if (booting) {
    return (
      <main className="center-state" aria-live="polite">
        <img className="brand-mark brand-mark--logo brand-mark--platform" src="/pasa-mark-light.svg" alt="Pasa" />
        <p>Güvenli oturum geri yükleniyor…</p>
      </main>
    );
  }

  if (!session) return <LoginScreen onLogin={login} error={error} />;

  return (
    <main
      className="operations"
      data-brand-watermark={branding.showWatermark ? branding.intensity : "off"}
      style={
        branding.showWatermark && branding.logoUrl
          ? ({ ["--brand-watermark-url" as string]: `url("${branding.logoUrl}")` } as CSSProperties)
          : undefined
      }
    >
      <header className="topbar">
        <div className="identity">
          {branding.logoUrl ? (
            <img
              className="brand-mark brand-mark--small brand-mark--logo"
              src={branding.logoUrl}
              alt={branding.logoAlt || ""}
            />
          ) : (
            <img
              className="brand-mark brand-mark--small brand-mark--logo brand-mark--platform"
              src="/pasa-mark-light.svg"
              alt="Pasa"
            />
          )}
          <div>
            <p className="eyebrow">PASA</p>
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
              className={view === "dashboard" ? "filter active" : "filter"}
              onClick={() => setView("dashboard")}
            >
              Gösterge
            </button>
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
            <button
              className={view === "promotions" ? "filter active" : "filter"}
              onClick={() => setView("promotions")}
            >
              İndirimler
            </button>
            <button
              className={view === "service" ? "filter active" : "filter"}
              onClick={() => setView("service")}
            >
              Garson
            </button>
            <button
              className={view === "analytics" ? "filter active" : "filter"}
              onClick={() => setView("analytics")}
            >
              İstatistik
            </button>
            <button
              className={view === "settings" ? "filter active" : "filter"}
              onClick={() => setView("settings")}
            >
              Ayarlar
            </button>
          </nav>
          <button
            type="button"
            className={soundOn ? "filter active" : "filter"}
            onClick={() => {
              const next = !soundOn;
              setSoundOn(next);
              setSoundEnabled(next);
              if (next) {
                void requestNotificationPermission();
                playAlertChime("order", { force: true });
              }
            }}
          >
            Ses {soundOn ? "açık" : "kapalı"}
          </button>
          <ThemePicker />
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

      {workspaceMessages.subscriptionOffers.length || workspaceMessages.notifications.length ? (
        <section className="audience-banners" aria-label="Hesap bildirimleri">
          {workspaceMessages.subscriptionOffers.map((offer) => (
            <article className="audience-banner audience-banner--offer" key={offer.id}>
              <strong>{offer.title}</strong>
              <p>
                {offer.body} ({offer.discountPercent}% · {offer.durationMonths} ay)
              </p>
            </article>
          ))}
          {workspaceMessages.notifications.map((notification) => (
            <article className="audience-banner" key={notification.id}>
              <strong>{notification.title}</strong>
              <p>{notification.body}</p>
            </article>
          ))}
        </section>
      ) : null}

      {view === "dashboard" ? (
        <DashboardPanel managementApi={managementApi} onError={setError} />
      ) : view === "tables" ? (
        <TablesPanel managementApi={managementApi} onError={setError} />
      ) : view === "menus" ? (
        <MenuPanel managementApi={managementApi} onError={setError} />
      ) : view === "promotions" ? (
        <PromotionsPanel managementApi={managementApi} onError={setError} />
      ) : view === "service" ? (
        <ServiceRequestsPanel
          managementApi={managementApi}
          onError={setError}
          incoming={incomingServiceRequest}
        />
      ) : view === "analytics" ? (
        <AnalyticsPanel managementApi={managementApi} onError={setError} />
      ) : view === "settings" ? (
        <SettingsPanel managementApi={managementApi} onError={setError} onLogout={logout} />
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

      <section className="order-board" aria-live="polite" aria-busy={loadingOrders}>
        {visibleTableGroups.map((group) => (
          <section
            key={group.tableId}
            className={`table-order-group${group.roundCount > 1 ? " table-order-group--multi" : ""}${
              group.hasSubmittedRound ? " table-order-group--attention" : ""
            }`}
            aria-label={`${group.tableLabel}, ${group.roundCount} sipariş`}
          >
            <header className="table-order-group__head">
              <div>
                <p className="eyebrow">MASA</p>
                <h2>{group.tableLabel}</h2>
                <p className="muted">
                  {group.roundCount > 1
                    ? `${group.roundCount} tur · son turu kaçırmayın`
                    : "Tek aktif sipariş"}
                </p>
              </div>
              <div className="table-order-group__totals">
                {group.roundCount > 1 ? (
                  <span className="table-order-group__badge">Çoklu sipariş</span>
                ) : null}
                {group.hasSubmittedRound ? (
                  <span className="table-order-group__badge table-order-group__badge--new">
                    Yeni tur
                  </span>
                ) : null}
                <strong>{formatOrderMoney(group.totalMinor, group.currency)}</strong>
                <span className="muted">masa toplamı</span>
              </div>
            </header>
            <div className="table-order-group__grid">
              {group.orders.map((order, index) => (
                <OrderCard
                  key={order.id}
                  order={order}
                  roundTitle={roundLabel(index, group.roundCount)}
                  isNewRound={recentOrderIds.includes(order.id) || order.status === "submitted"}
                  now={now}
                  pending={pendingOrder === order.id}
                  onStatus={changeStatus}
                  onDetail={openOrderDetail}
                />
              ))}
            </div>
          </section>
        ))}
        {!loadingOrders && visibleTableGroups.length === 0 ? (
          <div className="all-clear">
            <span>✓</span>
            <h2>İSTASYON TEMİZ</h2>
            <p>Bu filtrede aktif sipariş yok.</p>
          </div>
        ) : null}
      </section>
      {detailOrder ? (
        <OrderDetailPanel
          detail={detailOrder}
          loading={detailLoading}
          onClose={() => setDetailOrder(null)}
        />
      ) : null}
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
        <p>Pasa — masadan mutfağa restoran işletim sistemi. Yeni siparişten pasa, pastan servise.</p>
      </section>
      <form className="login-card" onSubmit={submit}>
        <img className="brand-mark brand-mark--logo brand-mark--platform" src="/pasa-mark-light.svg" alt="Pasa" />
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
        <ThemePicker />
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
  roundTitle,
  isNewRound,
  now,
  pending,
  onStatus,
  onDetail,
}: {
  order: Order;
  roundTitle: string;
  isNewRound: boolean;
  now: number;
  pending: boolean;
  onStatus: (order: Order, status: OrderStatus) => void;
  onDetail: (orderId: string) => void;
}) {
  const ageMinutes = Math.max(
    0,
    Math.floor((now - new Date(order.createdAtUtc ?? order.statusChangedAtUtc).getTime()) / 60_000),
  );
  const late = new Date(order.estimatedReadyAtUtc).getTime() < now && order.status !== "ready";
  return (
    <article
      className={`order-card order-card--${order.status}${late ? " is-late" : ""}${
        isNewRound ? " is-new-round" : ""
      }`}
    >
      <header>
        <div>
          <span className="status">{late ? "GECİKTİ" : statusLabel[order.status]}</span>
          <p className="order-card__round">{roundTitle}</p>
          <h2>{order.displayNumber}</h2>
        </div>
        <time>{ageMinutes} dk</time>
      </header>
      <dl>
        <div>
          <dt>MASA</dt>
          <dd>{order.tableLabel || "—"}</dd>
        </div>
        <div>
          <dt>SİPARİŞ ZAMANI</dt>
          <dd>
            {new Date(order.createdAtUtc ?? order.statusChangedAtUtc).toLocaleTimeString("tr-TR", {
              hour: "2-digit",
              minute: "2-digit",
            })}
          </dd>
        </div>
        <div>
          <dt>TUTAR</dt>
          <dd>{formatOrderMoney(order.amountMinor, order.currency)}</dd>
        </div>
        <div>
          <dt>DURUM</dt>
          <dd>{statusLabel[order.status]}</dd>
        </div>
        <div>
          <dt>İSTASYON</dt>
          <dd>{stationFor(order.status)}</dd>
        </div>
      </dl>
      <footer>
        <Button variant="secondary" disabled={pending} onClick={() => onDetail(order.id)}>
          Detay
        </Button>
        {(nextStatuses[order.status] ?? []).map((next) => (
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

function OrderDetailPanel({
  detail,
  loading,
  onClose,
}: {
  detail: OrderDetail;
  loading: boolean;
  onClose: () => void;
}) {
  const formatMoney = (minor: number) =>
    `₺${(minor / 100).toLocaleString("tr-TR", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;

  return (
    <div className="order-detail-backdrop" role="presentation" onClick={onClose}>
      <section
        className="order-detail-panel"
        role="dialog"
        aria-modal="true"
        aria-labelledby="order-detail-title"
        onClick={(event) => event.stopPropagation()}
      >
        <header>
          <div>
            <p className="eyebrow">SİPARİŞ DETAYI</p>
            <h2 id="order-detail-title">{detail.displayNumber}</h2>
            <p className="muted">
              Masa {detail.tableLabel} · {statusLabel[detail.status]}
            </p>
          </div>
          <button type="button" className="order-detail-close" onClick={onClose} aria-label="Kapat">
            ×
          </button>
        </header>
        {loading ? <p className="muted">Yükleniyor…</p> : null}
        <dl className="order-detail-summary">
          <div>
            <dt>Ara toplam</dt>
            <dd>{formatMoney(detail.subtotalAmountMinor)}</dd>
          </div>
          {detail.discountAmountMinor > 0 ? (
            <div>
              <dt>İndirim</dt>
              <dd>−{formatMoney(detail.discountAmountMinor)}</dd>
            </div>
          ) : null}
          <div>
            <dt>Toplam</dt>
            <dd>{formatMoney(detail.totalAmountMinor)}</dd>
          </div>
        </dl>
        <section>
          <h3>Kalemler</h3>
          {detail.items?.length ? (
            <ul className="order-detail-lines">
              {detail.items.map((item) => (
                <li key={item.id}>
                  <strong>
                    {item.quantity}× {item.name}
                  </strong>
                  <span>{formatMoney(item.unitPriceAmountMinor * item.quantity)}</span>
                  {item.note ? <em>{item.note}</em> : null}
                </li>
              ))}
            </ul>
          ) : (
            <p className="muted">Kalem kaydı yok.</p>
          )}
        </section>
        <section>
          <h3>Durum geçmişi</h3>
          <ol className="order-detail-history">
            {(detail.statusHistory ?? []).map((entry, index) => (
              <li key={`${entry.status}-${entry.changedAtUtc}-${index}`}>
                <strong>{statusLabel[entry.status as OrderStatus] ?? entry.status}</strong>
                <time>{new Date(entry.changedAtUtc).toLocaleString("tr-TR")}</time>
              </li>
            ))}
          </ol>
        </section>
      </section>
    </div>
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
                  {tableStatusLabel[table.operationalStatus] ?? table.operationalStatus} ·{" "}
                  {table.isActive ? "Kayıt aktif" : "Kapalı"} · {table.activeQrCount} QR
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
                  <p className="security-note">
                    Bu bağlantıyı bilgisayarda tıklamayın; yalnızca telefon kamerasıyla QR okutun. Tıklamak masayı
                    DOLU gösterir.
                  </p>
                  {print.entryUrl.includes("localhost") || print.entryUrl.includes("127.0.0.1") ? (
                    <p className="security-note" role="alert">
                      Bu URL telefonunuzdan açılmaz. API yeniden başlatılıp yeni QR üretin; adres otomatik
                      olarak Wi‑Fi IP’nize ayarlanır.
                    </p>
                  ) : null}
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
