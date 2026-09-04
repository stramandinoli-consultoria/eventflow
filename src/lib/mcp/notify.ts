import type { SupabaseClient } from "@supabase/supabase-js";

export type OrderPayload = {
  id: string;
  event_id: string;
  stand_id: string;
  stand_name: string;
  customer_name: string;
  status: string;
  total: number;
};

/**
 * Dispara os webhooks cadastrados que combinam com o pedido alterado.
 * Falhas de entrega nunca quebram a ferramenta MCP — apenas ficam
 * registradas em `last_status`.
 */
export async function dispatchWebhooks(
  supabase: SupabaseClient,
  order: OrderPayload,
  extra: { previous_status?: string | null; note?: string | null } = {},
): Promise<{ delivered: number; failed: number }> {
  const { data: hooks } = await supabase
    .from("ef_webhooks")
    .select("id, url, event_id, stand_name, active")
    .eq("active", true);

  const targets = (hooks ?? []).filter(
    (h) =>
      (!h.event_id || h.event_id === order.event_id) &&
      (!h.stand_name || h.stand_name.toLowerCase() === order.stand_name.toLowerCase()),
  );

  let delivered = 0;
  let failed = 0;

  await Promise.all(
    targets.map(async (hook) => {
      let status = "ok";
      try {
        const res = await fetch(hook.url, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            type: "order.status_changed",
            at: new Date().toISOString(),
            order,
            previous_status: extra.previous_status ?? null,
            note: extra.note ?? null,
          }),
        });
        if (!res.ok) status = `http_${res.status}`;
      } catch (err) {
        status = `error: ${(err as Error).message}`.slice(0, 200);
      }
      if (status === "ok") delivered += 1;
      else failed += 1;
      await supabase
        .from("ef_webhooks")
        .update({ last_status: status, last_sent_at: new Date().toISOString() })
        .eq("id", hook.id);
    }),
  );

  return { delivered, failed };
}
