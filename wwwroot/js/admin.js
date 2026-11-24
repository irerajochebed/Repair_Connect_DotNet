/* Live administration UI. Records are fetched from the authenticated API only. */
(() => {
  "use strict";
  const $ = selector => document.querySelector(selector);
  const root = $("#admin-workspace");
  if (!root) return;

  const state = {
    user: null, bound: false, usersPage: 1, techniciansPage: 1, categoriesPage: 1,
    usersSerial: 0, techniciansSerial: 0, categoriesSerial: 0, categoryEditorSerial: 0, metricsSerial: 0, reviewSerial: 0, createSerial: 0,
    managedUser: null, technician: null, editingCategoryId: null, deletingCategory: null,
    createBusy: false, userBusy: false, reviewBusy: false, categoryBusy: false, categoryDeleteBusy: false
  };
  const api = () => window.RepairApi;
  const toast = message => window.RepairUi?.toast?.(message);
  const node = (tag, className, value) => {
    const result = document.createElement(tag);
    if (className) result.className = className;
    if (value !== undefined) result.textContent = String(value);
    return result;
  };
  function roleNames(value) {
    if (typeof value === "number") return [[1, "Client"], [2, "Technician"], [4, "Admin"]].filter(([flag]) => (value & flag) === flag).map(([, name]) => name).join(", ") || "None";
    return String(value || "None").split(",").map(part => part.trim()).join(", ");
  }
  function enumName(value, names) { return typeof value === "number" ? names[value] || String(value) : String(value ?? ""); }
  const accountStatus = value => enumName(value, ["Active", "Inactive"]);
  const verification = value => enumName(value, ["Pending", "Approved", "Rejected"]);
  const date = value => {
    if (!value) return "—";
    const parsed = new Date(value);
    return Number.isNaN(parsed.getTime()) ? "—" : parsed.toLocaleDateString(undefined, { year: "numeric", month: "short", day: "numeric" });
  };
  function errorMessage(error) {
    if (error?.status === 401) return "Your session has ended. Sign in again to continue.";
    if (error?.status === 403) return "Your account does not have permission for this action.";
    return error?.message || "We couldn't load this information. Please try again.";
  }
  function showError(id, error) {
    const target = $(id);
    const copy = target.querySelector("span") || target;
    copy.textContent = errorMessage(error);
    target.hidden = false;
  }
  function clearError(id) { $(id).hidden = true; }
  function badge(value) {
    const tone = ["Active", "Approved"].includes(value) ? "success" : value === "Pending" ? "warning" : "neutral";
    return node("span", "status-badge " + tone, value);
  }
  function fieldRow(label, value) {
    const row = node("div");
    row.append(node("dt", "", label), node("dd", "", value || "—"));
    return row;
  }
  function busyButton(button, busy, label) {
    if (!button.dataset.idleLabel) button.dataset.idleLabel = button.textContent.trim();
    button.disabled = busy;
    button.textContent = busy ? label : button.dataset.idleLabel;
  }
  function toggleForm(form, busy) {
    form.querySelectorAll("input,select,textarea,button").forEach(control => { control.disabled = busy; });
  }
  function emptyState(id, title, copy) {
    const host = $(id);
    host.classList.add("admin-empty");
    host.replaceChildren(node("strong", "", title), node("span", "", copy));
  }
  function loadingState(prefix) {
    const target = $("#admin-" + prefix + "-state");
    target.classList.remove("admin-empty");
    target.textContent = "Loading " + ({ users: "accounts", technicians: "applications", categories: "categories" }[prefix]) + "…";
    $("#admin-" + prefix + "-results").setAttribute("aria-busy", "true");
    $("#admin-" + prefix + "-body").replaceChildren();
    $("#admin-" + prefix + "-prev").disabled = true;
    $("#admin-" + prefix + "-next").disabled = true;
    $("#admin-" + prefix + "-count").textContent = "";
    clearError("#admin-" + prefix + "-error");
  }
  function pagination(prefix, result, page) {
    const count = Number(result.totalCount) || 0;
    const start = count ? (page - 1) * 10 + 1 : 0;
    const end = count ? Math.min(page * 10, count) : 0;
    $("#admin-" + prefix + "-count").textContent = count ? start + "–" + end + " of " + count : "0 results";
    $("#admin-" + prefix + "-prev").disabled = !result.hasPreviousPage;
    $("#admin-" + prefix + "-next").disabled = !result.hasNextPage;
  }
  function isCurrentUser(id) { return Number(id) === Number(state.user?.id); }
  function closeSidebar() { bootstrap.Offcanvas.getInstance($("#admin-sidebar"))?.hide(); }
  function syncNavigation(tab) {
    $("#admin-current-section").textContent = tab.dataset.sectionLabel;
    $("#main-content").scrollTop = 0;
    closeSidebar();
  }
  function loadSection(tab) {
    if (tab.id === "admin-users-tab") return loadUsers(1);
    if (tab.id === "admin-technicians-tab") return loadTechnicians(1);
    if (tab.id === "admin-categories-tab") return loadCategories(1);
    return loadMetrics();
  }

  async function loadMetrics() {
    const serial = ++state.metricsSerial;
    clearError("#admin-metrics-error");
    $("#admin-metrics").setAttribute("aria-busy", "true");
    try {
      const result = await api().get("/api/admin/dashboard");
      if (serial !== state.metricsSerial) return;
      root.querySelectorAll("[data-admin-metric]").forEach(element => {
        const value = Number(result[element.dataset.adminMetric]);
        element.textContent = Number.isFinite(value) ? value.toLocaleString() : "—";
      });
    } catch (error) {
      if (serial === state.metricsSerial) showError("#admin-metrics-error", error);
    } finally {
      if (serial === state.metricsSerial) $("#admin-metrics").setAttribute("aria-busy", "false");
    }
  }
  async function loadUsers(page = state.usersPage) {
    const serial = ++state.usersSerial;
    state.usersPage = Math.max(1, page);
    loadingState("users");
    const query = new URLSearchParams({ page: state.usersPage, pageSize: 10 });
    for (const [key, id] of [["search", "#admin-user-search"], ["role", "#admin-user-role"], ["status", "#admin-user-status"]]) {
      const value = $(id).value.trim();
      if (value) query.set(key, value);
    }
    try {
      const result = await api().get("/api/admin/users?" + query);
      if (serial !== state.usersSerial) return;
      if (result.totalPages > 0 && state.usersPage > result.totalPages) { await loadUsers(result.totalPages); return; }
      const items = result.items || [];
      $("#admin-users-state").replaceChildren();
      if (!items.length) emptyState("#admin-users-state", "No accounts found", "Try a different name, role or status.");
      const body = $("#admin-users-body");
      items.forEach(user => {
        const row = node("tr");
        const identity = node("td");
        identity.append(node("strong", "", user.displayName), node("span", "admin-secondary", user.email));
        if (user.phoneNumber) identity.append(node("span", "admin-secondary", user.phoneNumber));
        const roles = node("td", "", roleNames(user.roles)); roles.dataset.label = "Role";
        const status = node("td"); status.dataset.label = "Status"; status.append(badge(accountStatus(user.status)));
        const joined = node("td", "", date(user.createdAt)); joined.dataset.label = "Joined";
        const actions = node("td"), manage = node("button", "btn btn-quiet btn-sm", "Manage");
        manage.type = "button"; manage.setAttribute("aria-label", "Manage " + user.displayName);
        manage.addEventListener("click", () => openUser(user));
        actions.append(manage); row.append(identity, roles, status, joined, actions); body.append(row);
      });
      pagination("users", result, state.usersPage);
    } catch (error) {
      if (serial === state.usersSerial) { $("#admin-users-state").replaceChildren(); showError("#admin-users-error", error); }
    } finally {
      if (serial === state.usersSerial) $("#admin-users-results").setAttribute("aria-busy", "false");
    }
  }
  async function loadTechnicians(page = state.techniciansPage) {
    const serial = ++state.techniciansSerial;
    state.techniciansPage = Math.max(1, page);
    loadingState("technicians");
    const query = new URLSearchParams({ page: state.techniciansPage, pageSize: 10, status: $("#admin-technician-status").value });
    const search = $("#admin-technician-search").value.trim();
    if (search) query.set("search", search);
    try {
      const result = await api().get("/api/admin/technicians?" + query);
      if (serial !== state.techniciansSerial) return;
      if (result.totalPages > 0 && state.techniciansPage > result.totalPages) { await loadTechnicians(result.totalPages); return; }
      const items = result.items || [];
      $("#admin-technicians-state").replaceChildren();
      if (!items.length) emptyState("#admin-technicians-state", "No applications in this view", "Applications will appear here when they match your filters.");
      const body = $("#admin-technicians-body");
      items.forEach(technician => {
        const row = node("tr"), identity = node("td");
        identity.append(node("strong", "", technician.displayName), node("span", "admin-secondary", technician.specialization));
        const experience = node("td", "", technician.yearsOfExperience + " years"); experience.dataset.label = "Experience";
        const documents = node("td", "", technician.documentCount); documents.dataset.label = "Documents";
        const status = node("td"); status.dataset.label = "Verification"; status.append(badge(verification(technician.verificationStatus)));
        const actions = node("td"), review = node("button", "btn btn-quiet btn-sm", "Review");
        review.type = "button"; review.setAttribute("aria-label", "Review " + technician.displayName);
        review.addEventListener("click", () => openReview(technician.userId));
        actions.append(review); row.append(identity, experience, documents, status, actions); body.append(row);
      });
      pagination("technicians", result, state.techniciansPage);
    } catch (error) {
      if (serial === state.techniciansSerial) { $("#admin-technicians-state").replaceChildren(); showError("#admin-technicians-error", error); }
    } finally {
      if (serial === state.techniciansSerial) $("#admin-technicians-results").setAttribute("aria-busy", "false");
    }
  }
  async function loadCategories(page = state.categoriesPage) {
    const serial = ++state.categoriesSerial;
    state.categoriesPage = Math.max(1, page);
    loadingState("categories");
    const query = new URLSearchParams({ page: state.categoriesPage, pageSize: 10 });
    const search = $("#admin-category-search").value.trim();
    const isActive = $("#admin-category-status").value;
    if (search) query.set("search", search);
    if (isActive !== "") query.set("isActive", isActive);
    try {
      const result = await api().get("/api/admin/categories?" + query);
      if (serial !== state.categoriesSerial) return;
      if (state.categoriesPage > Math.max(1, result.totalPages)) { await loadCategories(Math.max(1, result.totalPages)); return; }
      const items = result.items || [];
      $("#admin-categories-state").replaceChildren();
      if (!items.length) emptyState("#admin-categories-state", "No service categories found", "Try another search or status, or add a new category.");
      const body = $("#admin-categories-body");
      items.forEach(category => {
        const row = node("tr"), details = node("td");
        details.append(node("strong", "", category.name), node("span", "admin-secondary admin-category-description", category.description || "No description added."));
        const status = node("td"); status.dataset.label = "Status";
        status.append(badge(category.isActive ? "Active" : "Inactive"));
        const actions = node("td"), group = node("div", "admin-category-actions");
        const edit = node("button", "btn btn-quiet btn-sm", "Edit");
        edit.type = "button"; edit.setAttribute("aria-label", "Edit " + category.name);
        edit.addEventListener("click", () => openCategory(category.id));
        const remove = node("button", "btn btn-quiet btn-sm text-danger", "Delete");
        remove.type = "button"; remove.setAttribute("aria-label", "Delete " + category.name);
        remove.addEventListener("click", () => openCategoryDelete(category));
        group.append(edit, remove); actions.append(group); row.append(details, status, actions); body.append(row);
      });
      pagination("categories", result, state.categoriesPage);
    } catch (error) {
      if (serial === state.categoriesSerial) { $("#admin-categories-state").replaceChildren(); showError("#admin-categories-error", error); }
    } finally {
      if (serial === state.categoriesSerial) $("#admin-categories-results").setAttribute("aria-busy", "false");
    }
  }
  function clearCategoryErrors() {
    clearError("#admin-category-error");
    $("#admin-category-form").querySelectorAll(".is-invalid").forEach(input => { input.classList.remove("is-invalid"); input.removeAttribute("aria-invalid"); input.removeAttribute("aria-describedby"); });
    $("#admin-category-form").querySelectorAll(".invalid-feedback").forEach(feedback => { feedback.textContent = ""; feedback.classList.remove("d-block"); });
  }
  function categoryErrors(error) {
    showError("#admin-category-error", error);
    const fields = { name: "name", description: "description", isactive: "active" };
    const errors = { ...(error?.errors || {}) };
    if (error?.status === 409 && !Object.keys(errors).length) errors.Name = [errorMessage(error)];
    for (const [key, messages] of Object.entries(errors)) {
      const field = fields[key.split(".").pop().toLowerCase()];
      if (!field) continue;
      const input = $("#admin-category-" + field), feedback = $("#admin-category-" + field + "-error");
      feedback.textContent = Array.isArray(messages) ? messages.join(" ") : String(messages);
      feedback.classList.add("d-block"); input.classList.add("is-invalid");
      input.setAttribute("aria-invalid", "true"); input.setAttribute("aria-describedby", feedback.id);
    }
  }
  async function openCategory(id = null) {
    if (state.categoryBusy) return;
    const serial = ++state.categoryEditorSerial;
    state.editingCategoryId = id;
    clearCategoryErrors();
    $("#admin-category-form").reset();
    $("#admin-category-title").textContent = id === null ? "Add service category" : "Edit service category";
    $("#admin-category-save").textContent = id === null ? "Add category" : "Save changes";
    $("#admin-category-save").dataset.idleLabel = $("#admin-category-save").textContent;
    $("#admin-category-retry").hidden = true;
    $("#admin-category-form").hidden = id !== null;
    $("#admin-category-loading").hidden = id === null;
    bootstrap.Modal.getOrCreateInstance($("#admin-category-modal")).show();
    if (id === null) return;
    try {
      const category = await api().get("/api/admin/categories/" + id);
      if (serial !== state.categoryEditorSerial) return;
      $("#admin-category-name").value = category.name || "";
      $("#admin-category-description").value = category.description || "";
      $("#admin-category-active").checked = category.isActive === true;
      $("#admin-category-form").hidden = false;
    } catch (error) {
      if (serial === state.categoryEditorSerial) { showError("#admin-category-error", error); $("#admin-category-retry").hidden = false; }
    } finally {
      if (serial === state.categoryEditorSerial) $("#admin-category-loading").hidden = true;
    }
  }
  async function saveCategory(event) {
    event.preventDefault();
    if (state.categoryBusy) return;
    clearCategoryErrors();
    const form = $("#admin-category-form");
    if (!form.reportValidity()) return;
    const id = state.editingCategoryId, serial = state.categoryEditorSerial;
    const payload = { name: $("#admin-category-name").value.trim(), description: $("#admin-category-description").value.trim() || null, isActive: $("#admin-category-active").checked };
    state.categoryBusy = true; toggleForm(form, true); busyButton($("#admin-category-save"), true, "Saving…");
    try {
      if (id === null) await api().post("/api/admin/categories", payload);
      else await api().put("/api/admin/categories/" + id, payload);
      if (serial !== state.categoryEditorSerial) return;
      state.categoryBusy = false;
      bootstrap.Modal.getInstance($("#admin-category-modal"))?.hide();
      toast(id === null ? "Service category added." : "Service category updated.");
      await loadCategories(id === null ? 1 : state.categoriesPage);
    } catch (error) {
      if (serial === state.categoryEditorSerial) categoryErrors(error);
    } finally {
      state.categoryBusy = false; toggleForm(form, false); busyButton($("#admin-category-save"), false);
      if (serial === state.categoryEditorSerial) $("#admin-category-form .is-invalid")?.focus();
    }
  }
  function openCategoryDelete(category) {
    if (state.categoryDeleteBusy) return;
    state.deletingCategory = category;
    $("#admin-category-delete-name").textContent = category.name;
    $("#admin-category-delete-alternative").hidden = true;
    clearError("#admin-category-delete-error");
    bootstrap.Modal.getOrCreateInstance($("#admin-category-delete-modal")).show();
  }
  async function deleteCategory() {
    if (state.categoryDeleteBusy || !state.deletingCategory) return;
    const category = state.deletingCategory;
    state.categoryDeleteBusy = true; clearError("#admin-category-delete-error");
    busyButton($("#admin-category-delete-confirm"), true, "Deleting…");
    $("#admin-category-delete-alternative").hidden = true;
    try {
      await api().delete("/api/admin/categories/" + category.id);
      if (state.deletingCategory !== category) return;
      state.categoryDeleteBusy = false;
      bootstrap.Modal.getInstance($("#admin-category-delete-modal"))?.hide();
      toast("Service category deleted.");
      await loadCategories();
    } catch (error) {
      if (state.deletingCategory !== category) return;
      if (error.status === 409) {
        showError("#admin-category-delete-error", { message: "This category is used by existing records. Deactivate it instead to keep those records intact." });
        $("#admin-category-delete-alternative").hidden = false;
      } else showError("#admin-category-delete-error", error);
    } finally { state.categoryDeleteBusy = false; busyButton($("#admin-category-delete-confirm"), false); }
  }
  function openUser(user) {
    state.managedUser = user;
    clearError("#admin-user-error");
    $("#admin-user-title").textContent = user.displayName;
    const details = node("dl", "profile-details");
    details.append(fieldRow("Email", user.email), fieldRow("Phone", user.phoneNumber), fieldRow("Role", roleNames(user.roles)), fieldRow("Joined", date(user.createdAt)));
    $("#admin-user-summary").replaceChildren(details);
    $("#admin-manage-status").value = accountStatus(user.status);
    $("#admin-self-note").hidden = !isCurrentUser(user.id);
    $("#admin-manage-status").querySelector('[value="Inactive"]').disabled = isCurrentUser(user.id);
    $("#admin-user-danger").hidden = isCurrentUser(user.id) || accountStatus(user.status) === "Inactive";
    bootstrap.Modal.getOrCreateInstance($("#admin-user-modal")).show();
  }
  async function updateUser(event) {
    event.preventDefault();
    if (state.userBusy || !state.managedUser) return;
    const user = state.managedUser;
    state.userBusy = true; clearError("#admin-user-error");
    toggleForm($("#admin-user-form"), true);
    busyButton($("#admin-user-save"), true, "Saving…");
    $("#admin-user-deactivate").disabled = true;
    try {
      await api().put("/api/admin/users/" + user.id + "/status", { status: $("#admin-manage-status").value });
      state.userBusy = false;
      bootstrap.Modal.getInstance($("#admin-user-modal"))?.hide();
      toast("Account status updated.");
      await Promise.all([loadUsers(), loadMetrics()]);
    } catch (error) { showError("#admin-user-error", error); }
    finally {
      state.userBusy = false; toggleForm($("#admin-user-form"), false);
      busyButton($("#admin-user-save"), false); $("#admin-user-deactivate").disabled = false;
      $("#admin-manage-status").querySelector('[value="Inactive"]').disabled = isCurrentUser(user.id);
    }
  }
  async function deactivateUser() {
    if (state.userBusy || !state.managedUser || isCurrentUser(state.managedUser.id)) return;
    const user = state.managedUser;
    if (!confirm("Deactivate " + user.displayName + "? They will lose account access; their records will be kept.")) return;
    state.userBusy = true; clearError("#admin-user-error");
    toggleForm($("#admin-user-form"), true); busyButton($("#admin-user-deactivate"), true, "Deactivating…");
    try {
      await api().delete("/api/admin/users/" + user.id);
      state.userBusy = false;
      bootstrap.Modal.getInstance($("#admin-user-modal"))?.hide();
      toast("Account deactivated.");
      await Promise.all([loadUsers(), loadMetrics()]);
    } catch (error) { showError("#admin-user-error", error); }
    finally {
      state.userBusy = false; toggleForm($("#admin-user-form"), false); busyButton($("#admin-user-deactivate"), false);
      $("#admin-manage-status").querySelector('[value="Inactive"]').disabled = isCurrentUser(user.id);
    }
  }
  function clearCreateErrors() {
    clearError("#admin-create-error");
    $("#admin-create-form").querySelectorAll(".is-invalid").forEach(input => { input.classList.remove("is-invalid"); input.removeAttribute("aria-invalid"); input.removeAttribute("aria-describedby"); });
    $("#admin-create-form").querySelectorAll(".invalid-feedback").forEach(item => { item.textContent = ""; item.classList.remove("d-block"); });
    $("#admin-create-password").setAttribute("aria-describedby", "admin-password-hint");
  }
  function createErrors(error) {
    showError("#admin-create-error", error);
    const fields = { displayname: "name", email: "email", phonenumber: "phone", password: "password" };
    const errors = { ...(error?.errors || {}) };
    if (error?.status === 409 && !Object.keys(errors).length) errors.Email = [errorMessage(error)];
    for (const [key, messages] of Object.entries(errors)) {
      const field = fields[key.split(".").pop().toLowerCase()];
      if (!field) continue;
      const input = $("#admin-create-" + field), feedback = $("#admin-create-" + field + "-error");
      feedback.textContent = Array.isArray(messages) ? messages.join(" ") : String(messages);
      feedback.classList.add("d-block");
      input.classList.add("is-invalid"); input.setAttribute("aria-invalid", "true");
      input.setAttribute("aria-describedby", (field === "password" ? "admin-password-hint " : "") + feedback.id);
    }
  }
  async function createUser(event) {
    event.preventDefault();
    if (state.createBusy || !state.user) return;
    clearCreateErrors();
    const form = $("#admin-create-form");
    if (!form.reportValidity()) return;
    const payload = {
      displayName: $("#admin-create-name").value.trim(),
      email: $("#admin-create-email").value.trim(),
      phoneNumber: $("#admin-create-phone").value.trim() || null,
      password: $("#admin-create-password").value
    };
    const serial = ++state.createSerial;
    $("#admin-create-password").value = "";
    state.createBusy = true; toggleForm(form, true);
    busyButton($("#admin-create-submit"), true, "Creating…");
    try {
      await api().post("/api/admin/users", payload);
      if (serial !== state.createSerial) return;
      state.createBusy = false;
      bootstrap.Modal.getInstance($("#admin-create-modal"))?.hide();
      form.reset();
      toast("User account created.");
      await Promise.all([loadUsers(1), loadMetrics()]);
    } catch (error) { if (serial === state.createSerial) createErrors(error); }
    finally {
      payload.password = "";
      if (serial === state.createSerial) {
        state.createBusy = false; toggleForm(form, false); busyButton($("#admin-create-submit"), false);
        $("#admin-create-form .is-invalid:not(:disabled)")?.focus();
      }
    }
  }
  function renderReview(technician) {
    const host = $("#admin-review-content");
    const header = node("div", "admin-profile-header"), copy = node("div");
    copy.append(node("h3", "", technician.displayName), node("p", "text-primary", technician.specialization));
    header.append(copy);
    const details = node("dl", "profile-details");
    details.append(
      fieldRow("Email", technician.email), fieldRow("Phone", technician.phoneNumber),
      fieldRow("Experience", technician.yearsOfExperience + " years"), fieldRow("Service area", technician.address),
      fieldRow("Verification", verification(technician.verificationStatus)),
      fieldRow("Services", (technician.categories || []).join(", "))
    );
    if (technician.reviewedByName) details.append(fieldRow("Reviewed by", technician.reviewedByName), fieldRow("Reviewed on", date(technician.reviewedAt)));
    host.replaceChildren(header);
    if (technician.bio) host.append(node("p", "admin-review-bio", technician.bio));
    host.append(details);
    if (technician.rejectionReason) host.append(node("p", "info-strip mt-3", "Rejection reason: " + technician.rejectionReason));
    host.append(node("h4", "admin-section-title", "Supporting documents"));
    const documents = technician.documents || [];
    if (!documents.length) host.append(node("p", "admin-document-note", "No supporting documents have been added. A technician needs at least one document before approval."));
    else {
      const list = node("ul", "admin-document-list");
      documents.forEach(document => {
        const item = node("li");
        let url;
        try { const candidate = new URL(document.downloadUrl, location.origin); if (document.downloadUrl && candidate.origin === location.origin && ["http:", "https:"].includes(candidate.protocol)) url = candidate; } catch {}
        if (url) { const link = node("a", "text-link", document.fileName || "View document"); link.href = url.href; item.append(link); }
        else item.append(node("span", "", document.fileName || "Supporting document"));
        item.append(node("small", "", date(document.uploadedAt))); list.append(item);
      });
      host.append(list);
      if (documents.some(document => !document.downloadUrl)) host.append(node("p", "admin-document-note", "Document names are available. File downloads have not been enabled."));
    }
    host.hidden = false;
    $("#admin-review-actions").hidden = false;
    $("#admin-review-approve").disabled = documents.length === 0 || verification(technician.verificationStatus) === "Approved";
    $("#admin-review-approve").textContent = verification(technician.verificationStatus) === "Approved" ? "Already approved" : "Approve technician";
  }
  async function openReview(userId) {
    const serial = ++state.reviewSerial;
    state.technician = null;
    $("#admin-review-loading").hidden = false; $("#admin-review-content").hidden = true;
    $("#admin-review-actions").hidden = true; $("#admin-reject-form").hidden = true;
    $("#admin-reject-reason").value = ""; clearError("#admin-review-error");
    bootstrap.Modal.getOrCreateInstance($("#admin-review-modal")).show();
    try {
      const technician = await api().get("/api/admin/technicians/" + userId);
      if (serial !== state.reviewSerial) return;
      state.technician = technician; renderReview(technician);
    } catch (error) {
      if (serial !== state.reviewSerial) return;
      showError("#admin-review-error", error);
      const retry = node("button", "btn btn-quiet btn-sm mt-2", "Try again");
      retry.type = "button"; retry.addEventListener("click", () => openReview(userId));
      $("#admin-review-error").append(retry);
    } finally { if (serial === state.reviewSerial) $("#admin-review-loading").hidden = true; }
  }
  async function decideTechnician(approve) {
    if (state.reviewBusy || !state.technician) return;
    const reason = $("#admin-reject-reason").value.trim();
    if (!approve && !reason) { $("#admin-reject-reason").setCustomValidity("Please explain the rejection."); $("#admin-reject-reason").reportValidity(); return; }
    const technician = state.technician;
    state.reviewBusy = true; clearError("#admin-review-error");
    $("#admin-review-approve").disabled = true; $("#admin-review-reject").disabled = true;
    toggleForm($("#admin-reject-form"), true);
    const button = approve ? $("#admin-review-approve") : $("#admin-reject-submit");
    busyButton(button, true, approve ? "Approving…" : "Rejecting…");
    try {
      await api().put("/api/admin/technicians/" + technician.userId + (approve ? "/approve" : "/reject"), approve ? undefined : { reason });
      state.reviewBusy = false;
      bootstrap.Modal.getInstance($("#admin-review-modal"))?.hide();
      toast(approve ? "Technician approved." : "Application rejected.");
      await Promise.all([loadTechnicians(), loadMetrics()]);
    } catch (error) { showError("#admin-review-error", error); }
    finally {
      state.reviewBusy = false; toggleForm($("#admin-reject-form"), false); busyButton(button, false);
      $("#admin-review-reject").disabled = false;
      $("#admin-review-approve").disabled = !technician.documents?.length || verification(technician.verificationStatus) === "Approved";
    }
  }
  function bind() {
    if (state.bound) return;
    state.bound = true;
    root.querySelectorAll('.admin-tabs [role="tab"]').forEach(tab => {
      tab.addEventListener("shown.bs.tab", () => { syncNavigation(tab); loadSection(tab); });
      tab.addEventListener("click", () => { if (tab.classList.contains("active")) closeSidebar(); });
    });
    $("#admin-sidebar").addEventListener("shown.bs.offcanvas", () => $("#admin-menu-trigger").setAttribute("aria-expanded", "true"));
    $("#admin-sidebar").addEventListener("hidden.bs.offcanvas", () => $("#admin-menu-trigger").setAttribute("aria-expanded", "false"));
    $("#admin-user-filters").addEventListener("submit", event => { event.preventDefault(); loadUsers(1); });
    ["#admin-user-role", "#admin-user-status"].forEach(id => $(id).addEventListener("change", () => loadUsers(1)));
    $("#admin-technician-filters").addEventListener("submit", event => { event.preventDefault(); loadTechnicians(1); });
    $("#admin-technician-status").addEventListener("change", () => loadTechnicians(1));
    $("#admin-category-filters").addEventListener("submit", event => { event.preventDefault(); loadCategories(1); });
    $("#admin-category-status").addEventListener("change", () => loadCategories(1));
    $("#admin-retry-users").addEventListener("click", () => loadUsers());
    $("#admin-retry-technicians").addEventListener("click", () => loadTechnicians());
    $("#admin-retry-categories").addEventListener("click", () => loadCategories());
    $("#admin-retry-metrics").addEventListener("click", loadMetrics);
    $("#admin-users-prev").addEventListener("click", () => loadUsers(state.usersPage - 1));
    $("#admin-users-next").addEventListener("click", () => loadUsers(state.usersPage + 1));
    $("#admin-technicians-prev").addEventListener("click", () => loadTechnicians(state.techniciansPage - 1));
    $("#admin-technicians-next").addEventListener("click", () => loadTechnicians(state.techniciansPage + 1));
    $("#admin-categories-prev").addEventListener("click", () => loadCategories(state.categoriesPage - 1));
    $("#admin-categories-next").addEventListener("click", () => loadCategories(state.categoriesPage + 1));
    $("#admin-category-add").addEventListener("click", () => openCategory());
    $("#admin-category-form").addEventListener("submit", saveCategory);
    $("#admin-category-retry").addEventListener("click", () => { if (state.editingCategoryId !== null) openCategory(state.editingCategoryId); });
    $("#admin-category-modal").addEventListener("shown.bs.modal", () => { if (!$("#admin-category-form").hidden) $("#admin-category-name").focus(); });
    $("#admin-category-modal").addEventListener("hidden.bs.modal", () => { state.categoryEditorSerial++; state.editingCategoryId = null; $("#admin-category-form").reset(); });
    $("#admin-category-delete-confirm").addEventListener("click", deleteCategory);
    $("#admin-category-delete-alternative").addEventListener("click", () => {
      if (!state.deletingCategory || state.categoryDeleteBusy) return;
      const id = state.deletingCategory.id, modal = $("#admin-category-delete-modal");
      modal.addEventListener("hidden.bs.modal", () => { if (state.user) openCategory(id); }, { once: true });
      bootstrap.Modal.getInstance(modal)?.hide();
    });
    $("#admin-category-delete-modal").addEventListener("hidden.bs.modal", () => { state.deletingCategory = null; });
    $("#admin-create-form").addEventListener("submit", createUser);
    $("#admin-create-modal").addEventListener("show.bs.modal", () => { $("#admin-create-form").reset(); clearCreateErrors(); });
    $("#admin-create-modal").addEventListener("hidden.bs.modal", () => { $("#admin-create-password").value = ""; });
    $("#admin-user-form").addEventListener("submit", updateUser);
    $("#admin-user-deactivate").addEventListener("click", deactivateUser);
    $("#admin-review-approve").addEventListener("click", () => decideTechnician(true));
    $("#admin-review-reject").addEventListener("click", () => {
      $("#admin-reject-form").hidden = false; $("#admin-review-actions").hidden = true; $("#admin-reject-reason").focus();
    });
    $("#admin-reject-cancel").addEventListener("click", () => { $("#admin-reject-form").hidden = true; $("#admin-review-actions").hidden = false; });
    $("#admin-reject-reason").addEventListener("input", () => $("#admin-reject-reason").setCustomValidity(""));
    $("#admin-reject-form").addEventListener("submit", event => { event.preventDefault(); decideTechnician(false); });
    $("#admin-review-modal").addEventListener("hidden.bs.modal", () => { state.reviewSerial++; state.technician = null; });
    for (const [id, busy] of [["#admin-create-modal", () => state.createBusy], ["#admin-user-modal", () => state.userBusy], ["#admin-review-modal", () => state.reviewBusy], ["#admin-category-modal", () => state.categoryBusy], ["#admin-category-delete-modal", () => state.categoryDeleteBusy]]) {
      $(id).addEventListener("hide.bs.modal", event => { if (busy()) event.preventDefault(); });
    }
  }
  window.RepairAdmin = Object.freeze({
    async init(user) {
      state.user = user;
      if (!user || !roleNames(user.roles).split(", ").includes("Admin")) {
        closeSidebar();
        root.hidden = true; state.usersSerial++; state.techniciansSerial++; state.categoriesSerial++; state.categoryEditorSerial++; state.metricsSerial++; state.reviewSerial++; state.createSerial++;
        state.createBusy = false; state.userBusy = false; state.reviewBusy = false; state.categoryBusy = false; state.categoryDeleteBusy = false;
        state.managedUser = null; state.technician = null; state.editingCategoryId = null; state.deletingCategory = null;
        $("#admin-users-body").replaceChildren(); $("#admin-technicians-body").replaceChildren(); $("#admin-categories-body").replaceChildren();
        $("#admin-create-form").reset(); toggleForm($("#admin-create-form"), false); busyButton($("#admin-create-submit"), false);
        $("#admin-category-form").reset(); $("#admin-category-delete-name").textContent = "";
        for (const id of ["#admin-create-modal", "#admin-user-modal", "#admin-review-modal", "#admin-category-modal", "#admin-category-delete-modal"]) bootstrap.Modal.getInstance($(id))?.hide();
        return;
      }
      bind(); root.hidden = false;
      const active = root.querySelector('.admin-tabs [role="tab"].active');
      $("#admin-current-section").textContent = active.dataset.sectionLabel;
      await Promise.all(active.id === "admin-dashboard-tab" ? [loadMetrics()] : [loadMetrics(), loadSection(active)]);
    }
  });
})();
