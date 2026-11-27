/* Same-origin, cookie-authenticated API client. Nothing is persisted in browser storage. */
(() => {
  "use strict";
  class ApiError extends Error {
    constructor(message, status, errors) { super(message); this.status = status; this.errors = errors || {}; }
  }
  async function fetchWithTimeout(path, options) {
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), 20000);
    try { return await fetch(path, { ...options, signal: controller.signal }); }
    catch (error) {
      if (controller.signal.aborted) throw new ApiError("The server took too long. Refresh the list before trying again.", 408);
      throw new ApiError("Unable to reach the server. Check your connection and try again.", 0);
    } finally { clearTimeout(timer); }
  }
  async function read(response) {
    if (response.status === 204) return null;
    const type = response.headers.get("content-type") || "";
    if (!type.includes("json")) return null;
    try { return await response.json(); }
    catch { throw new ApiError("The server returned an unreadable response. Please try again.", 502); }
  }
  async function request(path, method = "GET", body) {
    if (!path.startsWith("/api/")) throw new Error("Invalid API destination.");
    const headers = { Accept: "application/json" };
    if (method !== "GET") {
      // Obtain a fresh token for the current identity, including after sign-in.
      let csrfResponse;
      csrfResponse = await fetchWithTimeout("/api/auth/csrf", { credentials: "same-origin", cache: "no-store", headers });
      const csrf = await read(csrfResponse);
      if (csrfResponse.status === 401) document.dispatchEvent(new CustomEvent("repair:session-expired"));
      if (!csrfResponse.ok || !csrf?.requestToken) throw new ApiError("Unable to verify this request. Refresh and try again.", csrfResponse.status);
      headers["X-CSRF-TOKEN"] = csrf.requestToken;
    }
    const multipart = typeof FormData !== "undefined" && body instanceof FormData;
    if (body !== undefined && !multipart) headers["Content-Type"] = "application/json";
    let response;
    try {
      response = await fetchWithTimeout(path, { method, credentials: "same-origin", cache: "no-store", headers,
        body: body === undefined ? undefined : multipart ? body : JSON.stringify(body) });
    } catch (error) {
      if (error instanceof ApiError) throw error;
      throw new ApiError("Unable to reach the server. Check your connection and try again.", 0);
    }
    const data = await read(response);
    if (!response.ok) {
      if (response.status === 401 && path !== "/api/auth/me" && path !== "/api/auth/login")
        document.dispatchEvent(new CustomEvent("repair:session-expired"));
      const errors = data?.errors || {};
      const firstError = Object.values(errors).flat().find(value => typeof value === "string");
      throw new ApiError(data?.detail || firstError || data?.title || "The request could not be completed. Please try again.", response.status, errors);
    }
    return data;
  }
  window.RepairApi = Object.freeze({
    get: path => request(path),
    post: (path, body) => request(path, "POST", body),
    postForm: (path, body) => {
      if (!(body instanceof FormData)) throw new Error("A form is required.");
      return request(path, "POST", body);
    },
    put: (path, body) => request(path, "PUT", body),
    delete: path => request(path, "DELETE")
  });
})();
