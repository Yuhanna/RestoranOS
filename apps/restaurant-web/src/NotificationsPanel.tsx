import { useCallback, useEffect, useState } from "react";
import { Button } from "@restaurant-os/design-system";
import { ApiError, type ManagementApi } from "./api";
import type { CreateNotificationInput, ManagedNotification, SubscriptionAudience } from "./domain";

const errorMessage = (error: unknown) =>
  error instanceof ApiError ? error.message : "Beklenmeyen bir hata oluştu.";

const audienceLabel: Record<string, string> = {
  all: "Tüm üyeler",
  free: "Ücretsiz plan",
  pro: "Pro üyeler",
  trial: "Deneme sürecindekiler",
  non_pro: "Pro olmayanlar",
};

type NotificationsPanelProps = {
  managementApi: ManagementApi;
  onError: (message: string) => void;
};

export function NotificationsPanel({ managementApi, onError }: NotificationsPanelProps) {
  const [notifications, setNotifications] = useState<ManagedNotification[]>([]);
  const [loading, setLoading] = useState(true);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [audience, setAudience] = useState<SubscriptionAudience>("non_pro");
  const [title, setTitle] = useState("");
  const [body, setBody] = useState("");
  const [actionUrl, setActionUrl] = useState("");
  const [lastDispatch, setLastDispatch] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setNotifications(await managementApi.listManagedNotifications());
      onError("");
    } catch (loadError) {
      onError(errorMessage(loadError));
    } finally {
      setLoading(false);
    }
  }, [managementApi, onError]);

  useEffect(() => {
    void load();
  }, [load]);

  const create = async () => {
    if (!title.trim() || !body.trim()) return;
    setBusyId("create");
    try {
      const input: CreateNotificationInput = {
        audience,
        title: title.trim(),
        body: body.trim(),
        startsAtUtc: new Date().toISOString(),
        actionUrl: actionUrl.trim() || null,
        isActive: true,
        broadcastToAllTenants: false,
      };
      await managementApi.createManagedNotification(input);
      setTitle("");
      setBody("");
      setActionUrl("");
      await load();
    } catch (createError) {
      onError(errorMessage(createError));
    } finally {
      setBusyId(null);
    }
  };

  const dispatch = async (notification: ManagedNotification) => {
    setBusyId(notification.id);
    try {
      const result = await managementApi.dispatchNotification(notification.id);
      setLastDispatch(
        `${result.emailSentCount} e-posta, ${result.pushSentCount} anlık bildirim gönderildi.`,
      );
      await load();
    } catch (dispatchError) {
      onError(errorMessage(dispatchError));
    } finally {
      setBusyId(null);
    }
  };

  return (
    <section className="tables-panel" aria-label="Üyelik bildirimleri">
      <header className="tables-panel__header">
        <div>
          <p className="eyebrow">ÜYELİK &amp; PLAN</p>
          <h2>Bildirim gönder</h2>
          <p className="muted">Yalnızca bu restoranın üyelerine e-posta ve tarayıcı push bildirimi iletir.</p>
        </div>
        <Button variant="ghost" onClick={() => void load()} disabled={loading}>
          Yenile
        </Button>
      </header>

      {lastDispatch ? <p className="notice-inline">{lastDispatch}</p> : null}

      <div className="tables-layout">
        <div className="table-list">
          <h3>Yeni bildirim</h3>
          <label>
            Hedef kitle
            <select value={audience} onChange={(event) => setAudience(event.target.value as SubscriptionAudience)}>
              {Object.entries(audienceLabel).map(([value, label]) => (
                <option key={value} value={value}>
                  {label}
                </option>
              ))}
            </select>
          </label>
          <label>
            Başlık
            <input value={title} onChange={(event) => setTitle(event.target.value)} />
          </label>
          <label>
            Mesaj
            <textarea rows={4} value={body} onChange={(event) => setBody(event.target.value)} />
          </label>
          <label>
            Bağlantı (isteğe bağlı)
            <input
              value={actionUrl}
              onChange={(event) => setActionUrl(event.target.value)}
              placeholder="/subscription"
            />
          </label>
          <Button disabled={busyId === "create" || !title.trim() || !body.trim()} onClick={() => void create()}>
            Bildirim oluştur
          </Button>
        </div>

        <div className="qr-list">
          <h3>Kayıtlı bildirimler</h3>
          {loading ? <p className="muted">Yükleniyor…</p> : null}
          {!loading && notifications.length === 0 ? (
            <p className="muted">Henüz bildirim yok.</p>
          ) : (
            notifications.map((notification) => (
              <article className="qr-row" key={notification.id}>
                <div>
                  <strong>{notification.title}</strong>
                  <p className="muted">
                    {audienceLabel[notification.audience] ?? notification.audience}
                    {" · Bu restoran"}
                    {notification.lastDispatchedAtUtc
                      ? ` · Son gönderim: ${new Date(notification.lastDispatchedAtUtc).toLocaleString("tr-TR")}`
                      : ""}
                  </p>
                  <p>{notification.body}</p>
                </div>
                <Button
                  disabled={busyId === notification.id || !notification.isActive}
                  onClick={() => void dispatch(notification)}
                >
                  {busyId === notification.id ? "Gönderiliyor…" : "E-posta + push gönder"}
                </Button>
              </article>
            ))
          )}
        </div>
      </div>
    </section>
  );
}
