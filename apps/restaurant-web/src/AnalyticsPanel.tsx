import { useCallback, useEffect, useMemo, useState } from "react";
import { Button } from "@restaurant-os/design-system";
import { ApiError, type ManagementApi } from "./api";
import type { AnalyticsSummary, SalesPeriod, TopItem, Workspace } from "./domain";

const formatMoney = (minor: number, currency: string) =>
  (minor / 100).toLocaleString("tr-TR", { style: "currency", currency });

const errorMessage = (error: unknown) =>
  error instanceof ApiError ? error.message : "Beklenmeyen bir hata oluştu.";

type AnalyticsPanelProps = {
  managementApi: ManagementApi;
  onError: (message: string) => void;
};

export function AnalyticsPanel({ managementApi, onError }: AnalyticsPanelProps) {
  const [days, setDays] = useState(30);
  const [summary, setSummary] = useState<AnalyticsSummary | null>(null);
  const [periods, setPeriods] = useState<SalesPeriod[]>([]);
  const [topItems, setTopItems] = useState<TopItem[]>([]);
  const [workspace, setWorkspace] = useState<Workspace | null>(null);
  const [loading, setLoading] = useState(true);
  const [checkoutBusy, setCheckoutBusy] = useState(false);

  const range = useMemo(() => {
    const to = new Date();
    const from = new Date(to.getTime() - days * 86_400_000);
    return { fromUtc: from.toISOString(), toUtc: to.toISOString() };
  }, [days]);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [nextSummary, nextPeriods, nextTopItems, nextWorkspace] = await Promise.all([
        managementApi.getAnalyticsSummary(range.fromUtc, range.toUtc),
        managementApi.getSalesByPeriod(range.fromUtc, range.toUtc, days <= 2 ? "hour" : "day"),
        managementApi.getTopItems(range.fromUtc, range.toUtc, 10),
        managementApi.getWorkspace(),
      ]);
      setSummary(nextSummary);
      setPeriods(nextPeriods);
      setTopItems(nextTopItems);
      setWorkspace(nextWorkspace);
    } catch (loadError) {
      onError(errorMessage(loadError));
    } finally {
      setLoading(false);
    }
  }, [managementApi, onError, range.fromUtc, range.toUtc, days]);

  useEffect(() => {
    void load();
  }, [load]);

  const checkout = async (planCode: "Pro" | "Enterprise") => {
    setCheckoutBusy(true);
    try {
      await managementApi.checkoutPlan(planCode);
      await load();
    } catch (checkoutError) {
      onError(errorMessage(checkoutError));
    } finally {
      setCheckoutBusy(false);
    }
  };

  const currency = summary?.currency ?? "TRY";
  const maxSales = Math.max(...periods.map((period) => period.grossSalesMinor), 1);

  return (
    <section className="tables-panel" aria-label="İstatistikler">
      <header className="tables-panel__header">
        <div>
          <h2>İstatistikler</h2>
          <p className="muted">
            Satış, brüt kâr ve en çok satan ürünler
            {workspace?.entitlements ? ` · Plan: ${workspace.entitlements.planDisplayName}` : ""}
          </p>
        </div>
        <div className="filter-group">
          {[7, 30, 90].map((value) => (
            <button
              key={value}
              className={days === value ? "filter active" : "filter"}
              onClick={() => setDays(value)}
            >
              {value} gün
            </button>
          ))}
          <Button variant="ghost" onClick={() => void load()} disabled={loading}>
            Yenile
          </Button>
        </div>
      </header>

      {summary.canViewFinancials &&
      workspace?.entitlements &&
      workspace.entitlements.planCode === "Free" &&
      !workspace.entitlements.isTrial ? (
        <div className="notice">
          Free plandasınız. Pro ile sınırsız masa, çeviri ve canlı panel açılır.
          <Button variant="secondary" disabled={checkoutBusy} onClick={() => void checkout("Pro")}>
            Pro&apos;ya yükselt (demo)
          </Button>
        </div>
      ) : null}

      {loading || !summary ? (
        <p className="muted">İstatistikler yükleniyor…</p>
      ) : (
        <>
          <div className="topbar__metrics analytics-grid" aria-label="Özet metrikler">
            <MetricCard label="Ciro" value={formatMoney(summary.grossSalesMinor, currency)} />
            {summary.canViewFinancials ? (
              <>
                <MetricCard
                  label="Tahmini maliyet"
                  value={formatMoney(summary.estimatedCostMinor ?? 0, currency)}
                />
                <MetricCard
                  label="Brüt kâr"
                  value={formatMoney(summary.grossProfitMinor ?? 0, currency)}
                />
              </>
            ) : null}
            <MetricCard label="Tamamlanan sipariş" value={String(summary.completedOrderCount)} />
            <MetricCard label="Ortalama sepet" value={formatMoney(summary.averageTicketMinor, currency)} />
            <MetricCard label="İptal" value={String(summary.cancelledOrderCount)} />
            <MetricCard label="Açık servis çağrısı" value={String(summary.openServiceRequestCount)} />
          </div>

          <h3>Satış trendi</h3>
          {periods.length === 0 ? (
            <p className="muted">Seçilen aralıkta tamamlanan sipariş yok.</p>
          ) : (
            <ul className="analytics-bars">
              {periods.map((period) => (
                <li key={period.periodStartUtc}>
                  <span className="muted">
                    {new Date(period.periodStartUtc).toLocaleString("tr-TR", {
                      day: "2-digit",
                      month: "2-digit",
                      hour: days <= 2 ? "2-digit" : undefined,
                      minute: days <= 2 ? "2-digit" : undefined,
                    })}
                  </span>
                  <div className="analytics-bar">
                    <span
                      style={{ width: `${Math.max(4, (period.grossSalesMinor / maxSales) * 100)}%` }}
                      title={formatMoney(period.grossSalesMinor, currency)}
                    />
                  </div>
                  <strong>{formatMoney(period.grossSalesMinor, currency)}</strong>
                </li>
              ))}
            </ul>
          )}

          <h3>En çok satan ürünler</h3>
          {topItems.length === 0 ? (
            <p className="muted">Ürün verisi yok.</p>
          ) : (
            <ul className="qr-list">
              {topItems.map((item) => (
                <li key={item.menuItemId} className="qr-row">
                  <div>
                    <strong>{item.name}</strong>
                    <span className="muted">
                      {item.quantitySold} adet
                      {item.grossProfitMinor != null
                        ? ` · kâr ${formatMoney(item.grossProfitMinor, currency)}`
                        : ""}
                    </span>
                  </div>
                  <strong>{formatMoney(item.revenueMinor, currency)}</strong>
                </li>
              ))}
            </ul>
          )}
        </>
      )}
    </section>
  );
}

function MetricCard({ label, value }: { label: string; value: string }) {
  return (
    <div className="metric">
      <span className="metric__value">{value}</span>
      <span className="metric__label">{label}</span>
    </div>
  );
}
