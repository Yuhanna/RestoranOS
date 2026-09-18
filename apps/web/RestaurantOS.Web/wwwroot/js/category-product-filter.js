/**
 * Kategori → ürün bağımlı seçim (proje standardı).
 *
 * Kullanım:
 *   <select data-ros-category-filter>...</select>
 *   <select data-ros-product-select data-ros-category-source="#idOrSelector">
 *     <option value="">—</option>
 *     <option value="..." data-category-id="{guid}">Ad</option>
 *   </select>
 *
 * İsteğe bağlı: data-ros-require-category="true" → kategori yokken ürünler gizli kalır.
 */
(function () {
  function resolveCategorySelect(productSelect) {
    var source = productSelect.getAttribute("data-ros-category-source");
    if (!source) return null;
    if (source.charAt(0) === "#" || source.charAt(0) === ".") {
      return document.querySelector(source);
    }
    return document.getElementById(source);
  }

  function applyFilter(productSelect, categorySelect) {
    if (!productSelect || !categorySelect) return;
    var categoryId = (categorySelect.value || "").trim();
    var requireCategory = productSelect.getAttribute("data-ros-require-category") === "true";
    var keepValue = productSelect.value;
    var matched = false;

    Array.prototype.forEach.call(productSelect.options, function (option) {
      if (!option.value) {
        option.hidden = false;
        option.disabled = false;
        return;
      }
      var optionCategory = (option.getAttribute("data-category-id") || "").trim();
      var visible =
        (!requireCategory && !categoryId) ||
        (!!categoryId && optionCategory === categoryId);
      option.hidden = !visible;
      option.disabled = !visible;
      if (visible && option.value === keepValue) matched = true;
    });

    if (!matched) {
      productSelect.value = "";
    }
  }

  function bindPair(productSelect) {
    if (productSelect.dataset.rosCategoryBound === "1") return;
    var categorySelect = resolveCategorySelect(productSelect);
    if (!categorySelect) return;
    productSelect.dataset.rosCategoryBound = "1";
    categorySelect.addEventListener("change", function () {
      applyFilter(productSelect, categorySelect);
    });
    applyFilter(productSelect, categorySelect);
  }

  function init(root) {
    var scope = root || document;
    scope.querySelectorAll("[data-ros-product-select]").forEach(bindPair);
  }

  window.RestaurantOsCategoryProductFilter = { init: init, applyFilter: applyFilter };

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", function () {
      init(document);
    });
  } else {
    init(document);
  }
})();
