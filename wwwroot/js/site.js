/* Shared UI and real account session. Domain features without APIs remain unavailable. */
(() => {
  "use strict";
  const $ = selector => document.querySelector(selector);
  const $$ = selector => Array.from(document.querySelectorAll(selector));
  const api = window.RepairApi;
  let currentUser = null, redirecting = false, sessionGeneration = 0, loggingOut = false, authBusy = false;
  function toast(message) {
    const target = $("#toast-message");
    if (!target) return;
    target.textContent = message;
    bootstrap.Toast.getOrCreateInstance($("#app-toast")).show();
  }
  function hasRole(user, role) {
    if (typeof user?.roles === "number") return (user.roles & { Client: 1, Technician: 2, Admin: 4 }[role]) !== 0;
    return String(user?.roles || "").split(",").map(value => value.trim()).includes(role);
  }
  window.RepairUi = Object.freeze({ toast, hasRole });
  function safeDestination() {
    const value = new URLSearchParams(location.search).get("returnUrl") || "/Dashboard";
    try {
      const target = new URL(value, location.origin);
      return target.origin === location.origin && target.pathname.startsWith("/") &&
        target.pathname.replace(/\/+$/, "").toLowerCase() !== "/account" ? target.pathname + target.search + target.hash : "/Dashboard";
    } catch { return "/Dashboard"; }
  }
  function goToSignIn() {
    if (redirecting) return;
    redirecting = true;
    sessionGeneration++;
    currentUser = null;
    clearProfile(true);
    $$("[data-profile-open]").forEach(button => { button.disabled = true; });
    window.RepairAdmin?.init(null);
    window.RepairTechnicianApplication?.init(null);
    location.assign("/Account?reason=session&returnUrl=" + encodeURIComponent(location.pathname + location.search));
  }
  function setAccount(user) {
    currentUser = user;
    $("#profile-loading")?.setAttribute("hidden", "");
    $("#profile-error")?.setAttribute("hidden", "");
    $$("[data-profile-open]").forEach(button => { button.disabled = false; });
    $$("[data-greeting-name]").forEach(node => { node.textContent = user.displayName.trim().split(/\s+/)[0] || user.displayName; });
    const details = $("#account-details");
    if (details) {
      details.replaceChildren();
      for (const [label, value] of [["Name", user.displayName], ["Email", user.email], ["Phone", user.phoneNumber || "Not provided"], ["Roles", String(user.roles)], ["Status", user.status]]) {
        const row = document.createElement("div"), term = document.createElement("dt"), description = document.createElement("dd");
        term.textContent = label; description.textContent = value;
        row.append(term, description); details.append(row);
      }
    }
  }
  function clearProfile(close = false) {
    $("#account-details")?.replaceChildren();
    $("#profile-loading")?.setAttribute("hidden", "");
    const modal = $("#profile-modal");
    if (close && modal) bootstrap.Modal.getInstance(modal)?.hide();
  }
  function showProfileError(error) {
    if (!$("#profile-error")) return;
    clearProfile();
    $("#profile-error-text").textContent = error.status === 401 ? "Your session has ended. Sign in again to view your account." : error.message;
    $("#profile-error").hidden = false;
    $("#profile-retry").hidden = error.status === 401;
    $("#profile-signin").hidden = error.status !== 401;
  }
  function showSessionError(error) {
    showProfileError(error);
    if (!$("#session-error")) return;
    $("#session-error-text").textContent = error.message;
    $("#session-error").hidden = false;
  }
  async function loadSession() {
    if (loggingOut || redirecting || authBusy) return;
    const generation = ++sessionGeneration;
    if ($("#profile-loading")) {
      clearProfile();
      $("#profile-loading").hidden = false;
      $("#profile-error").hidden = true;
    }
    if ($("#session-error")) $("#session-error").hidden = true;
    try {
      const user = await api.get("/api/auth/me");
      if (generation !== sessionGeneration) return;
      setAccount(user);
      if ($("#login-form")) { location.replace(safeDestination()); return; }
      if (hasRole(user, "Admin") && $("#admin-workspace")) await window.RepairAdmin.init(user);
      if (!hasRole(user, "Admin") && $("#technician-application")) await window.RepairTechnicianApplication?.init(user);
      openRequestedDialog();
    } catch (error) {
      if (generation !== sessionGeneration) return;
      if (error.status === 401) {
        currentUser = null;
        showProfileError(error);
        if ($("#technician-application") || $("#admin-workspace")) goToSignIn();
      } else showSessionError(error);
    }
  }
  function openRequestedDialog() {
    const params = new URLSearchParams(location.search);
    if (params.get("privacy") === "1") {
      bootstrap.Modal.getOrCreateInstance($("#privacy-modal")).show();
      history.replaceState(null, "", location.pathname + location.hash);
      return;
    }
    if (!currentUser) return;
    if (params.get("panel") === "technician") {
      const application = $("#technician-application"), approvals = $("#admin-technicians-tab");
      if (hasRole(currentUser, "Admin") && approvals) bootstrap.Tab.getOrCreateInstance(approvals).show();
      else if (application && !application.hidden) {
        $("#technician-application-title").setAttribute("tabindex", "-1");
        $("#technician-application-title").focus({ preventScroll: true });
        application.scrollIntoView({ block: "start" });
      }
      params.delete("panel");
      history.replaceState(null, "", location.pathname + (params.size ? "?" + params : "") + location.hash);
      return;
    }
    if (params.get("panel") === "profile") {
      params.delete("panel");
      history.replaceState(null, "", location.pathname + (params.size ? "?" + params : "") + location.hash);
    }
    const target = params.get("new") === "1" ? $("#unavailable-modal") : null;
    if (target) {
      bootstrap.Modal.getOrCreateInstance(target).show();
      history.replaceState(null, "", location.pathname + location.hash);
    }
  }
  function clearPasswords(form) {
    form?.querySelectorAll('input[autocomplete="current-password"],input[autocomplete="new-password"]').forEach(input => {
      input.value = ""; input.type = "password";
      const toggle = document.querySelector('[data-password-toggle="' + input.id + '"]');
      if (toggle) { toggle.textContent = "Show"; toggle.setAttribute("aria-label", "Show password"); }
    });
    $("#confirm-password")?.setCustomValidity("");
  }
  function setAuthBusy(busy, form) {
    authBusy = busy;
    $$("#login-form input,#login-form button,#register-form input,#register-form select,#register-form button,.account-tabs button,[data-auth-tab]").forEach(control => { control.disabled = busy; });
    form.toggleAttribute("aria-busy", busy);
    if (busy) form.setAttribute("aria-busy", "true");
  }
  function clearSignupErrors() {
    $("#register-error").hidden = true;
    $("#register-form").querySelectorAll(".is-invalid").forEach(input => { input.classList.remove("is-invalid"); input.removeAttribute("aria-invalid"); });
    $("#register-form").querySelectorAll(".invalid-feedback").forEach(feedback => { feedback.textContent = ""; feedback.classList.remove("d-block"); });
    $("#confirm-password").setCustomValidity("");
  }
  function signupFieldError(field, message) {
    const input = $("#" + (field === "confirm" ? "confirm-password" : "register-" + field));
    const feedback = $("#register-" + field + "-error");
    if (!input || !feedback) return;
    input.classList.add("is-invalid"); input.setAttribute("aria-invalid", "true");
    const hints = field === "password" ? "register-password-hint " : "";
    input.setAttribute("aria-describedby", hints + feedback.id);
    feedback.textContent = message; feedback.classList.add("d-block");
  }
  function signupErrors(error) {
    const summary = $("#register-error");
    summary.textContent = error.message || "We couldn't create your account. Please try again."; summary.hidden = false;
    const fields = { displayname: "name", email: "email", phonenumber: "phone", password: "password" };
    const errors = { ...(error.errors || {}) };
    if (error.status === 409 && !Object.keys(errors).length) errors.Email = [summary.textContent];
    for (const [key, messages] of Object.entries(errors)) {
      const field = fields[key.split(".").pop().toLowerCase()];
      if (field) signupFieldError(field, Array.isArray(messages) ? messages.join(" ") : String(messages));
    }
  }
  async function init() {
    $$("[data-password-toggle]").forEach(button => button.addEventListener("click", () => {
      const field = document.getElementById(button.dataset.passwordToggle);
      const show = field.type === "password";
      field.type = show ? "text" : "password";
      button.textContent = show ? "Hide" : "Show";
      button.setAttribute("aria-label", show ? "Hide password" : "Show password");
    }));
    const login = $("#login-form");
    if (login) {
      const params = new URLSearchParams(location.search);
      if (params.get("reason") === "session") {
        $("#login-error").textContent = "Your session has ended. Please sign in again.";
        $("#login-error").hidden = false;
      }
      const registration = $("#register-form");
      if (registration) {
        $$("[data-auth-tab]").forEach(button => button.addEventListener("click", () => {
          if (!authBusy) bootstrap.Tab.getOrCreateInstance($(button.dataset.authTab === "signup" ? "#register-tab" : "#signin-tab")).show();
        }));
        ["#signin-tab", "#register-tab"].forEach(id => $(id).addEventListener("hide.bs.tab", event => {
          if (authBusy) event.preventDefault();
          else clearPasswords(id === "#signin-tab" ? login : registration);
        }));
        if (["access", "register"].includes(params.get("mode"))) bootstrap.Tab.getOrCreateInstance($("#register-tab")).show();
        $("#confirm-password").addEventListener("input", () => $("#confirm-password").setCustomValidity(""));
        $("#register-password").addEventListener("input", () => $("#confirm-password").setCustomValidity(""));
        registration.addEventListener("submit", async event => {
          event.preventDefault();
          if (authBusy) return;
          clearSignupErrors();
          if (!registration.reportValidity()) return;
          if ($("#register-password").value !== $("#confirm-password").value) {
            $("#confirm-password").setCustomValidity("Passwords must match.");
            signupFieldError("confirm", "Passwords must match.");
            $("#confirm-password").focus(); return;
          }
          const payload = { displayName: $("#register-name").value.trim(), email: $("#register-email").value.trim(), phoneNumber: $("#register-phone").value.trim() || null,
            password: $("#register-password").value };
          ++sessionGeneration;
          const submit = $("#register-submit");
          setAuthBusy(true, registration); submit.textContent = "Creating account…";
          clearPasswords(registration);
          let failed = false;
          try {
            await api.post("/api/auth/register", payload);
            location.assign(safeDestination());
          } catch (error) { failed = true; signupErrors(error); }
          finally {
            payload.password = ""; setAuthBusy(false, registration); submit.textContent = "Create account";
            if (failed) ($("#register-form .is-invalid") || $("#register-error")).focus();
          }
        });
      }
      login.addEventListener("submit", async event => {
        event.preventDefault();
        if (authBusy || !login.reportValidity()) return;
        const submit = $("#login-submit"), password = $("#login-password");
        const payload = { email: $("#login-email").value.trim(), password: password.value, rememberMe: $("#remember-me").checked };
        ++sessionGeneration; setAuthBusy(true, login); submit.textContent = "Signing in…"; clearPasswords(login);
        $("#login-error").hidden = true;
        try {
          await api.post("/api/auth/login", payload);
          location.assign(safeDestination());
        } catch (error) {
          $("#login-error").textContent = error.message;
          $("#login-error").hidden = false;
          $("#login-error").focus();
        } finally {
          payload.password = ""; setAuthBusy(false, login); submit.textContent = "Sign in";
        }
      });
    }
    $$("[data-logout]").forEach(button => button.addEventListener("click", async () => {
      if (loggingOut) return;
      loggingOut = true;
      sessionGeneration++;
      button.disabled = true;
      clearProfile(true);
      $$("[data-profile-open]").forEach(trigger => { trigger.disabled = true; });
      try {
        await api.post("/api/auth/logout");
        currentUser = null;
        window.RepairAdmin?.init(null);
        window.RepairTechnicianApplication?.init(null);
        location.replace("/Account");
      } catch (error) { loggingOut = false; if (currentUser) setAccount(currentUser); toast(error.message); button.disabled = false; }
    }));
    $("#session-retry")?.addEventListener("click", loadSession);
    $("#profile-retry")?.addEventListener("click", loadSession);
    document.addEventListener("repair:session-expired", goToSignIn);
    // Keep unavailable workflows honest instead of making browser-only records.
    $("#quick-request-form")?.addEventListener("submit", event => {
      event.preventDefault(); toast("Repair requests are not available yet.");
    });
    $$('a[href*="new=1"]').forEach(link => link.addEventListener("click", event => {
      event.preventDefault(); toast("Repair requests are not available yet.");
    }));
    await loadSession();
    if (!currentUser) openRequestedDialog();
  }
  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", init, { once: true });
  else init();
})();
