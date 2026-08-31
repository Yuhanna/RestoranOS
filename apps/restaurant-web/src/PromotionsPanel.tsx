import { useCallback, useEffect, useMemo, useState } from "react";
import { Button } from "@restaurant-os/design-system";
import { ApiError, type ManagementApi } from "./api";
import type {
  CreateMenuPromotionInput,
  MenuDetail,
  MenuPromotion,
  MenuPromotionDiscountKind,
  MenuPromotionScope,
} from "./domain";

const errorMessage = (error: unknown) =>
  error instanceof ApiError ? error.message : "Beklenmeyen bir hata oluştu.";

const scopeLabel: Record<MenuPromotionScope, string> = {
  all_menu: "Tüm menü",
  category: "Kategori",
  product: "Ürün",
};

const discountLabel = (promotion: MenuPromotion) =>
  promotion.discountKind === "percent"
    ? `%${promotion.discountValue}`
    : `${(promotion.discountValue / 100).toFixed(0)} TL`;

type PromotionsPanelProps = {
  managementApi: ManagementApi;
  onError: (message: string) => void;
};

export function PromotionsPanel({ managementApi, onError }: PromotionsPanelProps) {
  const [promotions, setPromotions] = useState<MenuPromotion[]>([]);
  const [menuDetail, setMenuDetail] = useState<MenuDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [name, setName] = useState("");
  const [scope, setScope] = useState<MenuPromotionScope>("all_menu");
  const [discountKind, setDiscountKind] = useState<MenuPromotionDiscountKind>("percent");
  const [discountValue, setDiscountValue] = useState("20");
  const [dailyStart, setDailyStart] = useState("");
  const [dailyEnd, setDailyEnd] = useState("");
  const [categoryId, setCategoryId] = useState("");
  const [menuItemId, setMenuItemId] = useState("");
  const [endsAtLocal, setEndsAtLocal] = useState("");

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [nextPromotions, menus] = await Promise.all([
        managementApi.listMenuPromotions(),
        managementApi.listMenus(),
      ]);
      setPromotions(nextPromotions);
      const published = menus.find((menu) => menu.lifecycle === "published") ?? menus[0];
      if (published) {
        setMenuDetail(await managementApi.getMenu(published.id));
      } else {
        setMenuDetail(null);
      }
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

  const categoryOptions = useMemo(() => menuDetail?.categories ?? [], [menuDetail]);
  const productOptions = useMemo(() => menuDetail?.items ?? [], [menuDetail]);

  const create = async () => {
    if (!name.trim()) return;
    setBusy(true);
    try {
      const parsedValue =
        discountKind === "percent"
          ? Number.parseInt(discountValue, 10)
          : Math.round(Number.parseFloat(discountValue.replace(",", ".")) * 100);
      const input: CreateMenuPromotionInput = {
        name: name.trim(),
        scope,
        discountKind,
        discountValue: parsedValue,
        startsAtUtc: new Date().toISOString(),
        endsAtUtc: endsAtLocal
          ? new Date(`${endsAtLocal}T23:59:59`).toISOString()
          : null,
        dailyStartLocal: dailyStart || null,
        dailyEndLocal: dailyEnd || null,
        categoryId: scope === "category" ? categoryId || null : null,
        menuItemId: scope === "product" ? menuItemId || null : null,
        isActive: true,
      };
      await managementApi.createMenuPromotion(input);
      setName("");
      await load();
    } catch (createError) {
      onError(errorMessage(createError));
    } finally {
      setBusy(false);
    }
  };

  const toggleActive = async (promotion: MenuPromotion) => {
    setBusy(true);
    try {
      await managementApi.updateMenuPromotion(promotion.id, { isActive: !promotion.isActive });
      await load();
    } catch (updateError) {
      onError(errorMessage(updateError));
    } finally {
      setBusy(false);
    }
  };

  return (
    <section className="tables-panel" aria-label="Menü indirimleri">
      <header className="tables-panel__header">
        <div>
          <p className="eyebrow">MÜŞTERİ MENÜSÜ</p>
          <h2>İndirim kampanyaları</h2>
          <p className="muted">QR menüde ve sepette otomatik uygulanır.</p>
        </div>
        <Button variant="ghost" onClick={() => void load()} disabled={loading || busy}>
          Yenile
        </Button>
      </header>

      <div className="tables-layout">
        <div className="table-list">
          <h3>Yeni kampanya</h3>
          <label>
            Ad
            <input value={name} onChange={(event) => setName(event.target.value)} placeholder="Öğle menüsü %20" />
          </label>
          <label>
            Kapsam
            <select value={scope} onChange={(event) => setScope(event.target.value as MenuPromotionScope)}>
              <option value="all_menu">Tüm menü</option>
              <option value="category">Kategori</option>
              <option value="product">Tek ürün</option>
            </select>
          </label>
          {scope === "category" ? (
            <label>
              Kategori
              <select value={categoryId} onChange={(event) => setCategoryId(event.target.value)}>
                <option value="">Seçin</option>
                {categoryOptions.map((category) => (
                  <option key={category.id} value={category.id}>
                    {category.name}
                  </option>
                ))}
              </select>
            </label>
          ) : null}
          {scope === "product" ? (
            <label>
              Ürün
              <select value={menuItemId} onChange={(event) => setMenuItemId(event.target.value)}>
                <option value="">Seçin</option>
                {productOptions.map((item) => (
                  <option key={item.id} value={item.id}>
                    {item.name}
                  </option>
                ))}
              </select>
            </label>
          ) : null}
          <label>
            İndirim türü
            <select
              value={discountKind}
              onChange={(event) => setDiscountKind(event.target.value as MenuPromotionDiscountKind)}
            >
              <option value="percent">Yüzde</option>
              <option value="fixed_minor">Sabit tutar (TL)</option>
            </select>
          </label>
          <label>
            {discountKind === "percent" ? "Yüzde" : "Tutar (TL)"}
            <input value={discountValue} onChange={(event) => setDiscountValue(event.target.value)} />
          </label>
          <label>
            Günlük başlangıç (isteğe bağlı)
            <input type="time" value={dailyStart} onChange={(event) => setDailyStart(event.target.value)} />
          </label>
          <label>
            Günlük bitiş (isteğe bağlı)
            <input type="time" value={dailyEnd} onChange={(event) => setDailyEnd(event.target.value)} />
          </label>
          <label>
            Bitiş tarihi (isteğe bağlı)
            <input type="date" value={endsAtLocal} onChange={(event) => setEndsAtLocal(event.target.value)} />
          </label>
          <Button disabled={busy || !name.trim()} onClick={() => void create()}>
            Kampanya oluştur
          </Button>
        </div>

        <div className="qr-list">
          <h3>Aktif kampanyalar</h3>
          {loading ? <p className="muted">Yükleniyor…</p> : null}
          {!loading && promotions.length === 0 ? (
            <p className="muted">Henüz kampanya yok.</p>
          ) : (
            promotions.map((promotion) => (
              <article className="qr-row" key={promotion.id}>
                <div>
                  <strong>{promotion.name}</strong>
                  <p className="muted">
                    {scopeLabel[promotion.scope as MenuPromotionScope] ?? promotion.scope} ·{" "}
                    {discountLabel(promotion)}
                    {promotion.dailyStartLocal && promotion.dailyEndLocal
                      ? ` · ${promotion.dailyStartLocal.slice(0, 5)}–${promotion.dailyEndLocal.slice(0, 5)}`
                      : ""}
                  </p>
                </div>
                <Button
                  variant={promotion.isActive ? "secondary" : "ghost"}
                  disabled={busy}
                  onClick={() => void toggleActive(promotion)}
                >
                  {promotion.isActive ? "Aktif" : "Pasif"}
                </Button>
              </article>
            ))
          )}
        </div>
      </div>
    </section>
  );
}
