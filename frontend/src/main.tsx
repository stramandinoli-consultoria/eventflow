import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { RouterProvider } from "@tanstack/react-router";

import "./styles.css";
import { getRouter } from "./router";
import { startSync } from "@/lib/store";

const router = getRouter();

declare module "@tanstack/react-router" {
  interface Register {
    router: ReturnType<typeof getRouter>;
  }
}

// Carrega os dados da API (e liga o tempo real) antes de desenhar a primeira tela,
// para que links diretos como /events/ev1 não mostrem "não encontrado" por um instante.
void startSync().finally(() => {
  createRoot(document.getElementById("root")!).render(
    <StrictMode>
      <RouterProvider router={router} />
    </StrictMode>,
  );
});
