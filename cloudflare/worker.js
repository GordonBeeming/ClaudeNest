// ClaudeNest Cloudflare Worker — routes requests to backend or frontend
//
// Environment variables (set in Cloudflare dashboard):
//   BACKEND_ORIGIN  = "https://ca-claudenest-api-prod.{region}.azurecontainerapps.io"
//   FRONTEND_ORIGIN = "https://ca-claudenest-web-prod.{region}.azurecontainerapps.io"

export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    const path = url.pathname;

    // Route API and SignalR hub requests to the backend
    const isBackend = path.startsWith("/api/") || path.startsWith("/hubs/");
    const origin = isBackend ? env.BACKEND_ORIGIN : env.FRONTEND_ORIGIN;

    // Build the proxied URL preserving path and query string
    const targetUrl = new URL(path + url.search, origin);

    // Preserve all original headers including WebSocket upgrade headers
    // (required for SignalR WebSocket transport on /hubs/nest)
    const headers = new Headers(request.headers);
    headers.set("Host", targetUrl.host);

    const newRequest = new Request(targetUrl, {
      method: request.method,
      headers: headers,
      body: request.body,
      redirect: "follow",
    });

    return fetch(newRequest);
  },
};
