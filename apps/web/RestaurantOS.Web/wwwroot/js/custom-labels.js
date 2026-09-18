(() => {
  const MAX_LABELS = 8;
  const MAX_LENGTH = 32;

  const bind = (root) => {
    if (!root || root.dataset.bound === "1") return;
    root.dataset.bound = "1";
    const list = root.querySelector("[data-custom-label-list]");
    const input = root.querySelector("[data-custom-label-input]");
    const addBtn = root.querySelector("[data-custom-label-add]");
    if (!list || !input || !addBtn) return;
    const fieldName = list.getAttribute("data-field-name") || "CustomLabels";

    const currentValues = () =>
      [...list.querySelectorAll('input[type="hidden"]')].map((el) => el.value.trim().toLocaleLowerCase("tr"));

    const addLabel = () => {
      const raw = (input.value || "").trim();
      if (!raw) return;
      const value = raw.slice(0, MAX_LENGTH).trim();
      if (!value) return;
      if (currentValues().includes(value.toLocaleLowerCase("tr"))) {
        input.value = "";
        return;
      }
      if (currentValues().length >= MAX_LABELS) {
        input.setCustomValidity("En fazla 8 etiket ekleyebilirsiniz.");
        input.reportValidity();
        input.setCustomValidity("");
        return;
      }

      const chip = document.createElement("span");
      chip.className = "custom-label-chip";
      chip.innerHTML =
        '<input type="hidden" />' +
        "<span></span>" +
        '<button type="button" class="custom-label-chip__remove" aria-label="Sil">×</button>';
      const hidden = chip.querySelector('input[type="hidden"]');
      hidden.name = fieldName;
      hidden.value = value;
      chip.querySelector("span").textContent = value;
      list.appendChild(chip);
      input.value = "";
      input.focus();
    };

    list.addEventListener("click", (event) => {
      const btn = event.target.closest(".custom-label-chip__remove");
      if (!btn) return;
      btn.closest(".custom-label-chip")?.remove();
    });

    addBtn.addEventListener("click", addLabel);
    input.addEventListener("keydown", (event) => {
      if (event.key === "Enter") {
        event.preventDefault();
        addLabel();
      }
    });
  };

  document.querySelectorAll("[data-custom-labels]").forEach(bind);
})();
