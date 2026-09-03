(() => {
  const INITIAL_PHOTO_LIMIT = 36;

  const normalize = (value) =>
    (value ?? "")
      .toLocaleLowerCase("tr-TR")
      .normalize("NFD")
      .replace(/\p{M}/gu, "");

  const tokenize = (value) => normalize(value).split(/[^a-z0-9]+/u).filter(Boolean);

  const readLibrary = (root) => {
    const scriptId = root.dataset.libraryId;
    const scriptEl = scriptId ? document.getElementById(scriptId) : null;
    const raw = scriptEl?.textContent?.trim() || root.dataset.library || "{\"categories\":[],\"photos\":[]}";

    try {
      const parsed = JSON.parse(raw);
      return {
        categories: parsed.categories ?? parsed.Categories ?? [],
        photos: parsed.photos ?? parsed.Photos ?? [],
      };
    } catch {
      return { categories: [{ id: "all", label: "Tümü" }], photos: [] };
    }
  };

  const photoPath = (photo) => photo.path ?? photo.Path ?? "";
  const photoCategory = (photo) => photo.category ?? photo.Category ?? "";
  const photoTitle = (photo) => photo.title ?? photo.Title ?? "";
  const photoAlt = (photo) => photo.alt ?? photo.Alt ?? "";
  const photoTags = (photo) => photo.tags ?? photo.Tags ?? [];
  const categoryId = (category) => category.id ?? category.Id ?? "all";
  const categoryLabel = (category) => category.label ?? category.Label ?? "";

  const scorePhoto = (photo, queryTokens, productTokens) => {
    const title = normalize(photoTitle(photo));
    const haystack = normalize(
      [photoTitle(photo), photoAlt(photo), ...photoTags(photo)].join(" "),
    );
    let score = 0;
    for (const token of queryTokens) {
      if (haystack.includes(token)) score += 4;
      if (title === token || title.startsWith(`${token} `) || title.endsWith(` ${token}`)) score += 20;
    }
    for (const token of productTokens) {
      if (haystack.includes(token)) score += 6;
      if (title === token || title.includes(token)) score += 16;
    }
    if (photoTitle(photo) && productTokens.some((token) => title.includes(token))) {
      score += 8;
    }
    // Generic tags alone should not outrank a title match.
    const generic = ["ana yemek", "restoran", "baslangic", "tatli", "icecek", "kahvalti"];
    if (generic.some((g) => haystack.includes(normalize(g))) && score < 10) {
      score = Math.max(0, score - 2);
    }
    return score;
  };

  const mountPicker = (root) => {
    const mediaBase = root.dataset.mediaBase?.replace(/\/+$/, "") ?? "";
    const library = readLibrary(root);
    const imageUrlInput = document.getElementById(root.dataset.imageUrlId ?? "");
    const imageAltInput = document.getElementById(root.dataset.imageAltId ?? "");
    const preview = document.getElementById(root.dataset.previewId ?? "");
    const nameInput = document.getElementById(root.dataset.nameInputId ?? "Name");
    const searchInput = root.querySelector("[data-stock-search]");
    const categoryHost = root.querySelector("[data-stock-categories]");
    const grid = root.querySelector("[data-stock-grid]");
    const emptyState = root.querySelector("[data-stock-empty]");

    if (!imageUrlInput || !grid || !searchInput || !categoryHost) return;

    let activeCategory = "all";
    let selectedPath = imageUrlInput.value || "";

    const syncSearchFromName = () => {
      const name = nameInput?.value?.trim() ?? "";
      if (name) {
        searchInput.value = name;
      }
    };

    const renderPreview = (path, alt) => {
      if (!preview) return;
      if (!path) {
        preview.hidden = true;
        preview.innerHTML = "";
        return;
      }
      const src = path.startsWith("http") ? path : `${mediaBase}${path}`;
      preview.hidden = false;
      preview.innerHTML = `<img src="${src}" alt="${alt ?? "Seçili ürün fotoğrafı"}" />`;
    };

    const renderCategories = () => {
      categoryHost.innerHTML = "";
      const categories = library.categories.length
        ? library.categories
        : [{ id: "all", label: "Tümü" }];

      for (const category of categories) {
        const id = categoryId(category);
        const button = document.createElement("button");
        button.type = "button";
        button.className = `chip${activeCategory === id ? " is-active" : ""}`;
        button.textContent = categoryLabel(category);
        button.setAttribute("aria-pressed", activeCategory === id ? "true" : "false");
        button.addEventListener("click", () => {
          activeCategory = id;
          renderCategories();
          renderPhotos();
        });
        categoryHost.appendChild(button);
      }
    };

    const renderPhotos = () => {
      const productTokens = tokenize(nameInput?.value ?? "");
      const query = searchInput.value.trim() || nameInput?.value?.trim() || "";
      const queryTokens = tokenize(query);
      const photos = library.photos
        .filter((photo) => activeCategory === "all" || photoCategory(photo) === activeCategory)
        .map((photo) => ({ photo, score: scorePhoto(photo, queryTokens, productTokens) }))
        .filter(({ photo, score }) => {
          if (!queryTokens.length) return true;
          return score > 0 || normalize(photoTitle(photo)).includes(normalize(query));
        })
        .sort(
          (left, right) =>
            right.score - left.score ||
            photoTitle(left.photo).localeCompare(photoTitle(right.photo), "tr"),
        );

      grid.innerHTML = "";
      const hasSearch = queryTokens.length > 0 || productTokens.length > 0;
      const visiblePhotos = hasSearch ? photos : photos.slice(0, INITIAL_PHOTO_LIMIT);

      for (const { photo } of visiblePhotos) {
        const path = photoPath(photo);
        const button = document.createElement("button");
        button.type = "button";
        button.className = `stock-photo-picker__item${selectedPath === path ? " is-selected" : ""}`;
        button.setAttribute("role", "option");
        button.setAttribute("aria-selected", selectedPath === path ? "true" : "false");
        button.title = photoTitle(photo);
        const src = path.startsWith("http") ? path : `${mediaBase}${path}`;
        button.innerHTML = `<img src="${src}" alt="${photoAlt(photo)}" loading="lazy" /><span>${photoTitle(photo)}</span>`;
        button.addEventListener("click", () => {
          selectedPath = path;
          imageUrlInput.value = path;
          if (imageAltInput && !imageAltInput.value.trim()) {
            imageAltInput.value = photoAlt(photo);
          }
          renderPreview(path, imageAltInput?.value || photoAlt(photo));
          renderPhotos();
        });
        grid.appendChild(button);
      }

      if (emptyState) {
        emptyState.hidden = visiblePhotos.length > 0;
      }

      const scrollHint = root.querySelector("[data-stock-scroll-hint]");
      if (scrollHint) {
        scrollHint.hidden = visiblePhotos.length === 0;
        scrollHint.textContent = hasSearch
          ? "Daha fazla görsel için aşağı kaydırın."
          : `İlk ${INITIAL_PHOTO_LIMIT} öneri gösteriliyor. Arama yaparak daraltın veya aşağı kaydırın.`;
      }
    };

    searchInput.addEventListener("input", renderPhotos);
    nameInput?.addEventListener("input", () => {
      syncSearchFromName();
      renderPhotos();
    });

    syncSearchFromName();

    if (selectedPath) {
      const selected = library.photos.find((photo) => photoPath(photo) === selectedPath);
      renderPreview(selectedPath, imageAltInput?.value || (selected ? photoAlt(selected) : ""));
    }

    renderCategories();
    renderPhotos();
  };

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", () => {
      document.querySelectorAll("[data-stock-photo-picker]").forEach(mountPicker);
    });
  } else {
    document.querySelectorAll("[data-stock-photo-picker]").forEach(mountPicker);
  }
})();
