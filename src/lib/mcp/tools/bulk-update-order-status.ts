import { defineTool } from "@lovable.dev/mcp-js";
import { z } from "zod";
import { supabaseForUser } from "../supabase";
import { dispatchWebhooks } from "../notify";
import { DEFAULT_NOTE, ORDER_STATUSES } from "./update-order-status";

export default defineTool({
  name: "bulk_update_order_status",
  title: "Atualizar status em lote",
  description:
    "Atualiza o status de vários pedidos de uma vez, registra a timeline de cada um e dispara os webhooks. Retorna o resultado individual de cada pedido.",
  inputSchema: {
    order_ids: z.array(z.string().trim().min(1)).min(1).max(100).describe("IDs dos pedidos, ex.: [\"ord-1042\", \"ord-1043\"]."),
    status: z.enum(ORDER_STATUSES).describe("Novo status aplicado a todos os pedidos."),
    note: z.string().trim().max(280).optional().describe("Observação registrada na timeline de cada pedido."),
  },
  annotations: { readOnlyHint: false, destructiveHint: false, openWorldHint: true },
  handler: async ({ order_ids, status, note }, ctx) => {
    if (!ctx.isAuthenticated()) return { content: [{ type: "text", text: "Não autenticado." }], isError: true };
    const supabase = supabaseForUser(ctx);
    const ids = Array.from(new Set(order_ids));
    const { data: orders, error } = await supabase.from("ef_orders").select("*").in("id", ids);
    if (error) return { content: [{ type: "text", text: error.message }], isError: true };

    const timelineNote = note ?? DEFAULT_NOTE[status] ?? null;
    const results: { order_id: string; ok: boolean; previous_status?: string; skipped?: string; error?: string }[] = [];
    let webhooks = { delivered: 0, queued_for_retry: 0, failed: 0 };

    for (const id of ids) {
      const current = orders?.find((o) => o.id === id);
      if (!current) { results.push({ order_id: id, ok: false, error: "não encontrado" }); continue; }
      if (current.status === status) { results.push({ order_id: id, ok: true, previous_status: current.status, skipped: "já estava neste status" }); continue; }
      const at = new Date().toISOString();
      const { error: updErr } = await supabase.from("ef_orders").update({ status, updated_at: at }).eq("id", id);
      if (updErr) { results.push({ order_id: id, ok: false, error: updErr.message }); continue; }
      await supabase.from("ef_order_timeline").insert({ order_id: id, status, note: timelineNote, at });
      const d = await dispatchWebhooks(
        supabase,
        { id, event_id: current.event_id, stand_id: current.stand_id, stand_name: current.stand_name, customer_name: current.customer_name, status, total: Number(current.total) },
        { previous_status: current.status, note: timelineNote },
      );
      webhooks = { delivered: webhooks.delivered + d.delivered, queued_for_retry: webhooks.queued_for_retry + d.queued_for_retry, failed: webhooks.failed + d.failed };
      results.push({ order_id: id, ok: true, previous_status: current.status });
    }

    const payload = {
      status,
      updated: results.filter((r) => r.ok && !r.skipped).length,
      skipped: results.filter((r) => r.skipped).length,
      failed: results.filter((r) => !r.ok).length,
      results,
      webhooks,
    };
    return { content: [{ type: "text", text: JSON.stringify(payload, null, 2) }], structuredContent: payload };
  },
});
