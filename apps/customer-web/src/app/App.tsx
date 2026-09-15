import { useEffect, useMemo, useRef, useState, type CSSProperties } from "react";
import { Button, Dialog } from "@restaurant-os/design-system";
import { getCustomerGateway } from "../data/customerGateway";
import { readQrFromLocation } from "../data/resolveCustomerApiBaseUrl";
import { DEMO_QR_TOKEN } from "../data/mockCustomerGateway";
import {
  CustomerGatewayError,
  type AllergenKey,
  type CartLine,
  type CustomerGateway,
  type CustomerMenuSettings,
  type CustomerSession,
  type DietaryFilterKey,
  type Locale,
  type LunchPackage,
  type ModifierGroup,
  type Money,
  type Order,
  type Product,
  type SpiceLevel,
} from "../domain/customer";
import { lunchPackageMessages, messages as t } from "../i18n/messages";
import { createClientId } from "../lib/createClientId";
import {
  displayBadge,
  filterProducts,
  nutritionSummaryParts,
} from "../lib/productFilters";
import {
  cartLineKey,
  initialPortionId,
  initialSelections,
  lineTotalMinor,
  modifierOptionIds,
  packageCartLineKey,
  portionLabel,
  selectionLabels,
  unitPriceMinor,
} from "../lib/productPricing";
import { resolveProductMediaUrl } from "../lib/resolveProductMediaUrl";
import {
  formatPromoEndsAt,
  productHasPromo,
  promoHeadline,
  promoPercentBadge,
  promoSavingsMoney,
} from "../lib/promoOffer";

const allergenLabel = (key: AllergenKey) => t.allergenKeys[key];

function SpiceIndicator({ level }: { level: SpiceLevel }) {
  if (!level) return null;
  return (
    <div className="spice-level" aria-label={`${t.spiceLevel}: ${t.spiceLevels[level]}`}>
      <span className="spice-level__label">{t.spiceLevel}</span>
      <span className="spice-level__peppers" aria-hidden="true">
        {Array.from({ length: 3 }, (_, index) => (
          <span className={index < level ? "is-active" : undefined} key={index}>
            🌶
          </span>
        ))}
      </span>
      <span className="spice-level__text">{t.spiceLevels[level]}</span>
    </div>
  );
}

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

function PriceDisplay({
  product,
  unitMinor,
  emphasize = false,
}: {
  product: Product;
  unitMinor?: number;
  emphasize?: boolean;
}) {
  if (product.priceLabel) {
    return <strong className={emphasize ? "price-final" : undefined}>{product.priceLabel}</strong>;
  }

  const displayMinor = unitMinor ?? product.price.amountMinor;
  const hasDiscount = productHasPromo(product);
  if (!hasDiscount) {
    return (
      <strong className={emphasize ? "price-final" : undefined}>
        {formatMoney({ amountMinor: displayMinor, currency: product.price.currency })}
      </strong>
    );
  }

  return (
    <span className={`price-stack${emphasize ? " price-stack--detail" : ""}`}>
      <span className="price-list">{formatMoney(product.listPrice!)}</span>
      <strong className="price-final">
        {formatMoney({ amountMinor: displayMinor, currency: product.price.currency })}
      </strong>
    </span>
  );
}

function PromoDeal({ product, compact = false }: { product: Product; compact?: boolean }) {
  if (!productHasPromo(product)) return null;
  const savings = promoSavingsMoney(product);
  const ends = formatPromoEndsAt(product.promotionEndsAtUtc);
  const percentBadge = promoPercentBadge(product);

  if (compact) {
    return (
      <span className="promo-chip" aria-label={promoHeadline(product)}>
        {percentBadge ?? t.promoBadge}
      </span>
    );
  }

  return (
    <aside className="promo-deal" aria-label={t.promoBadge}>
      <div className="promo-deal__badges">
        <span className="badge badge--promo">{percentBadge ?? t.promoBadge}</span>
        {product.promotionLabel ? (
          <span className="promo-deal__name">{product.promotionLabel}</span>
        ) : null}
      </div>
      <p className="promo-deal__headline">{promoHeadline(product)}</p>
      {savings ? (
        <p className="promo-deal__save">{t.promoYouSave(formatMoney(savings))}</p>
      ) : null}
      {ends ? (
        <p className="promo-deal__until">
          <span className="promo-deal__until-label">{t.promoUntil}</span> {ends}
        </p>
      ) : null}
    </aside>
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
  amountMinor: lineTotalMinor(line),
  currency: "TRY",
});

const formatModifierDelta = (deltaMinor: number) =>
  deltaMinor > 0 ? `+${formatMoney({ amountMinor: deltaMinor, currency: "TRY" })}` : null;

function ProductDialog({
  product,
  menuSettings,
  onClose,
  onAdd,
}: {
  product: Product;
  menuSettings?: CustomerMenuSettings;
  onClose: () => void;
  onAdd: (line: CartLine) => void;
}) {
  const showNutrition = menuSettings?.showProductNutrition === true;
  const showAllergens = menuSettings?.showProductAllergens === true;
  const showModifiers = menuSettings?.showProductModifiers === true;
  const [quantity, setQuantity] = useState(1);
  const [note, setNote] = useState("");
  const [portionId, setPortionId] = useState(() =>
    showModifiers ? initialPortionId(product) : undefined,
  );
  const [selections, setSelections] = useState(() =>
    showModifiers ? initialSelections(product) : {},
  );
  const unitMinor = unitPriceMinor(product, selections, portionId);
  const previewLine: CartLine = {
    kind: "product",
    key: cartLineKey(product, selections, note, portionId),
    product,
    quantity,
    selections,
    portionId,
    note,
  };
  const nutritionParts = nutritionSummaryParts(product.nutrition, {
    weight: t.nutritionWeight,
    volume: t.nutritionVolume,
    calories: t.nutritionCalories,
    protein: t.nutritionProtein,
    carbs: t.nutritionCarbs,
    fat: t.nutritionFat,
    sugar: t.nutritionSugar,
    salt: t.nutritionSalt,
  });
  const badgeLabel = displayBadge(product);
  const hasExtraInfo =
    (showAllergens &&
      (!!product.ingredients?.length ||
        !!product.allergenKeys.length ||
        !!product.mayContainAllergenKeys?.length ||
        !!product.certificationNotes)) ||
    (showNutrition && nutritionParts.length > 0);

  const toggleModifierOption = (group: ModifierGroup, optionId: string) => {
    const maxSelections = group.maxSelections ?? 1;
    setSelections((current) => {
      const existing = current[group.id] ?? [];
      if (maxSelections <= 1) {
        return { ...current, [group.id]: [optionId] };
      }
      if (existing.includes(optionId)) {
        return { ...current, [group.id]: existing.filter((id) => id !== optionId) };
      }
      if (existing.length >= maxSelections) {
        return current;
      }
      return { ...current, [group.id]: [...existing, optionId] };
    });
  };

  return (
    <Dialog labelledBy="product-title" dismissLabel={t.close} onDismiss={onClose}>
      <div className="product-detail">
        <img
          src={resolveProductMediaUrl(product.imageUrl)}
          alt={product.imageAlt}
          className="product-detail__image"
        />
        <Button className="dialog-close" variant="ghost" aria-label={t.close} onClick={onClose}>
          ×
        </Button>
        <div className="product-detail__content">
          <div className="product-detail__heading">
            <div>
              {badgeLabel ? <span className="badge badge--brand">{badgeLabel}</span> : null}
              <h2 id="product-title">{product.name}</h2>
              {product.dietaryTags.length ? (
                <div className="chip-row chip-row--detail">
                  {product.dietaryTags.map((tag) => (
                    <span className="badge badge--dietary" key={tag}>
                      {t.dietary[tag]}
                    </span>
                  ))}
                </div>
              ) : null}
            </div>
            <PriceDisplay product={product} unitMinor={unitMinor} emphasize />
          </div>
          <PromoDeal product={product} />
          <div className="product-detail__meta">
            {product.prepTimeMinutes ? (
              <span className="meta-pill">{t.prepTime(product.prepTimeMinutes)}</span>
            ) : null}
            {product.servingNote ? (
              <span className="meta-pill">
                {t.servingNote}: {product.servingNote}
              </span>
            ) : null}
            {product.containsAlcohol ? (
              <span className="meta-pill meta-pill--warn">{t.containsAlcohol}</span>
            ) : null}
          </div>
          <p>{product.description}</p>
          {product.spiceLevel ? <SpiceIndicator level={product.spiceLevel} /> : null}

          {hasExtraInfo ? (
            <details className="product-more">
              <summary>
                {t.moreProductInfo}
                <span className="product-more__hint">{t.moreProductInfoHint}</span>
              </summary>
              {showAllergens && product.certificationNotes ? (
                <p className="certification-note">{product.certificationNotes}</p>
              ) : null}
              {showAllergens && product.ingredients?.length ? (
                <div className="ingredients">
                  <h3>{t.ingredients}</h3>
                  <p>{product.ingredients.join(", ")}</p>
                </div>
              ) : null}
              {showNutrition && nutritionParts.length ? (
                <div className="nutrition">
                  <h3>{t.nutrition}</h3>
                  <p>{nutritionParts.join(" · ")}</p>
                </div>
              ) : null}
              {showAllergens && product.allergenKeys.length ? (
                <div className="allergens">
                  <h3>{t.allergens}</h3>
                  <div className="chip-row">
                    {product.allergenKeys.map((key) => (
                      <span className="chip chip--allergen" key={key}>
                        {allergenLabel(key)}
                      </span>
                    ))}
                  </div>
                </div>
              ) : null}
              {showAllergens && product.mayContainAllergenKeys?.length ? (
                <div className="allergens allergens--may-contain">
                  <h3>{t.mayContain}</h3>
                  <div className="chip-row">
                    {product.mayContainAllergenKeys.map((key) => (
                      <span className="chip chip--may-contain" key={key}>
                        {allergenLabel(key)}
                      </span>
                    ))}
                  </div>
                </div>
              ) : null}
            </details>
          ) : null}

          {showModifiers
            ? product.modifierGroups.map((group) => {
                const maxSelections = group.maxSelections ?? 1;
                const selected = selections[group.id] ?? [];
                return (
                  <fieldset className="modifier-group" key={group.id}>
                    <legend>
                      {group.name}{" "}
                      <small>
                        {group.required ? t.required : t.optional}
                        {maxSelections > 1 ? " · en fazla " + maxSelections : ""}
                      </small>
                    </legend>
                    {group.options.map((option) => {
                      const inputId = group.id + "-" + option.id;
                      const isChecked = selected.includes(option.id);
                      return (
                        <label className="modifier-option" htmlFor={inputId} key={option.id}>
                          <input
                            id={inputId}
                            type={maxSelections > 1 ? "checkbox" : "radio"}
                            name={maxSelections > 1 ? undefined : group.id}
                            checked={isChecked}
                            onChange={() => toggleModifierOption(group, option.id)}
                          />
                          <span>{option.name}</span>
                          {option.priceDelta.amountMinor > 0 ? (
                            <strong>{formatModifierDelta(option.priceDelta.amountMinor)}</strong>
                          ) : null}
                        </label>
                      );
                    })}
                  </fieldset>
                );
              })
            : null}
          {showModifiers && product.portions && product.portions.length > 1 ? (
            <fieldset className="modifier-group">
              <legend>
                {t.portionChoice} <small>{t.required}</small>
              </legend>
              {product.portions.map((portion) => {
                const inputId = "portion-" + portion.id;
                return (
                  <label className="modifier-option" htmlFor={inputId} key={portion.id}>
                    <input
                      id={inputId}
                      type="radio"
                      name="portion"
                      checked={portionId === portion.id}
                      onChange={() => setPortionId(portion.id)}
                    />
                    <span>{portion.name}</span>
                  </label>
                );
              })}
            </fieldset>
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
              <span>
                {formatMoney({
                  amountMinor: unitMinor * quantity,
                  currency: "TRY",
                })}
              </span>
            </Button>
          </div>
        </div>
      </div>
    </Dialog>
  );
}

function LunchPackagesSection({
  packages,
  products,
  locale,
  onAdd,
}: {
  packages: ReadonlyArray<LunchPackage>;
  products: ReadonlyArray<Product>;
  locale: Locale;
  onAdd: (pkg: LunchPackage) => void;
}) {
  const [openId, setOpenId] = useState<string | null>(null);
  const copy = lunchPackageMessages[locale] ?? lunchPackageMessages.tr;
  const productById = useMemo(() => {
    const map = new Map<string, Product>();
    for (const product of products) map.set(product.id, product);
    return map;
  }, [products]);

  if (!packages?.length) return null;

  const openPackage = openId ? packages.find((item) => item.id === openId) ?? null : null;

  const resolveComponent = (component: LunchPackage["components"][number]) => {
    const product = productById.get(component.menuItemId);
    const imageUrl = component.imageUrl || product?.imageUrl || "";
    return {
      ...component,
      imageUrl,
      imageAlt: component.imageAlt || product?.imageAlt || component.name,
      description: component.description || product?.description || "",
    };
  };

  return (
    <section className="lunch-packages" aria-label={copy.title}>
      <div className="lunch-packages__grid">
        {packages.map((pkg) => {
          const hasDiscount = pkg.discount.amountMinor > 0;
          const preview = pkg.components.slice(0, 3).map(resolveComponent);
          return (
            <article className="lunch-package-card" key={pkg.id}>
              <button
                type="button"
                className="lunch-package-card__hit"
                onClick={() => setOpenId(pkg.id)}
                aria-label={`${pkg.name}. ${copy.openDetails}`}
              >
                <div className="lunch-package-card__meta">
                  <span className="badge badge--promo">{copy.subtitle}</span>
                  {pkg.dailyStartLocal && pkg.dailyEndLocal ? (
                    <span className="lunch-package-card__hours">
                      {pkg.dailyStartLocal} – {pkg.dailyEndLocal}
                    </span>
                  ) : null}
                </div>
                <h2 className="lunch-package-card__title">{pkg.name}</h2>
                {pkg.description ? (
                  <p className="lunch-package-card__desc">{pkg.description}</p>
                ) : (
                  <p className="lunch-package-card__hint">{copy.tapHint}</p>
                )}
                {preview.length ? (
                  <ul className="lunch-package-card__thumbs" aria-hidden="true">
                    {preview.map((component) => {
                      const src = resolveProductMediaUrl(component.imageUrl);
                      return (
                        <li key={`${pkg.id}-thumb-${component.menuItemId}`}>
                          {src ? (
                            <img src={src} alt="" loading="lazy" />
                          ) : (
                            <span className="lunch-package-card__thumb-fallback">
                              {component.name.slice(0, 1)}
                            </span>
                          )}
                        </li>
                      );
                    })}
                    {pkg.components.length > preview.length ? (
                      <li className="lunch-package-card__thumb-more">
                        +{pkg.components.length - preview.length}
                      </li>
                    ) : null}
                  </ul>
                ) : null}
                <div className="lunch-package-card__price-row">
                  <span className="price-stack">
                    {hasDiscount ? (
                      <span className="price-list">{formatMoney(pkg.listPrice)}</span>
                    ) : null}
                    <strong className="price-final">{formatMoney(pkg.price)}</strong>
                  </span>
                  <span className="lunch-package-card__cta-label">{copy.openDetails}</span>
                </div>
              </button>
            </article>
          );
        })}
      </div>

      {openPackage ? (
        <Dialog
          labelledBy="lunch-package-detail-title"
          dismissLabel={copy.closeDetails}
          onDismiss={() => setOpenId(null)}
          footer={
            <div className="lunch-package-detail__footer">
              <div className="lunch-package-detail__footer-price">
                {openPackage.discount.amountMinor > 0 ? (
                  <span className="price-list">{formatMoney(openPackage.listPrice)}</span>
                ) : null}
                <strong className="price-final">{formatMoney(openPackage.price)}</strong>
                {openPackage.discount.amountMinor > 0 ? (
                  <span className="lunch-package-detail__save">
                    {copy.savings}: {formatMoney(openPackage.discount)}
                  </span>
                ) : null}
              </div>
              <Button
                onClick={() => {
                  onAdd(openPackage);
                  setOpenId(null);
                }}
              >
                {copy.add}
              </Button>
            </div>
          }
        >
          <div className="lunch-package-detail">
            <p className="eyebrow">{copy.subtitle}</p>
            <h2 id="lunch-package-detail-title">{openPackage.name}</h2>
            {openPackage.dailyStartLocal && openPackage.dailyEndLocal ? (
              <p className="lunch-package-detail__hours">
                {copy.hours}: {openPackage.dailyStartLocal} – {openPackage.dailyEndLocal}
              </p>
            ) : null}
            {openPackage.description ? <p className="lede">{openPackage.description}</p> : null}
            <ul className="lunch-package-detail__items" aria-label={copy.includes}>
              {openPackage.components.map((raw) => {
                const component = resolveComponent(raw);
                const src = resolveProductMediaUrl(component.imageUrl);
                return (
                  <li key={`${openPackage.id}-${component.menuItemId}-${component.slotLabel ?? ""}`}>
                    <div className="lunch-package-detail__media">
                      {src ? (
                        <img src={src} alt={component.imageAlt || component.name} loading="lazy" />
                      ) : (
                        <span aria-hidden="true">{component.name.slice(0, 1)}</span>
                      )}
                    </div>
                    <div className="lunch-package-detail__copy">
                      {component.slotLabel ? (
                        <span className="lunch-package-detail__slot">{component.slotLabel}</span>
                      ) : null}
                      <strong>{component.name}</strong>
                      {component.description ? <p>{component.description}</p> : null}
                    </div>
                    <span className="lunch-package-detail__item-price">
                      {formatMoney({
                        amountMinor: component.listAmountMinor,
                        currency: openPackage.price.currency,
                      })}
                    </span>
                  </li>
                );
              })}
            </ul>
          </div>
        </Dialog>
      ) : null}
    </section>
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
  submitError: string | null;
  onClose: () => void;
  onChangeQuantity: (key: string, quantity: number) => void;
  onRemove: (key: string) => void;
  onSubmit: () => void;
}) {
  const packageCopy = lunchPackageMessages[session.locale] ?? lunchPackageMessages.tr;
  const listSubtotal: Money = {
    amountMinor: lines.reduce((sum, line) => {
      const unitList =
        line.kind === "package"
          ? line.package.listPrice.amountMinor
          : (line.product.listPrice?.amountMinor ?? line.product.price.amountMinor);
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
                {submitError}
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
              {lines.map((line) => {
                if (line.kind === "package") {
                  const hasPackageDiscount = line.package.discount.amountMinor > 0;
                  const lineListTotal = {
                    amountMinor: line.package.listPrice.amountMinor * line.quantity,
                    currency: line.package.price.currency,
                  };
                  return (
                    <li key={line.key}>
                      <span className="cart-line__thumb cart-line__thumb--package" aria-hidden="true">
                        {line.package.name.slice(0, 1)}
                      </span>
                      <div>
                        <h3>{line.package.name}</h3>
                        <p className="cart-line__promo">{packageCopy.title}</p>
                        {line.package.components.length ? (
                          <p className="cart-line__modifiers">
                            {line.package.components
                              .map((component) =>
                                component.slotLabel
                                  ? `${component.slotLabel}: ${component.name}`
                                  : component.name,
                              )
                              .join(" · ")}
                          </p>
                        ) : null}
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
                      {hasPackageDiscount ? (
                        <span className="price-stack price-stack--cart">
                          <span className="price-list">{formatMoney(lineListTotal)}</span>
                          <strong className="price-final">{formatMoney(lineTotal(line))}</strong>
                        </span>
                      ) : (
                        <strong>{formatMoney(lineTotal(line))}</strong>
                      )}
                    </li>
                  );
                }

                const lineHasPromo = productHasPromo(line.product);
                const unitList = line.product.listPrice?.amountMinor;
                const lineListTotal =
                  unitList != null
                    ? {
                        amountMinor: unitList * line.quantity,
                        currency: line.product.price.currency,
                      }
                    : null;
                return (
                  <li key={line.key}>
                    <img src={resolveProductMediaUrl(line.product.imageUrl)} alt="" />
                    <div>
                      <h3>{line.product.name}</h3>
                      {lineHasPromo ? (
                        <p className="cart-line__promo">
                          {promoPercentBadge(line.product) ?? t.promoBadge}
                          {line.product.promotionLabel
                            ? ` · ${line.product.promotionLabel}`
                            : null}
                        </p>
                      ) : null}
                      {portionLabel(line.product, line.portionId) ? (
                        <p className="cart-line__modifiers">{portionLabel(line.product, line.portionId)}</p>
                      ) : null}
                      {selectionLabels(line.product, line.selections).length ? (
                        <p className="cart-line__modifiers">
                          {selectionLabels(line.product, line.selections).join(" · ")}
                        </p>
                      ) : null}
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
                    {lineHasPromo && lineListTotal ? (
                      <span className="price-stack price-stack--cart">
                        <span className="price-list">{formatMoney(lineListTotal)}</span>
                        <strong className="price-final">{formatMoney(lineTotal(line))}</strong>
                      </span>
                    ) : (
                      <strong>{formatMoney(lineTotal(line))}</strong>
                    )}
                  </li>
                );
              })}
            </ul>
            <div className="cart-total">
              <div className="cart-total__row">
                <span>{t.listPrice}</span>
                <strong>{formatMoney(listSubtotal)}</strong>
              </div>
              {hasDiscount ? (
                <div className="cart-total__row cart-total__row--discount">
                  <span>
                    {t.discountSavings}
                    <span className="cart-total__hint">Kampanyalı ürünlerden</span>
                  </span>
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

function orderStatusIndex(status: Order["status"]) {
  const statuses = ["submitted", "accepted", "preparing", "ready", "served"] as const;
  if (status === "completed") return statuses.length - 1;
  return statuses.findIndex((item) => item === status);
}

function OrderTracking({ orders, onBack }: { orders: Order[]; onBack: () => void }) {
  const statuses = ["submitted", "accepted", "preparing", "ready", "served"] as const;
  const sorted = [...orders].sort((a, b) => a.displayNumber.localeCompare(b.displayNumber));

  return (
    <main className="tracking" id="main-content">
      <span className="tracking__mark" aria-hidden="true">
        ✓
      </span>
      <p className="eyebrow">{t.tableOrdersEyebrow(sorted.length)}</p>
      <h1>{t.orderReceived}</h1>
      <p>{t.orderReceivedBodyMulti}</p>
      <div className="tracking-rounds">
        {sorted.map((order) => {
          const currentIndex = orderStatusIndex(order.status);
          return (
            <section className="tracking-round" key={order.id}>
              <div className="tracking-round__head">
                <strong>{order.displayNumber}</strong>
                <span>{(t.statuses as Record<string, string>)[order.status] ?? order.status}</span>
              </div>
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
            </section>
          );
        })}
      </div>
      <Button variant="secondary" onClick={onBack}>
        {t.backToMenu}
      </Button>
    </main>
  );
}

const activeOrderStorageKey = (qrToken: string) =>
  `restaurant-os.customer.active-orders:qr:${normalizeQrToken(qrToken)}`;
const legacyActiveOrderStorageKey = (qrToken: string) =>
  `restaurant-os.customer.active-order:qr:${normalizeQrToken(qrToken)}`;

type StoredActiveOrders = {
  orderIds: string[];
  sessionToken: string;
};

function normalizeQrToken(qrToken: string) {
  try {
    return decodeURIComponent(qrToken.trim());
  } catch {
    return qrToken.trim();
  }
}

function readStoredActiveOrders(qrToken: string): StoredActiveOrders | null {
  try {
    const raw = localStorage.getItem(activeOrderStorageKey(qrToken));
    if (raw) {
      const parsed = JSON.parse(raw) as Partial<StoredActiveOrders> & { orderId?: string };
      const ids = Array.isArray(parsed.orderIds)
        ? parsed.orderIds.map((id) => id.trim()).filter(Boolean)
        : parsed.orderId?.trim()
          ? [parsed.orderId.trim()]
          : [];
      if (ids.length && parsed.sessionToken?.trim()) {
        return { orderIds: [...new Set(ids)], sessionToken: parsed.sessionToken.trim() };
      }
    }
    const legacyRaw = localStorage.getItem(legacyActiveOrderStorageKey(qrToken));
    if (!legacyRaw) return null;
    const legacy = JSON.parse(legacyRaw) as { orderId?: string; sessionToken?: string };
    if (!legacy.orderId?.trim() || !legacy.sessionToken?.trim()) return null;
    return { orderIds: [legacy.orderId.trim()], sessionToken: legacy.sessionToken.trim() };
  } catch {
    return null;
  }
}

function writeStoredActiveOrders(qrToken: string, orderIds: string[], sessionToken: string) {
  const unique = [...new Set(orderIds.map((id) => id.trim()).filter(Boolean))];
  if (unique.length === 0) {
    clearStoredActiveOrders(qrToken);
    return;
  }
  localStorage.setItem(
    activeOrderStorageKey(qrToken),
    JSON.stringify({ orderIds: unique, sessionToken } satisfies StoredActiveOrders),
  );
  localStorage.removeItem(legacyActiveOrderStorageKey(qrToken));
}

function clearStoredActiveOrders(qrToken: string) {
  localStorage.removeItem(activeOrderStorageKey(qrToken));
  localStorage.removeItem(legacyActiveOrderStorageKey(qrToken));
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
  const [dietaryFilters, setDietaryFilters] = useState<ReadonlySet<DietaryFilterKey>>(
    () => new Set(),
  );
  const [excludedAllergens, setExcludedAllergens] = useState<ReadonlySet<AllergenKey>>(
    () => new Set(),
  );
  const [selectedProduct, setSelectedProduct] = useState<Product | null>(null);
  const [lines, setLines] = useState<CartLine[]>([]);
  const [cartOpen, setCartOpen] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [orders, setOrders] = useState<Order[]>([]);
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
      const stored = readStoredActiveOrders(qrToken);
      const fromSession = (session.activeOrders ?? []).filter((item) =>
        isActiveOrderStatus(item.status),
      );

      // Table-scoped: every open round on this QR should appear on every phone.
      if (fromSession.length > 0) {
        if (cancelled) return;
        const merged = [...fromSession].sort((a, b) =>
          a.displayNumber.localeCompare(b.displayNumber),
        );
        setOrders(merged);
        setOrderSessionToken(session.sessionToken);
        writeStoredActiveOrders(
          qrToken,
          merged.map((item) => item.id),
          session.sessionToken,
        );
        return;
      }

      const candidateIds = [...new Set(stored?.orderIds ?? [])];
      const restored: Order[] = [];
      for (const orderId of candidateIds) {
        try {
          const latest = await gateway.getOrder(
            orderId,
            session.sessionToken,
            controller.signal,
          );
          if (cancelled) return;
          if (isActiveOrderStatus(latest.status)) restored.push(latest);
        } catch (error) {
          if (error instanceof DOMException && error.name === "AbortError") return;
        }
      }

      if (cancelled) return;
      setOrders(restored);
      setOrderSessionToken(session.sessionToken);
      if (restored.length > 0) {
        writeStoredActiveOrders(
          qrToken,
          restored.map((item) => item.id),
          session.sessionToken,
        );
      } else {
        clearStoredActiveOrders(qrToken);
      }
    };

    void restore();
    return () => {
      cancelled = true;
      controller.abort();
    };
  }, [gateway, qrToken, session.activeOrders, session.sessionToken]);

  const orderIdsKey = orders.map((item) => item.id).sort().join(",");

  useEffect(() => {
    if (!orderIdsKey) return;

    const ids = orderIdsKey.split(",").filter(Boolean);
    let disposed = false;
    const stops: Array<() => Promise<void>> = [];
    const controller = new AbortController();

    const upsertLatest = (latest: Order) => {
      setOrders((current) => {
        const without = current.filter((item) => item.id !== latest.id);
        const next = isActiveOrderStatus(latest.status) ? [...without, latest] : without;
        writeStoredActiveOrders(
          qrToken,
          next.map((item) => item.id),
          orderSessionToken,
        );
        if (next.length === 0) clearStoredActiveOrders(qrToken);
        return next.sort((a, b) => a.displayNumber.localeCompare(b.displayNumber));
      });
    };

    const resyncAll = () => {
      for (const orderId of ids) {
        void gateway
          .getOrder(orderId, orderSessionToken, controller.signal)
          .then((latest) => {
            if (!disposed) upsertLatest(latest);
          })
          .catch(() => {});
      }
    };

    void resyncAll();
    const pollId = window.setInterval(resyncAll, 4000);

    for (const orderId of ids) {
      void gateway
        .watchOrder(orderId, orderSessionToken, (latest) => {
          if (!disposed) upsertLatest(latest);
        }, resyncAll)
        .then(async (unsubscribe) => {
          if (disposed) await unsubscribe();
          else stops.push(unsubscribe);
        })
        .catch(() => {});
    }

    return () => {
      disposed = true;
      window.clearInterval(pollId);
      controller.abort();
      for (const stop of stops) void stop();
    };
  }, [gateway, orderIdsKey, orderSessionToken, qrToken]);

  const filteredProducts = useMemo(
    () =>
      filterProducts(session.products, {
        categoryId: category,
        query,
        dietaryFilters,
        excludedAllergens,
      }),
    [category, dietaryFilters, excludedAllergens, query, session.products],
  );
  const dietaryFilterOptions = useMemo(
    () =>
      session.customerMenu?.showDietaryFilters
        ? session.customerMenu.dietaryFilterOptions
        : [],
    [session.customerMenu],
  );
  const allergenExclusionOptions = useMemo(
    () =>
      session.customerMenu?.showAllergenExclusions
        ? session.customerMenu.allergenExclusionOptions
        : [],
    [session.customerMenu],
  );
  const showMenuFilters = dietaryFilterOptions.length > 0 || allergenExclusionOptions.length > 0;
  const menuDisclaimer =
    session.customerMenu?.allergenDisclaimer?.trim() || t.allergenDisclaimer;
  const hasActiveFilters = dietaryFilters.size > 0 || excludedAllergens.size > 0;

  const toggleDietaryFilter = (filter: DietaryFilterKey) => {
    setDietaryFilters((current) => {
      const next = new Set(current);
      if (next.has(filter)) next.delete(filter);
      else next.add(filter);
      return next;
    });
  };

  const toggleAllergenExclusion = (allergen: AllergenKey) => {
    setExcludedAllergens((current) => {
      const next = new Set(current);
      if (next.has(allergen)) next.delete(allergen);
      else next.add(allergen);
      return next;
    });
  };
  const cartCount = lines.reduce((sum, line) => sum + line.quantity, 0);
  const cartTotal: Money = {
    amountMinor: lines.reduce((sum, line) => sum + lineTotal(line).amountMinor, 0),
    currency: "TRY",
  };

  const addLine = (incoming: CartLine) => {
    setLines((current) => {
      const existing = current.find((line) => line.key === incoming.key);
      return existing
        ? current.map((line) =>
            line === existing ? { ...line, quantity: line.quantity + incoming.quantity } : line,
          )
        : [...current, incoming];
    });
  };

  const addPackage = (pkg: LunchPackage) => {
    addLine({
      kind: "package",
      key: packageCartLineKey(pkg),
      package: pkg,
      quantity: 1,
      note: "",
    });
  };

  const submitOrder = async () => {
    setSubmitting(true);
    setSubmitError(null);
    try {
      const submitted = await gateway.submitOrder({
        sessionToken: session.sessionToken,
        idempotencyKey: idempotencyKey.current,
        lines: lines.map((line) =>
          line.kind === "package"
            ? {
                packageId: line.package.id,
                quantity: line.quantity,
                note: line.note || undefined,
              }
            : {
                productId: line.product.id,
                quantity: line.quantity,
                modifierOptionIds: modifierOptionIds(line.selections),
                note: line.note || undefined,
              },
        ),
      });
      setOrders((current) => {
        const next = [...current.filter((item) => item.id !== submitted.id), submitted];
        writeStoredActiveOrders(
          qrToken,
          next.map((item) => item.id),
          session.sessionToken,
        );
        return next;
      });
      setOrderSessionToken(session.sessionToken);
      setTrackingOpen(true);
      setCartOpen(false);
      setLines([]);
      idempotencyKey.current = createClientId();
    } catch (error) {
      if (error instanceof CustomerGatewayError && error.code === "INVALID_SESSION") {
        setSubmitError(t.submitSessionExpired);
      } else if (error instanceof CustomerGatewayError && error.code === "ORDER_REJECTED") {
        setSubmitError(t.submitOrderRejected);
      } else if (error instanceof CustomerGatewayError && error.message.trim()) {
        setSubmitError(error.message);
      } else {
        setSubmitError(t.submitError);
      }
    } finally {
      setSubmitting(false);
    }
  };

  if (orders.length > 0 && trackingOpen) {
    return <OrderTracking orders={orders} onBack={() => setTrackingOpen(false)} />;
  }

  return (
    <>
      <header className="site-header">
        <div className="restaurant-lockup">
          {session.customerMenu?.logoUrl ? (
            <img
              className="restaurant-mark restaurant-mark--logo"
              src={resolveProductMediaUrl(session.customerMenu.logoUrl)}
              alt={session.customerMenu.logoAlt || session.restaurantName || ""}
            />
          ) : (
            <img
              className="restaurant-mark restaurant-mark--logo restaurant-mark--platform"
              src="/pasa-mark.svg"
              alt="Pasa"
            />
          )}
          <div>
            <strong>{session.restaurantName ?? "Restoran"}</strong>
            <span>
              {session.branchName} · {session.tableLabel}
            </span>
          </div>
        </div>
        <div className="header-actions">
          {orders.length > 0 ? (
            <Button
              variant="secondary"
              className="header-track"
              aria-label={t.trackOrdersAria(orders.length)}
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
              variant="primary"
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
              variant="secondary"
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
        <LunchPackagesSection
          packages={session.packages}
          products={session.products}
          locale={session.locale}
          onAdd={addPackage}
        />
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
        {showMenuFilters ? (
        <section className="menu-filters" aria-label={t.dietaryFilters}>
          {dietaryFilterOptions.length ? (
            <div className="menu-filters__group">
              <span className="menu-filters__label">{t.dietaryFilters}</span>
              <div className="menu-filters__chips">
                {dietaryFilterOptions.map((filter) => (
                  <Button
                    key={filter}
                    variant={dietaryFilters.has(filter) ? "primary" : "secondary"}
                    aria-pressed={dietaryFilters.has(filter)}
                    onClick={() => toggleDietaryFilter(filter)}
                  >
                    {t.dietary[filter]}
                  </Button>
                ))}
              </div>
            </div>
          ) : null}
          {allergenExclusionOptions.length ? (
            <div className="menu-filters__group">
              <span className="menu-filters__label">{t.allergenFilters}</span>
              <div className="menu-filters__chips">
                {allergenExclusionOptions.map((allergen) => (
                  <Button
                    key={allergen}
                    variant={excludedAllergens.has(allergen) ? "primary" : "secondary"}
                    aria-pressed={excludedAllergens.has(allergen)}
                    onClick={() => toggleAllergenExclusion(allergen)}
                  >
                    {allergenLabel(allergen)}
                  </Button>
                ))}
              </div>
            </div>
          ) : null}
          {hasActiveFilters ? (
            <Button
              variant="ghost"
              onClick={() => {
                setDietaryFilters(new Set());
                setExcludedAllergens(new Set());
              }}
            >
              {t.clearFilters}
            </Button>
          ) : null}
        </section>
        ) : null}
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
                    <img
                      src={resolveProductMediaUrl(product.imageUrl)}
                      alt={product.imageAlt}
                      loading="lazy"
                    />
                    {productHasPromo(product) ? <PromoDeal product={product} compact /> : null}
                    {displayBadge(product) ? (
                      <span className="badge badge--brand">{displayBadge(product)}</span>
                    ) : null}
                    {!product.available ? <span className="badge">{t.unavailable}</span> : null}
                    {product.spiceLevel ? (
                      <span className="product-card__spice" aria-hidden="true">
                        {"🌶".repeat(product.spiceLevel)}
                      </span>
                    ) : null}
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
                    {product.prepTimeMinutes ? (
                      <span className="product-card__prep">{t.prepTime(product.prepTimeMinutes)}</span>
                    ) : null}
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
        <footer className="menu-disclaimer">
          <p>{menuDisclaimer}</p>
          {session.customerMenu?.allergenMatrixUrl ? (
            <p>
              <a href={session.customerMenu.allergenMatrixUrl} rel="noopener noreferrer" target="_blank">
                {t.allergenMatrixLink}
              </a>
            </p>
          ) : null}
        </footer>
      </main>
      {orders.length > 0 && !cartCount ? (
        <div className="sticky-cart">
          <Button fullWidth onClick={() => setTrackingOpen(true)}>
            <span>{t.trackOrder}</span>
            <strong>{t.tableOrdersEyebrow(orders.length)}</strong>
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
          menuSettings={session.customerMenu}
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

type AppResolveState =
  | { status: "required" }
  | { status: "loading" }
  | { status: "invalid"; detail?: string }
  | { status: "menu-unavailable"; detail?: string }
  | { status: "error"; detail?: string }
  | { status: "ready"; session: CustomerSession; qrToken: string };

export function App({ gateway, qrToken }: AppProps) {
  const token = useMemo(
    () => (qrToken === undefined ? readQrFromLocation() : qrToken),
    [qrToken],
  );
  const activeGateway = useMemo(
    () => gateway ?? getCustomerGateway(token),
    [gateway, token],
  );
  const resolveGenerationRef = useRef(0);
  const [state, setState] = useState<AppResolveState>(() =>
    token ? { status: "loading" } : { status: "required" },
  );
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

    const generation = ++resolveGenerationRef.current;
    const locale =
      new URLSearchParams(window.location.search).get("lang") === "en" ? "en" : "tr";
    setState({ status: "loading" });

    void activeGateway
      .resolveQr(token, undefined, locale)
      .then((session) => {
        if (generation !== resolveGenerationRef.current) {
          return;
        }
        setState({ status: "ready", session, qrToken: token });
      })
      .catch((error: unknown) => {
        if (generation !== resolveGenerationRef.current) {
          return;
        }
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

    return () => {
      resolveGenerationRef.current += 1;
    };
  }, [activeGateway, token]);

  return (
    <div
      className="app-shell"
      data-menu-theme={
        state.status === "ready"
          ? state.session.customerMenu?.themeId?.trim() || "modern"
          : "modern"
      }
      data-brand-watermark={
        state.status === "ready" &&
        state.session.customerMenu?.showBrandWatermark &&
        state.session.customerMenu?.logoUrl
          ? state.session.customerMenu.brandWatermarkIntensity === "medium"
            ? "medium"
            : "soft"
          : "off"
      }
      style={
        state.status === "ready" &&
        state.session.customerMenu?.showBrandWatermark &&
        state.session.customerMenu?.logoUrl
          ? ({
              ["--brand-watermark-url" as string]: `url("${resolveProductMediaUrl(state.session.customerMenu.logoUrl)}")`,
            } as CSSProperties)
          : undefined
      }
    >
      {!online ? (
        <div className="offline-banner" role="status">
          {t.offline}
        </div>
      ) : null}
      {state.status === "required" ? (
        <main className="entry-state" id="main-content">
          <span className="entry-state__mark" aria-hidden="true">
            <img src="/pasa-mark.svg" alt="" width={56} height={56} />
          </span>
          <p className="eyebrow">Pasa</p>
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
      {state.status === "ready" ? (
        <Menu session={state.session} gateway={activeGateway} qrToken={state.qrToken} />
      ) : null}
    </div>
  );
}
