import { useCallback, useEffect, useState } from "react";
import { ApiError, type ManagementApi } from "./api";
import type { Workspace } from "./domain";
import { ThemePicker } from "./ThemePicker";

const errorMessage = (error: unknown) =>
  error instanceof ApiError ? error.message : "Beklenmeyen bir hata oluştu.";

type SettingsPanelProps = {
  managementApi: ManagementApi;
  onError: (message: string) => void;
  onLogout: () => void;
};

export function SettingsPanel({ managementApi, onError, onLogout }: SettingsPanelProps) {
  const [workspace, setWorkspace] = useState<Workspace | null>(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setWorkspace(await managementApi.getWorkspace());
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

  const entitlements = workspace?.entitlements;

  return (
    <section className="settings-panel" aria-label="Ayarlar">
      <header>
        <p className="eyebrow">AYARLAR</p>
        <h2>Hesap ve tercihler</h2>
      </header>

      {loading && !workspace ? <p className="muted">Ayarlar yükleniyor…</p> : null}

      {workspace ? (
        <dl className="settings-list">
          <div>
            <dt>Restoran</dt>
            <dd>{workspace.restaurantName}</dd>
          </div>
          <div>
            <dt>Şube</dt>
            <dd>{workspace.branchName}</dd>
          </div>
          {entitlements ? (
            <>
              <div>
                <dt>Plan</dt>
                <dd>
                  {entitlements.planDisplayName} ({entitlements.planCode})
                </dd>
              </div>
              <div>
                <dt>Masa kotası</dt>
                <dd>
                  {entitlements.tableCount} / {entitlements.maxTablesPerBranch ?? "∞"}
                </dd>
              </div>
            </>
          ) : null}
        </dl>
      ) : null}

      {entitlements?.warnings?.length ? (
        <ul className="settings-warnings">
          {entitlements.warnings.map((warning) => (
            <li key={warning}>{warning}</li>
          ))}
        </ul>
      ) : null}

      <div className="settings-actions">
        <ThemePicker />
        <button type="button" className="filter" onClick={() => void onLogout()}>
          Çıkış yap
        </button>
      </div>
    </section>
  );
}
