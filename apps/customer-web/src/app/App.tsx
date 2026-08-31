import { useEffect, useMemo, useRef, useState } from "react";
import { Button, Dialog } from "@restaurant-os/design-system";
import { getCustomerGateway } from "../data/customerGateway";
import { readQrFromLocation } from "../data/resolveCustomerApiBaseUrl";
import { DEMO_QR_TOKEN } from "../data/mockCustomerGateway";
import {
  CustomerGatewayError,
  type CartLine,
  type CustomerGateway,
  type CustomerSession,
  type Money,
  type Order,
  type Product,
} from "../domain/customer";
import { messages as t } from "../i18n/messages";
import { createClientId } from "../lib/createClientId";

type AppProps = {
  gateway?: CustomerGateway;
  qrToken?: string | null;
};

const formatMoney = (money: Money) =>
  new Intl.NumberFormat("tr-TR", {
    style: "currency",
    currency: money.currency,
    maximumFractionDigits: 0,
  }).format(money.amountMinor / 100);

function PriceDisplay({ product, emphasize = false }: { product: Product; emphasize?: boolean }) {
  const hasDiscount = (product.discount?.amountMinor ?? 0) > 0 && product.listPrice;
  if (!hasDiscount) {
    return <strong className={emphasize ? "price-final" : undefined}>{formatMoney(product.price)}</strong>;
  }

  return (
    <span className="price-stack">
      <span className="price-list">{formatMoney(product.listPrice!)}</span>
      <strong className="price-final">{formatMoney(product.price)}</strong>
    </span>
  );
}

/** .NET often emits 7-digit fractional seconds; older mobile browsers reject them. */
const formatClock = (iso: string) => {
  const normalized = iso.replace(/(\.\d{3})\d+/, "$1");
  const date = new Date(normalized);
  if (Number.isNaN(date.getTime())) return "—";
  return new Intl.DateTimeFormat("tr-TR", {
    hour: "2-digit",
    minute: "2-digit",
  }).format(date);
};

const lineTotal = (line: CartLine): Money => ({
  amountMinor: line.product.price.amountMinor * line.quantity,
  currency: "TRY",
});

const newLineKey = (product: Product, note: string) => `${product.id}:${note.trim()}`;

function ProductDialog({
  product,
  onClose,
  onAdd,
}: {
  product: Product;
  onClose: () => void;
  onAdd: (line: CartLine) => void;
}) {
  const [quantity, setQuantity] = useState(1);
  const [note, setNote] = useState("");
  const previewLine: CartLine = {
    key: newLineKey(product, note),
    product,
    quantity,
    selections: {},
    note,
  };

  return (
    <Dialog labelledBy="product-title" dismissLabel={t.close} onDismiss={onClose}>
      <div className="product-detail">
        <img src={product.imageUrl} alt={product.imageAlt} className="product-detail__image" />
        <Button className="dialog-close" variant="ghost" aria-label={t.close} onClick={onClose}>
          ×
        </Button>
        <div className="product-detail__content">
          <div className="product-detail__heading">
            <div>
              {product.badge ? <span className="badge badge--brand">{product.badge}</span> : null}
              <h2 id="product-title">{product.name}</h2>
            </div>
            <PriceDisplay product={product} emphasize />
          </div>
          <p>{product.description}</p>
          {product.allergens.length ? (
            <div className="allergens">
              <h3>{t.allergens}</h3>
              <div className="chip-row">
                {product.allergens.map((allergen) => (
                  <span className="chip" key={allergen}>
                    {allergen}
                  </span>
                ))}
              </div>
            </div>
          ) : null}
          <label className="note-field">
            <span>{t.productNote}</span>
            <textarea
              value={note}
              maxLength={160}
              placeholder={t.productNotePlaceholder}
              onChange={(event) => setNote(event.target.value)}
            />
          </label>
          <div className="product-detail__actions">
            <div className="stepper" aria-label={t.quantity}>
              <Button
                variant="ghost"
                aria-label={t.decrease}
                disabled={quantity === 1}
                onClick={() => setQuantity((current) => Math.max(1, current - 1))}
              >
                −
              </Button>
              <output aria-live="polite">{quantity}</output>
              <Button
                variant="ghost"
                aria-label={t.increase}
                onClick={() => setQuantity((current) => current + 1)}
              >
                +
              </Button>
            </div>
            <Button
              fullWidth
              onClick={() => {
                onAdd(previewLine);
                onClose();
              }}
            >
              <span>{t.addToCart}</span>
              <span>{formatMoney(lineTotal(previewLine))}</span>
            </Button>
          </div>
        </div>
      </div>
    </Dialog>
  );
}

function CartDialog({
  session,
  lines,
  submitting,
  submitError,
  onClose,
  onChangeQuantity,
  onRemove,
  onSubmit,
}: {
  session: CustomerSession;
  lines: CartLine[];
  submitting: boolean;
  submitError: boolean;
  onClose: () => void;
  onChangeQuantity: (key: string, quantity: number) => void;
  onRemove: (key: string) => void;
  onSubmit: () => void;
}) {
  const listSubtotal: Money = {
    amountMinor: lines.reduce((sum, line) => {
      const unitList = line.product.listPrice?.amountMinor ?? line.product.price.amountMinor;
      return sum + unitList * line.quantity;
    }, 0),
    currency: "TRY",
  };
  const total: Money = {
    amountMinor: lines.reduce((sum, line) => sum + lineTotal(line).amountMinor, 0),
    currency: "TRY",
  };
  const discountTotal: Money = {
    amountMinor: Math.max(0, listSubtotal.amountMinor - total.amountMinor),
    currency: "TRY",
  };
  const hasDiscount = discountTotal.amountMinor > 0;

  return (
    <Dialog
      labelledBy="cart-title"
      dismissLabel={t.close}
      onDismiss={onClose}
      footer={
        lines.length ? (
          <>
            {submitError ? (
              <p className="form-error" role="alert">
                {t.submitError}
              </p>
            ) : null}
            <Button fullWidth disabled={submitting || !navigator.onLine} onClick={onSubmit}>
              {submitting ? t.submitting : t.submitOrder}
            </Button>
          </>
        ) : null
      }
    >
      <div className="cart">
        <header className="dialog-heading">
          <div>
            <h2 id="cart-title">{t.cartTitle}</h2>
            <p>
              {session.branchName} · {session.tableLabel}
            </p>
          </div>
          <Button variant="ghost" aria-label={t.close} onClick={onClose}>
            ×
          </Button>
        </header>
        {lines.length ? (
          <>
            <ul className="cart-lines">
              {lines.map((line) => (
                <li key={line.key}>
                  <img src={line.product.imageUrl} alt="" />
                  <div>
                    <h3>{line.product.name}</h3>
                    {line.note ? <p>{line.note}</p> : null}
                    <div className="stepper stepper--small">
                      <Button
                        variant="ghost"
                        aria-label={line.quantity === 1 ? t.remove : t.decrease}
                        onClick={() =>
                          line.quantity === 1
                            ? onRemove(line.key)
                            : onChangeQuantity(line.key, line.quantity - 1)
                        }
                      >
                        {line.quantity === 1 ? "×" : "−"}
                      </Button>
                      <output>{line.quantity}</output>
                      <Button
                        variant="ghost"
                        aria-label={t.increase}
                        onClick={() => onChangeQuantity(line.key, line.quantity + 1)}
                      >
                        +
                      </Button>
                    </div>
                  </div>
                  <strong>{formatMoney(lineTotal(line))}</strong>
                </li>
              ))}
            </ul>
            <div className="cart-total">
              <div className="cart-total__row">
                <span>{t.listPrice}</span>
                <strong>{formatMoney(listSubtotal)}</strong>
              </div>
              {hasDiscount ? (
                <div className="cart-total__row cart-total__row--discount">
                  <span>{t.discountSavings}</span>
                  <strong>-{formatMoney(discountTotal)}</strong>
                </div>
              ) : null}
              <div className="cart-total__row cart-total__row--due">
                <span>{hasDiscount ? t.amountDue : t.subtotal}</span>
                <strong>{formatMoney(total)}</strong>
              </div>
            </div>
            <p className="service-note">{t.serviceNote}</p>
          </>
        ) : (
          <p className="empty-message">{t.cartEmpty}</p>
        )}
      </div>
    </Dialog>
  );
}

function OrderTracking({ order, onBack }: { order: Order; onBack: () => void }) {
  const statuses = ["submitted", "accepted", "preparing", "ready", "completed"] as const;
  const currentIndex = statuses.findIndex((status) => status === order.status);

  return (
    <main className="tracking" id="main-content">
      <span className="tracking__mark" aria-hidden="true">
        ✓
      </span>
      <p className="eyebrow">{order.displayNumber}</p>
      <h1>{t.orderReceived}</h1>
      <p>{t.orderReceivedBody}</p>
      <div className="eta-card">
        <span>{t.estimatedReady}</span>
        <strong>{formatClock(order.estimatedReadyAt)}</strong>
      </div>
      <ol className="status-list">
        {order.status === "cancelled" ? (
          <li className="is-active">{t.statuses.cancelled}</li>
        ) : null}
        {statuses.map((status, index) => (
          <li className={index <= currentIndex ? "is-active" : ""} key={status}>
            <span aria-hidden="true" />
            {t.statuses[status]}
          </li>
        ))}
      </ol>
      <Button variant="secondary" onClick={onBack}>
        {t.backToMenu}
      </Button>
    </main>
  );
}

const activeOrderStorageKey = (qrToken: string) =>
  `restaurant-os.customer.active-order:qr:${normalizeQrToken(qrToken)}`;

type StoredActiveOrder = {
  orderId: string;
  /** Session that owns the order (needed after QR re-resolve creates a new session). */
  sessionToken: string;
};

function normalizeQrToken(qrToken: string) {
  try {
    return decodeURIComponent(qrToken.trim());
  } catch {
    return qrToken.trim();
  }
}

function readStoredActiveOrder(qrToken: string): StoredActiveOrder | null {
  try {
    const raw = localStorage.getItem(activeOrderStorageKey(qrToken));
    if (!raw) return null;
    const parsed = JSON.parse(raw) as Partial<StoredActiveOrder>;
    if (!parsed.orderId?.trim() || !parsed.sessionToken?.trim()) return null;
    return { orderId: parsed.orderId.trim(), sessionToken: parsed.sessionToken.trim() };
  } catch {
    return null;
  }
}

function writeStoredActiveOrder(qrToken: string, orderId: string, sessionToken: string) {
  localStorage.setItem(
    activeOrderStorageKey(qrToken),
    JSON.stringify({ orderId, sessionToken } satisfies StoredActiveOrder),
  );
}

function clearStoredActiveOrder(qrToken: string) {
  localStorage.removeItem(activeOrderStorageKey(qrToken));
}

function isActiveOrderStatus(status: Order["status"]) {
  return status !== "completed" && status !== "cancelled";
}

function Menu({
  session,
  gateway,
  qrToken,
}: {
  session: CustomerSession;
  gateway: CustomerGateway;
  qrToken: string;
}) {
  const [category, setCategory] = useState("all");
  const [query, setQuery] = useState("");
  const [selectedProduct, setSelectedProduct] = useState<Product | null>(null);
  const [lines, setLines] = useState<CartLine[]>([]);
  const [cartOpen, setCartOpen] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState(false);
  const [order, setOrder] = useState<Order | null>(null);
  const [orderSessionToken, setOrderSessionToken] = useState(session.sessionToken);
  const [trackingOpen, setTrackingOpen] = useState(false);
  const [waiterBusy, setWaiterBusy] = useState(false);
  const [waiterMessage, setWaiterMessage] = useState<string | null>(null);
  const [openServiceTypes, setOpenServiceTypes] = useState<ReadonlySet<string>>(
    () => new Set(session.openServiceRequestTypes ?? []),
  );
  const idempotencyKey = useRef(createClientId());

  const callStaff = async (type: "waiter" | "bill") => {
    if (openServiceTypes.has(type)) {
      setWaiterMessage(type === "bill" ? t.callWaiterBillOpen : t.callWaiterOpen);
      return;
    }

    setWaiterBusy(true);
    setWaiterMessage(null);
    try {
      await gateway.createServiceRequest(session.sessionToken, type);
      setOpenServiceTypes((prev) => new Set([...prev, type]));
      setWaiterMessage(type === "bill" ? t.callWaiterBillSent : t.callWaiterSent);
    } catch (error) {
      if (error instanceof CustomerGatewayError && error.code === "SERVICE_REQUEST_OPEN") {
        setOpenServiceTypes((prev) => new Set([...prev, type]));
        setWaiterMessage(type === "bill" ? t.callWaiterBillOpen : t.callWaiterOpen);
      } else {
        setWaiterMessage(t.callWaiterError);
      }
    } finally {
      setWaiterBusy(false);
    }
  };

  useEffect(() => {
    const controller = new AbortController();
    let cancelled = false;

    const restore = async () => {
      const stored = readStoredActiveOrder(qrToken);
      const fromSession = (session.activeOrders ?? []).filter((item) =>
        isActiveOrderStatus(item.status),
      );
      const candidateIds = [
        ...new Set(
          [stored?.orderId, ...fromSession.map((item) => item.id)].filter(
            (id): id is string => Boolean(id?.trim()),
          ),
        ),
      ];

      // Prefer server-provided order payloads first (no extra round-trip).
      const sessionMatch =
        (stored?.orderId
          ? fromSession.find((item) => item.id === stored.orderId)
          : undefined) ?? fromSession[0];
      if (sessionMatch) {
        if (cancelled) return;
        setOrder(sessionMatch);
        setOrderSessionToken(session.sessionToken);
        writeStoredActiveOrder(qrToken, sessionMatch.id, session.sessionToken);
        setTrackingOpen(false);
        return;
      }

      for (const orderId of candidateIds) {
        try {
          const latest = await gateway.getOrder(
            orderId,
            session.sessionToken,
            controller.signal,
          );
          if (cancelled) return;
          if (!isActiveOrderStatus(latest.status)) {
            if (stored?.orderId === orderId) clearStoredActiveOrder(qrToken);
            continue;
          }
          setOrder(latest);
          setOrderSessionToken(session.sessionToken);
          writeStoredActiveOrder(qrToken, latest.id, session.sessionToken);
          setTrackingOpen(false);
          return;
        } catch (error) {
          if (error instanceof DOMException && error.name === "AbortError") return;
        }
      }

      if (!cancelled && stored && candidateIds.length === 0) {
        clearStoredActiveOrder(qrToken);
      }
    };

    void restore();
    return () => {
      cancelled = true;
      controller.abort();
    };
  }, [gateway, qrToken, session.activeOrders, session.sessionToken]);

  useEffect(() => {
    if (!order) return;

    let disposed = false;
    let stop: (() => Promise<void>) | undefined;
    const controller = new AbortController();
    const resync = () =>
      gateway
        .getOrder(order.id, orderSessionToken, controller.signal)
        .then((latest) => {
          if (!disposed) {
            setOrder(latest);
            if (latest.status === "completed" || latest.status === "cancelled") {
              clearStoredActiveOrder(qrToken);
            }
          }
        })
        .catch(() => {});

    void resync();
    // SignalR may fail on some phones/LAN setups; polling keeps status moving.
    const pollId = window.setInterval(resync, 4000);
    void gateway
      .watchOrder(order.id, orderSessionToken, (latest) => {
        setOrder(latest);
        if (latest.status === "completed" || latest.status === "cancelled") {
          clearStoredActiveOrder(qrToken);
        }
      }, resync)
      .then(async (unsubscribe) => {
        if (disposed) await unsubscribe();
        else stop = unsubscribe;
      })
      .catch(() => {});

    return () => {
      disposed = true;
      window.clearInterval(pollId);
      controller.abort();
      if (stop) void stop();
    };
  }, [gateway, order?.id, orderSessionToken, qrToken]);

  const filteredProducts = useMemo(() => {
    const normalizedQuery = query.trim().toLocaleLowerCase("tr-TR");
    return session.products.filter(
      (product) =>
        (category === "all" || product.categoryId === category) &&
        (!normalizedQuery ||
          `${product.name} ${product.description}`
            .toLocaleLowerCase("tr-TR")
            .includes(normalizedQuery)),
    );
  }, [category, query, session.products]);
  const cartCount = lines.reduce((sum, line) => sum + line.quantity, 0);
  const cartTotal: Money = {
    amountMinor: lines.reduce((sum, line) => sum + lineTotal(line).amountMinor, 0),
    currency: "TRY",
  };

  const addLine = (incoming: CartLine) => {
    setLines((current) => {
      const existing = current.find(
        (line) => line.key === incoming.key && line.note === incoming.note,
      );
      return existing
        ? current.map((line) =>
            line === existing ? { ...line, quantity: line.quantity + incoming.quantity } : line,
          )
        : [...current, incoming];
    });
  };

  const submitOrder = async () => {
    setSubmitting(true);
    setSubmitError(false);
    try {
      const submitted = await gateway.submitOrder({
        sessionToken: session.sessionToken,
        idempotencyKey: idempotencyKey.current,
        lines: lines.map((line) => ({
          productId: line.product.id,
          quantity: line.quantity,
          modifierOptionIds: [],
          note: line.note || undefined,
        })),
      });
      writeStoredActiveOrder(qrToken, submitted.id, session.sessionToken);
      setOrder(submitted);
      setOrderSessionToken(session.sessionToken);
      setTrackingOpen(true);
      setCartOpen(false);
      setLines([]);
      idempotencyKey.current = createClientId();
    } catch {
      setSubmitError(true);
    } finally {
      setSubmitting(false);
    }
  };

  if (order && trackingOpen) {
    return <OrderTracking order={order} onBack={() => setTrackingOpen(false)} />;
  }

  return (
    <>
      <header className="site-header">
        <div className="restaurant-lockup">
          <span className="restaurant-mark" aria-hidden="true">
            {session.restaurantName?.slice(0, 1) ?? "R"}
          </span>
          <div>
            <strong>{session.restaurantName ?? "Restoran"}</strong>
            <span>
              {session.branchName} · {session.tableLabel}
            </span>
          </div>
        </div>
        <div className="header-actions">
          {order ? (
            <Button
              variant="secondary"
              className="header-track"
              aria-label={t.trackOrderAria(order.displayNumber)}
              onClick={() => setTrackingOpen(true)}
            >
              {t.trackOrder}
            </Button>
          ) : null}
          <Button
            variant="secondary"
            className="header-cart"
            aria-label={`${t.cartLabel}: ${t.itemCount(cartCount)}`}
            onClick={() => setCartOpen(true)}
          >
            <span aria-hidden="true">⌑</span>
            {cartCount ? <b>{cartCount}</b> : null}
          </Button>
        </div>
      </header>
      <main className="menu" id="main-content">
        <section className="menu-hero">
          <p className="eyebrow">{t.menuEyebrow}</p>
          <h1>{t.menuTitle}</h1>
          <div className="staff-actions">
            <Button
              variant="secondary"
              disabled={waiterBusy || openServiceTypes.has("waiter")}
              onClick={() => void callStaff("waiter")}
            >
              {waiterBusy
                ? t.callWaiterSending
                : openServiceTypes.has("waiter")
                  ? t.callWaiterOpen
                  : t.callWaiter}
            </Button>
            <Button
              variant="ghost"
              disabled={waiterBusy || openServiceTypes.has("bill")}
              onClick={() => void callStaff("bill")}
            >
              {openServiceTypes.has("bill") ? t.callWaiterBillOpen : t.callWaiterBill}
            </Button>
          </div>
          {waiterMessage ? (
            <p className="service-note" role="status">
              {waiterMessage}
            </p>
          ) : null}
          <label className="search-field">
            <span>{t.searchLabel}</span>
            <input
              type="search"
              value={query}
              placeholder={t.searchPlaceholder}
              onChange={(event) => setQuery(event.target.value)}
            />
          </label>
        </section>
        <nav className="categories" aria-label={t.menuEyebrow}>
          {session.categories.map((item) => (
            <Button
              variant={category === item.id ? "primary" : "secondary"}
              aria-pressed={category === item.id}
              key={item.id}
              onClick={() => setCategory(item.id)}
            >
              {item.name}
            </Button>
          ))}
        </nav>
        {!session.products.length ? (
          <section className="empty-state">
            <h2>{t.menuEmptyTitle}</h2>
            <p>{t.menuEmptyBody}</p>
          </section>
        ) : filteredProducts.length ? (
          <section className="product-grid" aria-live="polite">
            {filteredProducts.map((product) => (
              <article
                className={`product-card${product.available ? "" : " is-unavailable"}`}
                key={product.id}
              >
                <button
                  className="product-card__open"
                  disabled={!product.available}
                  aria-label={product.name}
                  onClick={() => setSelectedProduct(product)}
                >
                  <span className="product-card__media">
                    <img src={product.imageUrl} alt={product.imageAlt} loading="lazy" />
                    {product.badge ? (
                      <span className="badge badge--brand">{product.badge}</span>
                    ) : null}
                    {!product.available ? <span className="badge">{t.unavailable}</span> : null}
                  </span>
                  <span className="product-card__body">
                    <span className="chip-row">
                      {product.dietaryTags.map((tag) => (
                        <span className="badge badge--dietary" key={tag}>
                          {t.dietary[tag]}
                        </span>
                      ))}
                    </span>
                    <strong>{product.name}</strong>
                    <span>{product.description}</span>
                  </span>
                </button>
                <footer>
                  <PriceDisplay product={product} emphasize />
                  <Button
                    aria-label={`${product.name}: ${t.add}`}
                    disabled={!product.available}
                    onClick={() => setSelectedProduct(product)}
                  >
                    +
                  </Button>
                </footer>
              </article>
            ))}
          </section>
        ) : (
          <section className="empty-state">
            <h2>{t.emptyResultsTitle}</h2>
            <p>{t.emptyResultsBody}</p>
          </section>
        )}
      </main>
      {order && !cartCount ? (
        <div className="sticky-cart">
          <Button fullWidth onClick={() => setTrackingOpen(true)}>
            <span>{t.trackOrder}</span>
            <strong>{order.displayNumber}</strong>
          </Button>
        </div>
      ) : null}
      {cartCount ? (
        <div className="sticky-cart">
          <Button fullWidth onClick={() => setCartOpen(true)}>
            <span>
              <b>{cartCount}</b> {t.viewCart}
            </span>
            <strong>{formatMoney(cartTotal)}</strong>
          </Button>
        </div>
      ) : null}
      {selectedProduct ? (
        <ProductDialog
          product={selectedProduct}
          onClose={() => setSelectedProduct(null)}
          onAdd={addLine}
        />
      ) : null}
      {cartOpen ? (
        <CartDialog
          session={session}
          lines={lines}
          submitting={submitting}
          submitError={submitError}
          onClose={() => setCartOpen(false)}
          onChangeQuantity={(key, quantity) =>
            setLines((current) =>
              current.map((line) => (line.key === key ? { ...line, quantity } : line)),
            )
          }
          onRemove={(key) => setLines((current) => current.filter((line) => line.key !== key))}
          onSubmit={submitOrder}
        />
      ) : null}
    </>
  );
}

export function App({ gateway, qrToken }: AppProps) {
  const token = qrToken === undefined ? readQrFromLocation() : qrToken;
  const activeGateway = useMemo(
    () => gateway ?? getCustomerGateway(token),
    [gateway, token],
  );
  const [state, setState] = useState<
    | {
        status: "required" | "loading" | "invalid" | "menu-unavailable" | "error";
        detail?: string;
      }
    | { status: "ready"; session: CustomerSession }
  >({ status: token ? "loading" : "required" });
  const [online, setOnline] = useState(navigator.onLine);

  useEffect(() => {
    const updateOnline = () => setOnline(navigator.onLine);
    window.addEventListener("online", updateOnline);
    window.addEventListener("offline", updateOnline);
    return () => {
      window.removeEventListener("online", updateOnline);
      window.removeEventListener("offline", updateOnline);
    };
  }, []);

  useEffect(() => {
    if (!token) {
      setState({ status: "required" });
      return;
    }
    const controller = new AbortController();
    setState({ status: "loading" });
    activeGateway
      .resolveQr(
        token,
        controller.signal,
        new URLSearchParams(window.location.search).get("lang") === "en" ? "en" : "tr",
      )
      .then((session) => setState({ status: "ready", session }))
      .catch((error: unknown) => {
        if (error instanceof DOMException && error.name === "AbortError") return;
        if (error instanceof CustomerGatewayError && error.code === "INVALID_QR") {
          setState({ status: "invalid", detail: error.message });
          return;
        }
        if (error instanceof CustomerGatewayError && error.code === "MENU_UNAVAILABLE") {
          setState({ status: "menu-unavailable", detail: error.message });
          return;
        }
        // ORDER_REJECTED from resolve is almost always a stale/invalid QR on some clients.
        if (error instanceof CustomerGatewayError && error.code === "ORDER_REJECTED") {
          setState({ status: "invalid", detail: error.message });
          return;
        }
        setState({
          status: "error",
          detail: error instanceof Error ? error.message : undefined,
        });
      });
    return () => controller.abort();
  }, [activeGateway, token]);

  return (
    <div className="app-shell">
      <a className="skip-link" href="#main-content">
        {t.skipToContent}
      </a>
      {!online ? (
        <div className="offline-banner" role="status">
          {t.offline}
        </div>
      ) : null}
      {state.status === "required" ? (
        <main className="entry-state" id="main-content">
          <span className="entry-state__mark" aria-hidden="true">
            R
          </span>
          <p className="eyebrow">Restaurant OS</p>
          <h1>{t.qrRequiredTitle}</h1>
          <p>{t.qrRequiredBody}</p>
          <Button
            onClick={() => {
              const url = new URL(window.location.href);
              url.searchParams.set("qr", DEMO_QR_TOKEN);
              window.location.assign(url);
            }}
          >
            {t.openDemo}
          </Button>
        </main>
      ) : null}
      {state.status === "loading" ? (
        <main className="entry-state" id="main-content" aria-busy="true">
          <span className="loader" aria-hidden="true" />
          <h1>{t.qrResolving}</h1>
        </main>
      ) : null}
      {state.status === "invalid" ? (
        <main className="entry-state" id="main-content">
          <span className="entry-state__mark entry-state__mark--error" aria-hidden="true">
            !
          </span>
          <h1>{t.qrInvalidTitle}</h1>
          <p>{t.qrInvalidBody}</p>
          {state.detail ? <p className="service-note">{state.detail}</p> : null}
          <div className="entry-state__actions">
            <Button onClick={() => window.location.reload()}>{t.retry}</Button>
            <Button
              variant="secondary"
              onClick={() => {
                const url = new URL(window.location.href);
                url.searchParams.set("qr", DEMO_QR_TOKEN);
                window.location.assign(url);
              }}
            >
              {t.openDemo}
            </Button>
          </div>
        </main>
      ) : null}
      {state.status === "menu-unavailable" ? (
        <main className="entry-state" id="main-content">
          <span className="entry-state__mark entry-state__mark--error" aria-hidden="true">
            !
          </span>
          <h1>{t.qrMenuUnavailableTitle}</h1>
          <p>{t.qrMenuUnavailableBody}</p>
          <Button onClick={() => window.location.reload()}>{t.retry}</Button>
        </main>
      ) : null}
      {state.status === "error" ? (
        <main className="entry-state" id="main-content">
          <span className="entry-state__mark entry-state__mark--error" aria-hidden="true">
            !
          </span>
          <h1>{t.qrLoadErrorTitle}</h1>
          <p>{t.qrLoadErrorBody}</p>
          {state.detail ? <p className="service-note">{state.detail}</p> : null}
          <Button onClick={() => window.location.reload()}>{t.retry}</Button>
        </main>
      ) : null}
      {state.status === "ready" && token ? (
        <Menu session={state.session} gateway={activeGateway} qrToken={token} />
      ) : null}
    </div>
  );
}
