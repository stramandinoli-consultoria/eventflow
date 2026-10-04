import { createFileRoute } from "@tanstack/react-router";

// Processa a fila de reenvio de webhooks. Sem entrada do chamador: só reenvia
// entregas pendentes cujo horário de reenvio já passou, então é seguro chamar
// a partir de um agendador externo.
export const Route = createFileRoute("/api/public/webhooks/retry")({
  server: {
    handlers: {
      POST: async () => {
        const { supabaseAdmin } = await import("@/integrations/supabase/client.server");
        const { processDueDeliveries } = await import("@/lib/mcp/notify");
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        const results = await processDueDeliveries(supabaseAdmin as any, 50);
        return Response.json({ processed: results.length });
      },
    },
  },
});
