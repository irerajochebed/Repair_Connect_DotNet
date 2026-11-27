(() => {
  const form = document.querySelector("#repair-form");
  const draftKey = "repair-request-draft";

  function onSubmit(form, submit) {
    form.addEventListener("submit", async event => {
      event.preventDefault();
      const button = form.querySelector("button");
      if (button.disabled) return;
      button.disabled = true;
      let error = form.querySelector('[role="alert"]');
      if (!error) {
        error = document.createElement("p");
        error.className = "text-danger";
        error.setAttribute("role", "alert");
        form.append(error);
      }
      error.hidden = true;
      try {
        await submit(Object.fromEntries(new FormData(form)));
      } catch (failure) {
        error.textContent = failure.message;
        error.hidden = false;
        button.disabled = false;
      }
    });
  }

  if (form) {
    const picker = form.querySelector("#service-picker");
    const search = picker.querySelector("input[type=search]");
    const options = [...picker.querySelectorAll("[name=categoryIds]")];
    function showSelection() {
      const selected = options.filter(option => option.checked);
      form.querySelector("#service-summary").textContent =
        selected.map(option => option.parentElement.textContent.trim()).join(", ") || "Choose services";
    }
    function filterServices() {
      const term = search.value.trim().toLowerCase();
      for (const option of options) {
        option.parentElement.hidden = !option.parentElement.textContent.toLowerCase().includes(term);
      }
      form.querySelector("#no-service-matches").hidden = options.some(option => !option.parentElement.hidden);
    }
    picker.addEventListener("change", showSelection);
    search.addEventListener("input", filterServices);
    search.addEventListener("keydown", event => {
      if (event.key === "Enter") event.preventDefault();
    });
    picker.addEventListener("toggle", () => {
      if (picker.open) search.focus();
      else { search.value = ""; filterServices(); }
    });
    picker.addEventListener("keydown", event => {
      if (event.key === "Escape") { picker.open = false; picker.querySelector("summary").focus(); }
    });
    document.addEventListener("click", event => {
      if (!picker.contains(event.target)) picker.open = false;
    });
    if (new URLSearchParams(location.search).has("resume")) {
      const draft = JSON.parse(sessionStorage.getItem(draftKey) || "{}");
      for (const [name, value] of Object.entries(draft)) {
        const control = form.elements.namedItem(name);
        if (name === "categoryId" || name === "categoryIds") {
          const saved = [value].flat().map(String);
          options.forEach(option => { option.checked = saved.includes(option.value); });
        } else if (control) {
          control.value = value;
        }
      }
    }
    showSelection();
    onSubmit(form, async data => {
      data.categoryIds = options.filter(option => option.checked).map(option => Number(option.value));
      if (!data.categoryIds.length) {
        picker.open = true;
        throw new Error("Select at least one service.");
      }
      if (form.dataset.signedIn === "no") {
        sessionStorage.setItem(draftKey, JSON.stringify(data));
        location.assign("/Account?returnUrl=" + encodeURIComponent("/Explore?resume=1"));
        return;
      }
      data.preferredDate ||= null;
      const request = await RepairApi.post("/api/repair-requests", data);
      sessionStorage.removeItem(draftKey);
      location.assign("/Dashboard?submitted=" + request.id + "#request-" + request.id);
    });
  }

  document.querySelectorAll("[data-repair-action]").forEach(actionForm => {
    onSubmit(actionForm, async data => {
      if ("rating" in data) data.rating = Number(data.rating);
      if ("scheduledDate" in data) data.scheduledDate ||= null;
      await RepairApi.post(actionForm.dataset.repairAction, data);
      if (actionForm.hasAttribute("data-after-accept")) location.assign("/Dashboard?panel=jobs");
      else location.reload();
    });
  });
})();
