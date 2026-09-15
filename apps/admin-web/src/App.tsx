import { FormEvent, useEffect, useState } from "react";
import { AdminApi, ApiError, api as defaultApi } from "./api";
import type { Catalog, PlanPrice, Session, SubscriptionOffer, ViewName } from "./domain";

const money = (minor: number, currency: string) =>
  `${(minor / 100).toLocaleString("tr-TR", { minimumFractionDigits: 2, maximumFractionDigits: 2 })} ${currency}`;

const statusClass = (status: string) =>
  status === "published" ? "status-published" : status === "draft" ? "status-draft" : "status-archived";

export function App({ api = defaultApi }: { api?: AdminApi }) {
  const [session, setSession] = useState<Session | null>(null);
  const [booting, setBooting] = useState(true);
  const [view, setView] = useState<ViewName>("overview");
  const [error, setError] = useState<string | null>(null);
  const [catalog, setCatalog] = useState<Catalog | null>(null);
  const [offers, setOffers] = useState<SubscriptionOffer[]>([]);

  useEffect(() => {
    let active = true;
    api
      .restore()
      .then((next) => {
        if (active) setSession(next);
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
  }, [api]);

  useEffect(() => {
    if (!session) return;
    let active = true;
    Promise.all([api.getCatalog(), api.listOffers()])
      .then(([nextCatalog, nextOffers]) => {
        if (!active) return;
        setCatalog(nextCatalog);
        setOffers(nextOffers);
      })
      .catch((caught) => {
        if (!active) return;
        if (caught instanceof ApiError && caught.status === 401) {
          setSession(null);
          return;
        }
        setError(caught instanceof ApiError ? caught.message : "Veriler yüklenemedi.");
      });
    return () => {
      active = false;
    };
  }, [api, session]);

  const handleWorkspaceError = (message: string | null) => {
    if (message === "SESSION_EXPIRED") {
      setSession(null);
      return;
    }
    setError(message);
  };

  if (booting) {
    return (
      <div className="login-panel">
        <p className="muted">Pasa Admin yükleniyor…</p>
      </div>
    );
  }

  if (!session) {
    return <LoginScreen api={api} onLoggedIn={setSession} />;
  }

  return (
    <div className="shell">
      <aside className="rail">
        <div className="rail-brand">
          <img src="/pasa-admin-mark.svg" alt="" />
          <div>
            <strong>Pasa Admin</strong>
            <span>İç yönetim · admin.pasa.app</span>
          </div>
        </div>
        <nav aria-label="Admin menü">
          <button className={view === "overview" ? "active" : ""} type="button" onClick={() => setView("overview")}>
            Özet
          </button>
          <button className={view === "catalog" ? "active" : ""} type="button" onClick={() => setView("catalog")}>
            Planlar ve fiyatlar
          </button>
          <button className={view === "campaigns" ? "active" : ""} type="button" onClick={() => setView("campaigns")}>
            Üyelik kampanyaları
          </button>
        </nav>
        <div className="rail-foot">
          <div>{session.email ?? session.userId}</div>
          <div>Rol: {session.roleCode ?? "—"} · platform</div>
          <button
            className="btn secondary"
            type="button"
            style={{ marginTop: "0.75rem", width: "100%" }}
            onClick={() => {
              void api.logout().finally(() => setSession(null));
            }}
          >
            Çıkış
          </button>
        </div>
      </aside>
      <main className="workspace">
        {error ? (
          <div className="alert" role="alert">
            {error}
          </div>
        ) : null}
        {view === "overview" ? <Overview session={session} catalog={catalog} offerCount={offers.length} /> : null}
        {view === "catalog" ? (
          <CatalogScreen
            api={api}
            session={session}
            catalog={catalog}
            onCatalog={setCatalog}
            onError={handleWorkspaceError}
          />
        ) : null}
        {view === "campaigns" ? (
          <CampaignsScreen
            api={api}
            session={session}
            offers={offers}
            onOffers={setOffers}
            onError={handleWorkspaceError}
          />
        ) : null}
      </main>
    </div>
  );
}

function LoginScreen({
  api,
  onLoggedIn,
}: {
  api: AdminApi;
  onLoggedIn: (session: Session) => void;
}) {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setBusy(true);
    setError(null);
    try {
      onLoggedIn(await api.login(email, password));
    } catch (caught) {
      setError(
        caught instanceof ApiError && caught.code === "PLATFORM_ACCESS_DENIED"
          ? "Bu hesap restoran paneli içindir. Pasa Admin için platform operatörü girişi kullanın."
          : caught instanceof ApiError
            ? caught.message
            : "Giriş tamamlanamadı.",
      );
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="login-split">
      <section className="login-brand">
        <div>
          <p className="eyebrow" style={{ color: "#93c5fd" }}>
            Pasa Admin
          </p>
          <h1>Bu restoran paneli değil.</h1>
          <p>
            Üyelik fiyatları, aylık/yıllık katalog ve kampanyalar için iç yönetim konsolu. Sipariş, masa ve menü
            buradan yönetilmez.
          </p>
        </div>
        <p>Canlı adres: admin.pasa.app</p>
      </section>
      <section className="login-panel">
        <div className="login-card">
          <p className="eyebrow">İç sistem</p>
          <h2>Pasa Admin girişi</h2>
          <p className="muted">Kayıt yoktur. Yalnızca davetli platform operatörleri.</p>
          {error ? (
            <div className="alert" role="alert">
              {error}
            </div>
          ) : null}
          <form className="stack" onSubmit={(event) => void submit(event)}>
            <label>
              E-posta
              <input
                autoComplete="username"
                inputMode="email"
                required
                value={email}
                onChange={(event) => setEmail(event.target.value)}
              />
            </label>
            <label>
              Parola
              <input
                type="password"
                autoComplete="current-password"
                required
                value={password}
                onChange={(event) => setPassword(event.target.value)}
              />
            </label>
            <button className="btn" type="submit" disabled={busy}>
              {busy ? "Giriş yapılıyor…" : "Admin girişi"}
            </button>
          </form>
        </div>
      </section>
    </div>
  );
}

function Overview({
  session,
  catalog,
  offerCount,
}: {
  session: Session;
  catalog: Catalog | null;
  offerCount: number;
}) {
  const published = catalog?.products.flatMap((product) => product.prices).filter((price) => price.status === "published")
    .length ?? 0;
  return (
    <>
      <div className="page-head">
        <div>
          <p className="eyebrow">Pasa Admin</p>
          <h1>Kontrol özeti</h1>
          <p className="note">
            Restoran operasyonu değil; SaaS üyelik kataloğu. Yayınlanan fiyat yeni yükseltmelerde geçerlidir.
          </p>
        </div>
        <span className="chip">{import.meta.env.DEV ? "Geliştirme" : "Canlı"}</span>
      </div>
      <div className="cards">
        <article className="card">
          <h2>Oturum</h2>
          <p>
            {session.email} · {session.roleCode}
          </p>
        </article>
        <article className="card">
          <h2>Yayınlı fiyat</h2>
          <p>{published} sürüm</p>
        </article>
        <article className="card">
          <h2>Aktif kampanya kaydı</h2>
          <p>{offerCount}</p>
        </article>
      </div>
    </>
  );
}

function CatalogScreen({
  api,
  session,
  catalog,
  onCatalog,
  onError,
}: {
  api: AdminApi;
  session: Session;
  catalog: Catalog | null;
  onCatalog: (catalog: Catalog) => void;
  onError: (message: string | null) => void;
}) {
  const canWrite = session.roleCode === "Owner" || session.roleCode === "Billing";
  const canPublish = session.roleCode === "Owner";
  const [productCode, setProductCode] = useState("Pro");
  const [interval, setInterval] = useState("month");
  const [amount, setAmount] = useState("2499");
  const [busy, setBusy] = useState(false);

  const canArchive = (status: string) =>
    status === "draft" ? canWrite : status === "published" ? canPublish : false;

  const create = async (event: FormEvent) => {
    event.preventDefault();
    const lira = Number(amount.replace(",", "."));
    if (!Number.isFinite(lira) || lira < 0) {
      onError("Geçerli bir tutar girin.");
      return;
    }
    setBusy(true);
    onError(null);
    try {
      await api.createPrice({
        productCode,
        interval,
        amountMinor: Math.round(lira * 100),
      });
      onCatalog(await api.getCatalog());
    } catch (caught) {
      if (caught instanceof ApiError && caught.status === 401) {
        onError("SESSION_EXPIRED");
        return;
      }
      onError(caught instanceof ApiError ? caught.message : "Fiyat kaydedilemedi.");
    } finally {
      setBusy(false);
    }
  };

  const act = async (price: PlanPrice, action: "publish" | "archive") => {
    setBusy(true);
    onError(null);
    try {
      if (action === "publish") await api.publishPrice(price.id);
      else await api.archivePrice(price.id);
      onCatalog(await api.getCatalog());
    } catch (caught) {
      if (caught instanceof ApiError && caught.status === 401) {
        onError("SESSION_EXPIRED");
        return;
      }
      onError(caught instanceof ApiError ? caught.message : "İşlem tamamlanamadı.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <div className="page-head">
        <div>
          <h1>Planlar ve fiyatlar</h1>
          <p className="note">{catalog?.note}</p>
        </div>
      </div>
      {canWrite ? (
        <form className="form-row" onSubmit={(event) => void create(event)}>
          <label>
            Ürün
            <select
              value={productCode}
              onChange={(event) => {
                const next = event.target.value;
                setProductCode(next);
                if (next === "Free") setAmount("0");
              }}
            >
              <option value="Free">Free</option>
              <option value="Pro">Pro</option>
              <option value="Enterprise">Enterprise</option>
              <option value="ExtraBranch">Ek şube</option>
            </select>
          </label>
          <label>
            Aralık
            <select value={interval} onChange={(event) => setInterval(event.target.value)}>
              <option value="month">Aylık</option>
              <option value="year">Yıllık</option>
            </select>
          </label>
          <label>
            Tutar (KDV dahil, ₺)
            <input
              value={amount}
              disabled={productCode === "Free"}
              onChange={(event) => setAmount(event.target.value)}
            />
          </label>
          <button className="btn" type="submit" disabled={busy}>
            Taslak oluştur
          </button>
        </form>
      ) : (
        <p className="muted">Bu rol katalog yazamaz.</p>
      )}
      {(catalog?.products ?? []).map((product) => (
        <section key={product.productCode} className="card" style={{ marginBottom: "1rem" }}>
          <h2>
            {product.displayName}{" "}
            <span className="muted">
              {product.productKind === "addon" ? "ek ürün" : "plan"}
            </span>
          </h2>
          <table className="data">
            <thead>
              <tr>
                <th>Aralık</th>
                <th>Tutar</th>
                <th>Durum</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {product.prices.length === 0 ? (
                <tr>
                  <td colSpan={4} className="muted">
                    Fiyat yok
                  </td>
                </tr>
              ) : (
                product.prices.map((price) => (
                  <tr key={price.id}>
                    <td>{price.interval === "year" ? "Yıllık" : "Aylık"}</td>
                    <td>{money(price.amountMinor, price.currency)}</td>
                    <td className={statusClass(price.status)}>{price.status}</td>
                    <td>
                      {canPublish && price.status === "draft" ? (
                        <button className="btn" type="button" disabled={busy} onClick={() => void act(price, "publish")}>
                          Yayınla
                        </button>
                      ) : null}
                      {canArchive(price.status) ? (
                        <button
                          className="btn secondary"
                          type="button"
                          disabled={busy}
                          onClick={() => void act(price, "archive")}
                        >
                          Arşivle
                        </button>
                      ) : null}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </section>
      ))}
    </>
  );
}

function CampaignsScreen({
  api,
  session,
  offers,
  onOffers,
  onError,
}: {
  api: AdminApi;
  session: Session;
  offers: SubscriptionOffer[];
  onOffers: (offers: SubscriptionOffer[]) => void;
  onError: (message: string | null) => void;
}) {
  const canWrite = session.roleCode === "Owner" || session.roleCode === "Billing" || session.roleCode === "Support";
  const [title, setTitle] = useState("Pro geçiş kampanyası");
  const [body, setBody] = useState("3 ay %20 indirim.");
  const [percent, setPercent] = useState("20");
  const [months, setMonths] = useState("3");
  const [busy, setBusy] = useState(false);

  const create = async (event: FormEvent) => {
    event.preventDefault();
    setBusy(true);
    onError(null);
    try {
      await api.createOffer({
        audience: "NonPro",
        targetPlanCode: "Pro",
        discountPercent: Number(percent),
        durationMonths: Number(months),
        title,
        body,
      });
      onOffers(await api.listOffers());
    } catch (caught) {
      if (caught instanceof ApiError && caught.status === 401) {
        onError("SESSION_EXPIRED");
        return;
      }
      onError(caught instanceof ApiError ? caught.message : "Kampanya kaydedilemedi.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <div className="page-head">
        <div>
          <h1>Üyelik kampanyaları</h1>
          <p className="note">
            Bu restoran menü indirimi değildir. SaaS yükseltme teklifidir. Kupon kodu bir sonraki dilimde eklenecek.
          </p>
        </div>
      </div>
      {canWrite ? (
        <form className="stack" style={{ maxWidth: "32rem", marginBottom: "1.5rem" }} onSubmit={(event) => void create(event)}>
          <label>
            Başlık
            <input value={title} onChange={(event) => setTitle(event.target.value)} />
          </label>
          <label>
            Metin
            <input value={body} onChange={(event) => setBody(event.target.value)} />
          </label>
          <div className="form-row">
            <label>
              İndirim %
              <input value={percent} onChange={(event) => setPercent(event.target.value)} />
            </label>
            <label>
              Süre (ay)
              <input value={months} onChange={(event) => setMonths(event.target.value)} />
            </label>
          </div>
          <button className="btn" type="submit" disabled={busy}>
            Kampanya oluştur
          </button>
        </form>
      ) : null}
      <table className="data">
        <thead>
          <tr>
            <th>Başlık</th>
            <th>Plan</th>
            <th>İndirim</th>
            <th>Durum</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          {offers.length === 0 ? (
            <tr>
              <td colSpan={5} className="muted">
                Kampanya yok
              </td>
            </tr>
          ) : (
            offers.map((offer) => (
              <tr key={offer.id}>
                <td>{offer.title}</td>
                <td>{offer.targetPlanCode}</td>
                <td>
                  %{offer.discountPercent} / {offer.durationMonths} ay
                </td>
                <td>{offer.isActive ? "aktif" : "kapalı"}</td>
                <td>
                  {canWrite ? (
                    <button
                      className="btn secondary"
                      type="button"
                      onClick={() => {
                        void api
                          .toggleOffer(offer.id, offer.isActive)
                          .then(() => api.listOffers().then(onOffers))
                          .catch((caught) => {
                            if (caught instanceof ApiError && caught.status === 401) {
                              onError("SESSION_EXPIRED");
                              return;
                            }
                            onError(caught instanceof ApiError ? caught.message : "Kampanya güncellenemedi.");
                          });
                      }}
                    >
                      {offer.isActive ? "Durdur" : "Aç"}
                    </button>
                  ) : null}
                </td>
              </tr>
            ))
          )}
        </tbody>
      </table>
    </>
  );
}
