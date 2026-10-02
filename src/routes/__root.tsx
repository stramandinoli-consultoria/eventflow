import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import {
  Outlet,
  Link,
  createRootRouteWithContext,
  useRouter,
  HeadContent,
  Scripts,
} from "@tanstack/react-router";
import { Toaster } from "sonner";
import { useEffect } from "react";

import appCss from "../styles.css?url";
import { Nav } from "@/components/Nav";
import { ErrorBoundary } from "@/components/ErrorBoundary";
import { startOrderSync } from "@/lib/order-sync";

function NotFoundComponent() {
  return (
    <div className="flex min-h-screen items-center justify-center bg-background px-4">
      <div className="max-w-md text-center">
        <h1 className="text-7xl font-bold text-gradient">404</h1>
        <h2 className="mt-4 text-xl font-semibold text-foreground">Página não encontrada</h2>
        <p className="mt-2 text-sm text-muted-foreground">Talvez essa barraca não esteja no mapa.</p>
        <div className="mt-6">
          <Link to="/" className="inline-flex items-center justify-center rounded-md gradient-hero px-4 py-2 text-sm font-medium text-primary-foreground">
            Voltar para eventos
          </Link>
        </div>
      </div>
    </div>
  );
}

function ErrorComponent({ error, reset }: import("@tanstack/react-router").ErrorComponentProps) {
  console.error(error);
  const router = useRouter();
  return (
    <div className="flex min-h-screen items-center justify-center bg-background px-4">
      <div className="max-w-md text-center">
        <h1 className="text-xl font-semibold tracking-tight text-foreground">Algo deu errado</h1>
        <p className="mt-2 text-sm text-muted-foreground">Tente novamente em instantes.</p>
        <div className="mt-6 flex flex-wrap justify-center gap-2">
          <button
            onClick={() => { router.invalidate(); reset(); }}
            className="inline-flex items-center justify-center rounded-md gradient-hero px-4 py-2 text-sm font-medium text-primary-foreground"
          >Tentar novamente</button>
        </div>
      </div>
    </div>
  );
}

export const Route = createRootRouteWithContext<{ queryClient: QueryClient }>()({
  head: () => ({
    meta: [
      { charSet: "utf-8" },
      { name: "viewport", content: "width=device-width, initial-scale=1" },
      { title: "EventFlow — Gestão de Eventos Gastronômicos" },
      { name: "description", content: "Plataforma SaaS para eventos gastronômicos, festivais e feiras com mapa interativo, pedidos em tempo real e timeline." },
      { property: "og:title", content: "EventFlow — Gestão de Eventos Gastronômicos" },
      { name: "twitter:title", content: "EventFlow — Gestão de Eventos Gastronômicos" },
      { property: "og:description", content: "Plataforma SaaS para eventos gastronômicos, festivais e feiras com mapa interativo, pedidos em tempo real e timeline." },
      { name: "twitter:description", content: "Plataforma SaaS para eventos gastronômicos, festivais e feiras com mapa interativo, pedidos em tempo real e timeline." },
      { property: "og:image", content: "https://pub-bb2e103a32db4e198524a2e9ed8f35b4.r2.dev/9c8c72e7-a160-47b6-bd65-475d344137e3/id-preview-5e858fb7--f9491c11-b8b5-4bc4-950d-9e836f9f017b.lovable.app-1781016990570.png" },
      { name: "twitter:image", content: "https://pub-bb2e103a32db4e198524a2e9ed8f35b4.r2.dev/9c8c72e7-a160-47b6-bd65-475d344137e3/id-preview-5e858fb7--f9491c11-b8b5-4bc4-950d-9e836f9f017b.lovable.app-1781016990570.png" },
      { name: "twitter:card", content: "summary_large_image" },
      { property: "og:type", content: "website" },
    ],
    links: [{ rel: "stylesheet", href: appCss }],
  }),
  shellComponent: RootShell,
  component: RootComponent,
  notFoundComponent: NotFoundComponent,
  errorComponent: ErrorComponent,
});

function RootShell({ children }: { children: React.ReactNode }) {
  return (
    <html lang="pt-BR">
      <head><HeadContent /></head>
      <body>{children}<Scripts /></body>
    </html>
  );
}

function RootComponent() {
  const { queryClient } = Route.useRouteContext();
  useEffect(() => { startOrderSync(); }, []);
  return (
    <QueryClientProvider client={queryClient}>
      <ErrorBoundary>
        <div className="min-h-screen flex flex-col">
          <Nav />
          <main className="flex-1"><Outlet /></main>
        </div>
      </ErrorBoundary>
      <Toaster theme="dark" position="top-right" richColors />
    </QueryClientProvider>
  );
}
