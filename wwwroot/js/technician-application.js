/* One account, with technician access granted after a real application review. */
(() => {
  "use strict";
  const $ = selector => document.querySelector(selector);
  const root = $("#technician-application");
  if (!root) return;
  const form = $("#technician-application-form");
  const state = { user: null, profile: null, serial: 0, busy: false, bound: false, categories: [] };
  const api = () => window.RepairApi;
  const fields = { specialization: "specialization", categoryid: "category", categoryids: "category", yearsofexperience: "experience", bio: "bio", address: "address", document: "document" };
  const hints = { category: "application-category-hint" };
  const node = (tag, text) => { const element = document.createElement(tag); if (text !== undefined) element.textContent = String(text); return element; };
  const statusName = value => typeof value === "number" ? ["Pending", "Approved", "Rejected"][value] || "Unknown" : String(value || "Unknown");
  function isAdmin(user) {
    return typeof user?.roles === "number" ? (user.roles & 4) === 4 : String(user?.roles || "").split(",").some(role => role.trim() === "Admin");
  }
  function clearErrors() {
    $("#application-error").hidden = true;
    form.querySelectorAll(".is-invalid").forEach(input => {
      input.classList.remove("is-invalid"); input.removeAttribute("aria-invalid"); input.removeAttribute("aria-describedby");
    });
    for (const [field, hint] of Object.entries(hints)) $("#application-" + field).setAttribute("aria-describedby", hint);
    form.querySelectorAll(".invalid-feedback").forEach(feedback => { feedback.textContent = ""; feedback.classList.remove("d-block"); });
  }
  function fieldError(field, message) {
    const input = $("#application-" + field), feedback = $("#application-" + field + "-error");
    input.classList.add("is-invalid"); input.setAttribute("aria-invalid", "true");
    input.setAttribute("aria-describedby", (hints[field] ? hints[field] + " " : "") + feedback.id);
    feedback.textContent = message; feedback.classList.add("d-block");
  }
  function showError(error, retry = false) {
    const summary = $("#application-error");
    summary.textContent = error.status === 401 ? "Your session has ended. Sign in again to continue." : error.message || "We couldn't load your application. Please try again.";
    summary.hidden = false; $("#application-retry").hidden = !retry;
    for (const [key, messages] of Object.entries(error.errors || {})) {
      const field = fields[key.split(".").pop().toLowerCase()];
      if (field) fieldError(field, Array.isArray(messages) ? messages.join(" ") : String(messages));
    }
  }
  function addingCategories() { return statusName(state.profile?.status) === "Approved"; }
  function setBusy(busy) {
    state.busy = busy; form.setAttribute("aria-busy", String(busy));
    form.querySelectorAll("input,select,textarea,button").forEach(control => { control.disabled = busy || Boolean(control.closest("[data-category-hidden]")); });
    $("#application-submit").disabled = busy || !state.categories.length;
    $("#application-submit").textContent = busy ? "Submitting…" : addingCategories() ? "Request approval" : state.profile ? "Resubmit application" : "Submit application";
  }
  function loading(busy, message = "Loading your application…") {
    $("#application-loading").hidden = !busy; $("#application-loading").textContent = message;
    root.setAttribute("aria-busy", String(busy));
  }
  function detail(label, value) {
    const row = node("div"); row.append(node("dt", label), node("dd", value || "Not provided")); return row;
  }
  function renderStatus() {
    const profile = state.profile, status = statusName(profile.status);
    $("#technician-application-title").textContent = "Technician application";
    $("#application-status").open = false;
    $("#application-status").hidden = false; form.hidden = true;
    const badge = $("#application-status-badge");
    const pending = (profile.categories || []).filter(c => statusName(c.status) === "Pending");
    badge.textContent = status === "Approved" && pending.length ? "Approved · New categories pending" : status;
    badge.className = "status-badge " + (status === "Approved" ? "success" : status === "Pending" ? "warning" : "neutral");
    $("#application-status-message").textContent = {
      Pending: "Your application is awaiting administrator review. You can keep using your client account.",
      Approved: "Your application has been approved. Technician access is included in your account.",
      Rejected: "Update your application and send it for review again."
    }[status] || "Your application status is unavailable. Please try again.";
    const details = $("#application-details");
    details.replaceChildren(detail("Specialization", profile.specialization), 
      detail("Experience", Number(profile.yearsOfExperience) + " years"), detail("Address", profile.address));
    for (const category of profile.categories || []) {
      details.append(detail(category.name, statusName(category.status) + (category.rejectionReason ? " · " + category.rejectionReason : "")));
    }
    if (profile.bio) details.append(detail("About your work", profile.bio));
    if (profile.reviewedAt) {
      const reviewed = new Date(profile.reviewedAt);
      if (!Number.isNaN(reviewed.getTime())) details.append(detail("Reviewed", reviewed.toLocaleDateString(undefined, { year: "numeric", month: "short", day: "numeric" })));
    }
    $("#application-rejection").hidden = status !== "Rejected" || !profile.rejectionReason;
    $("#application-rejection-reason").textContent = profile.rejectionReason || "";
    $("#application-edit").hidden = status === "Pending";
    $("#application-edit").textContent = status === "Approved" ? "Add categories" : "Update and resubmit";
    const documents = $("#application-document-list"); documents.replaceChildren();
    for (const document of profile.documents || []) {
      const item = node("li"); let url;
      try {
        const candidate = new URL(document.downloadUrl, location.origin);
        if (document.downloadUrl && candidate.origin === location.origin && ["http:", "https:"].includes(candidate.protocol)) url = candidate;
      } catch {}
      if (url) { const link = node("a", document.fileName || "Download document"); link.href = url.href; item.append(link); }
      else item.append(node("span", document.fileName || "Supporting document"));
      documents.append(item);
    }
    $("#application-documents").hidden = !documents.children.length;
  }
  async function showEditor(serial = ++state.serial) {
    if (!state.user || state.busy) return;
    clearErrors(); $("#application-retry").hidden = true;
    $("#technician-application-title").textContent = addingCategories() ? "Add service categories" : state.profile ? "Update application" : "Become a technician";
    $("#application-status").hidden = true; form.hidden = true; loading(true, "Loading service categories…");
    try {
      const categories = await api().get("/api/categories");
      if (serial !== state.serial) return;
      state.categories = (Array.isArray(categories) ? categories : []).filter(category => category.isActive !== false);
      form.reset();
      const profile = state.profile;
      for (const field of form.querySelectorAll(":scope > div:not(.service-field):not(.form-actions)")) {
        field.hidden = addingCategories();
        field.toggleAttribute("data-category-hidden", addingCategories());
      }
      if (addingCategories()) {
        const existing = new Set(profile.categories.filter(c => statusName(c.status) !== "Rejected").map(c => c.id));
        state.categories = state.categories.filter(c => !existing.has(c.id));
      }
      const selected = new Set((profile?.categories || []).map(c => c.id));
      const list = $("#application-options");
      list.replaceChildren();
      for (const category of state.categories) {
        const label = node("label");
        const input = node("input");
        input.type = "checkbox"; input.name = "categoryIds"; input.value = category.id;
        input.checked = !addingCategories() && selected.has(category.id);
        label.append(input, node("span", category.name)); list.append(label);
      }
      $("#application-services").open = false;
      refreshPicker();
      if (profile) {
        $("#application-specialization").value = profile.specialization || "";
        $("#application-experience").value = String(profile.yearsOfExperience ?? "");
        $("#application-address").value = profile.address || ""; $("#application-bio").value = profile.bio || "";

      }
      $("#application-category-hint").textContent = !state.categories.length ? "No service categories are available yet." : statusName(profile?.status) === "Approved" ? "Your approved services stay available while new categories are reviewed." : "";
      $("#application-cancel").hidden = !profile; form.hidden = false; setBusy(false);
    } catch (error) { if (serial === state.serial) showError(error, true); }
    finally { if (serial === state.serial) loading(false); }
  }
  async function loadApplication() {
    if (!state.user || state.busy) return;
    const serial = ++state.serial;
    clearErrors(); $("#application-retry").hidden = true; form.hidden = true; $("#application-status").hidden = true; loading(true);
    try {
      const profile = await api().get("/api/technician-application");
      if (serial !== state.serial) return;
      state.profile = profile;
      renderStatus();
    } catch (error) {
      if (serial !== state.serial) return;
      if (error.status === 404) { state.profile = null; await showEditor(serial); }
      else showError(error, true);
    } finally { if (serial === state.serial) loading(false); }
  }
  async function submitApplication(event) {
    event.preventDefault();
    if (!state.user || state.busy) return;
    clearErrors();
    if (!form.reportValidity()) return;
    const file = $("#application-document").files[0];
    if (file && (!/\.(pdf|jpe?g|png)$/i.test(file.name) || file.size > 5 * 1024 * 1024 || file.size === 0)) {
      fieldError("document", "Choose a PDF, JPG or PNG file up to 5 MB."); $("#application-document").focus(); return;
    }
    const checked = form.querySelectorAll('[name="categoryIds"]:checked');
    if (!checked.length) {
      fieldError("category", "Choose at least one service category.");
      $("#application-services").open = true; $("#application-search").focus(); return;
    }
    const payload = new FormData();
    payload.append("specialization", $("#application-specialization").value.trim());
    for (const option of checked) payload.append("categoryIds", option.value);
    payload.append("yearsOfExperience", String(Number($("#application-experience").value)));
    for (const field of addingCategories() ? [] : ["bio", "address"]) {
      const value = $("#application-" + field).value.trim();
      if (!value) {
        fieldError(field, field === "bio" ? "Describe your work." : "Enter your address.");
        $("#application-" + field).focus();
        return;
      }
      payload.append(field, value);
    }
    if (file) payload.append("document", file);
    const serial = state.serial; setBusy(true);
    try {
      const result = addingCategories()
        ? await api().post("/api/technician-application/categories", { categoryIds: [...checked].map(option => Number(option.value)) })
        : await api().postForm("/api/technician-application", payload);
      if (serial !== state.serial) return;
      state.profile = result; form.reset(); renderStatus();
      window.RepairUi?.toast?.("Application submitted for review."); $("#application-status summary").focus();
    } catch (error) {
      if (serial !== state.serial) return;
      showError(error);
      if (error.status === 409) { state.busy = false; await loadApplication(); }
    } finally {
      if (serial === state.serial) {
        setBusy(false);
        if (!$("#application-error").hidden) ($("#technician-application-form .is-invalid") || $("#application-error")).focus();
      }
    }
  }
  function refreshPicker() {
    const term = $("#application-search").value.trim().toLowerCase();
    const names = [];
    const labels = [...$("#application-options").children];
    for (const label of labels) {
      label.hidden = !label.textContent.toLowerCase().includes(term);
      if (label.querySelector("input").checked) names.push(label.textContent);
    }
    $("#application-selection").textContent = names.join(", ") || "Choose categories";
    $("#application-no-match").hidden = labels.some(label => !label.hidden);
  }
  function bind() {
    if (state.bound) return;
    state.bound = true; form.addEventListener("submit", submitApplication);
    const picker = $("#application-services"), search = $("#application-search");
    search.addEventListener("input", refreshPicker);
    picker.addEventListener("change", refreshPicker);
    picker.addEventListener("toggle", () => {
      if (picker.open) search.focus();
      else { search.value = ""; refreshPicker(); }
    });
    picker.addEventListener("keydown", event => {
      if (event.key === "Enter" && event.target === search) event.preventDefault();
      if (event.key === "Escape") { picker.open = false; $("#application-category").focus(); }
    });
    document.addEventListener("click", event => { if (!picker.contains(event.target)) picker.open = false; });
    $("#application-retry").addEventListener("click", loadApplication);
    $("#application-edit").addEventListener("click", () => showEditor());
    $("#application-cancel").addEventListener("click", () => { if (!state.busy && state.profile) { state.serial++; form.reset(); clearErrors(); renderStatus(); } });
  }
  window.RepairTechnicianApplication = Object.freeze({
    async init(user) {
      state.serial++; state.user = user && !isAdmin(user) ? user : null; state.profile = null; state.categories = []; state.busy = false;
      form.reset(); clearErrors(); setBusy(false);
      root.hidden = !state.user;
      if (!state.user) { $("#application-document-list").replaceChildren(); $("#application-details").replaceChildren(); $("#application-rejection-reason").textContent = ""; return; }
      bind(); await loadApplication();
    }
  });
})();
