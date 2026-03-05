import fs from "fs";
import path from "path";
import { execSync } from "child_process";
import { defineConfig, type Plugin } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";

const backendUrl = process.env.BACKEND_URL || "http://localhost:5000";
const isServe = !process.argv.includes("build");

// Re-use the trusted dotnet dev certificate for the Vite HTTPS server (dev only)
const certDir = path.resolve(import.meta.dirname, ".cert");
const certFile = path.join(certDir, "localhost.pem");
const keyFile = path.join(certDir, "localhost.key");

if (isServe && (!fs.existsSync(certFile) || !fs.existsSync(keyFile))) {
  fs.mkdirSync(certDir, { recursive: true });
  execSync(
    `dotnet dev-certs https --export-path "${certFile}" --format Pem --no-password`,
  );
}

/**
 * Injects VITE_* process env vars (set by Aspire via WithEnvironment) into the
 * HTML as window.__ASPIRE_ENV so they're available at runtime in the browser.
 * Vite 7 doesn't automatically forward process.env to import.meta.env.
 */
function aspireEnvPlugin(): Plugin {
  return {
    name: "aspire-env",
    transformIndexHtml(html) {
      const envVars: Record<string, string> = {};
      for (const [key, value] of Object.entries(process.env)) {
        if (key.startsWith("VITE_") && value) {
          envVars[key] = value;
        }
      }
      if (Object.keys(envVars).length === 0) return html;

      return html.replace(
        "<head>",
        `<head>\n    <script>window.__ASPIRE_ENV=${JSON.stringify(envVars)};</script>`,
      );
    },
  };
}

export default defineConfig({
  plugins: [aspireEnvPlugin(), react(), tailwindcss()],
  server: {
    https: isServe ? { cert: certFile, key: keyFile } : undefined,
    port: parseInt(process.env.PORT || "5173"),
    strictPort: true,
    proxy: {
      "/api": {
        target: backendUrl,
        changeOrigin: true,
      },
      "/hubs": {
        target: backendUrl,
        changeOrigin: true,
        ws: true,
      },
      "/install.sh": {
        target: backendUrl,
        changeOrigin: true,
      },
      "/install.ps1": {
        target: backendUrl,
        changeOrigin: true,
      },
    },
  },
});
