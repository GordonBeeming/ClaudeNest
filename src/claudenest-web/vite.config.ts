import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";

const backendUrl = process.env.BACKEND_URL || "http://localhost:5000";

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
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
    },
  },
});
