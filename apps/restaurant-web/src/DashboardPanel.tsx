import { useCallback, useEffect, useState } from "react";
import { ApiError, type ManagementApi } from "./api";
import type { TodayDashboard } from "./domain";

const formatMoney = (minor: number, currency: string) =>
  (minor / 100).toLocaleString("tr-TR", { style: "currency", currency });

const errorMessage = (error: unknown) =>
  error instanceof ApiError ? error.message : "Beklenmeyen bir hata oluştu.";

type DashboardPanelProps = {
  managementApi: ManagementApi;
  onError: (message: string) => void;
};

export function DashboardPanel({ managementApi, onError }: DashboardPanelProps) {
  const [summary, setSummary] = useState<TodayDashboard | null>(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setSummary(await managementApi.getTodayDashboard());
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

  if (loading && !summary) {
    return <p className="muted">Gösterge paneli yükleniyor…</p>;
  }

  if (!summary) {
    return <p className="muted">Bugünkü metrikler yüklenemedi.</p>;
  }

  return (
    <section className="dashboard-panel" aria-label="Bugünkü gösterge paneli">
      <header className="dashboard-panel__header">
        <div>
          <p className="eyebrow">GÖSTERGE PANELİ</p>
          <h2>Bugün</h2>
        </div>
        <button type="button" className="filter" onClick={() => void load()} disabled={loading}>
          {loading ? "Yenileniyor…" : "Yenile"}
        </button>
      </header>
      <div className="dashboard-grid">
        <article className="dashboard-card">
          <span>Bugünkü siparişler</span>
          <strong>{summary.todaysOrderCount}</strong>
        </article>
        <article className="dashboard-card">
          <span>Bugünkü gelir</span>
          <strong>{formatMoney(summary.todaysRevenueMinor, summary.currency)}</strong>
        </article>
        <article className="dashboard-card">
          <span>Açık masalar</span>
          <strong>{summary.openTablesCount}</strong>
        </article>
        <article className="dashboard-card">
          <span>Bekleyen siparişler</span>
          <strong>{summary.pendingOrdersCount}</strong>
        </article>
        <article className="dashboard-card">
          <span>Tamamlanan (bugün)</span>
          <strong>{summary.completedOrdersTodayCount}</strong>
        </article>
      </div>
    </section>
  );
}
