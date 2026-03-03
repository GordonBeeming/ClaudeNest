/** Runtime env vars injected by Aspire via the aspireEnvPlugin in vite.config.ts */
const aspireEnv = (window as unknown as Record<string, unknown>).__ASPIRE_ENV as
  | Record<string, string>
  | undefined;

function getEnv(key: string): string {
  return aspireEnv?.[key] || import.meta.env[key] || "";
}

export const auth0Domain = getEnv("VITE_AUTH0_DOMAIN");
export const auth0ClientId = getEnv("VITE_AUTH0_CLIENT_ID");
export const auth0Audience = getEnv("VITE_AUTH0_AUDIENCE");
export const isAuth0Configured = !!(auth0Domain && auth0ClientId);

export const appInsightsConnectionString = getEnv("VITE_APPINSIGHTS_CONNECTION_STRING");
