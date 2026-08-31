import { useCallback, useEffect, useState } from "react";
import { Button } from "@restaurant-os/design-system";
import { ApiError, type ManagementApi } from "./api";
import type { ServiceRequest } from "./domain";
import { playAlertChime, serviceRequestAlertKind } from "./notifications";

const typeLabel: Record<string, string> = {
  waiter: "Garson çağır",
  bill: "Hesap iste",
};

const errorMessage = (error: unknown) =>
  error instanceof ApiError ? error.message : "Beklenmeyen bir hata oluştu.";

type ServiceRequestsPanelProps = {
  managementApi: ManagementApi;
  onError: (message: string) => void;
  incoming?: ServiceRequest | null;
};

export function ServiceRequestsPanel({
  managementApi,
  onError,
  incoming,
}: ServiceRequestsPanelProps) {
  const [requests, setRequests] = useState<ServiceRequest[]>([]);
  const [loading, setLoading] = useState(true);
  const [pendingId, setPendingId] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setRequests(await managementApi.listOpenServiceRequests());
    } catch (loadError) {
      onError(errorMessage(loadError));
    } finally {
      setLoading(false);
    }
  }, [managementApi, onError]);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    if (!incoming || incoming.status !== "open") return;
    setRequests((current) => {
      if (current.some((request) => request.id === incoming.id)) return current;
      playAlertChime(serviceRequestAlertKind(incoming.type));
      return [incoming, ...current];
    });
  }, [incoming]);

  const complete = async (request: ServiceRequest) => {
    setPendingId(request.id);
    try {
      await managementApi.completeServiceRequest(request.id);
      setRequests((current) => current.filter((item) => item.id !== request.id));
    } catch (completeError) {
      onError(errorMessage(completeError));
    } finally {
      setPendingId(null);
    }
  };

  return (
    <section className="tables-panel" aria-label="Garson ve hesap çağrıları">
      <header className="tables-panel__header">
        <div>
          <h2>Servis çağrıları</h2>
          <p className="muted">Açık garson ve hesap istekleri · {requests.length} bekleyen</p>
        </div>
        <Button variant="ghost" onClick={() => void load()} disabled={loading}>
          Yenile
        </Button>
      </header>
      {loading ? <p className="muted">Yükleniyor…</p> : null}
      {!loading && requests.length === 0 ? (
        <p className="muted">Açık çağrı yok.</p>
      ) : (
        <ul className="qr-list">
          {requests.map((request) => (
            <li key={request.id} className="qr-row">
              <div>
                <strong>{request.tableLabel}</strong>
                <span className="muted">
                  {typeLabel[request.type] ?? request.type} ·{" "}
                  {new Date(request.createdAtUtc).toLocaleString("tr-TR")}
                </span>
                {request.note ? <span className="muted">{request.note}</span> : null}
              </div>
              <Button
                variant="secondary"
                disabled={pendingId === request.id}
                onClick={() => void complete(request)}
              >
                Tamamlandı
              </Button>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
