import { defineConfig, loadEnv } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";
import tsConfigPaths from "vite-tsconfig-paths";
import { tanstackRouter } from "@tanstack/router-plugin/vite";

// Front-end SPA. Em desenvolvimento, /api, /hubs e /mcp são encaminhados para a
// API .NET (padrão http://localhost:5080), então não há problema de CORS.
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), "");
  const apiTarget = env.API_PROXY_TARGET || "http://localhost:5080";

  return {
    plugins: [
      // O plugin do router precisa vir antes do plugin do React.
      tanstackRouter({ target: "react", autoCodeSplitting: true }),
      react(),
      tailwindcss(),
      tsConfigPaths({ projects: ["./tsconfig.json"] }),
    ],
    resolve: {
      dedupe: ["react", "react-dom", "@tanstack/react-router", "@tanstack/react-query"],
    },
    server: {
      port: 5174,
      proxy: {
        "/api": { target: apiTarget, changeOrigin: true },
        "/mcp": { target: apiTarget, changeOrigin: true },
        "/hubs": { target: apiTarget, changeOrigin: true, ws: true },
      },
    },
    preview: { port: 5174 },
  };
});
