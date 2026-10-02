import { defineTool } from "@lovable.dev/mcp-js";
import { z } from "zod";
import { supabaseForUser } from "../supabase";
import { dispatchWebhooks } from "../notify";

export const ORDER_STATUSES = ["awaiting_payment", "preparing", "ready", "completed", "cancelled"] as const;

export const DEFAULT_NOTE: Record<string, string> = {
  preparing: "Pagamento confirmado no caixa",
  ready: "Notificação WhatsApp enviada",
  completed: "Pedido entregue ao cliente",
  cancelled: "Pedido cancelado",
};

export default defineTool({
  name: "update_order_status",
  title: "Atualizar status do pedido",
  description:
    "Atualiza o status de um pedido (ex.: marcar como pronto), registra automaticamente a timeline e dispara os webhooks cadastrados.",
  inputSchema: {
    order_id: z.string().trim().min(1).describe("ID do pedido, ex.: ord-1042."),
    status: z.enum(ORDER_STATUSES).describe("Novo status do pedido."),
    note: z.string().trim().max(280).optional().describe("Observação registrada na timeline."),
  },
  annotations: { readOnlyHint: false, destructiveHint: false, openWorldHint: true },
  handler: async ({ order_id, status, note }, ctx) => {
    if (!ctx.isAuthenticated()) {
      return { content: [{ type: "text", text: "Não autenticado." }], isError: true };
    }
    const supabase = supabaseForUser(ctx);

    const { data: current, error: readErr } = await supabase
      .from("ef_orders")
      .select("*")
      .eq("id", order_id)
      .maybeSingle();
    if (readErr) return { content: [{ type: "text", text: readErr.message }], isError: true };
    if (!current) {
      return { content: [{ type: "text", text: `Pedido ${order_id} não encontrado.` }], isError: true };
    }

    const at = new Date().toISOString();
    const { data: updated, error: updErr } = await supabase
      .from("ef_orders")
      .update({ status, updated_at: at })
      .eq("id", order_id)
      .select()
      .maybeSingle();
    if (updErr) return { content: [{ type: "text", text: updErr.message }], isError: true };

    const timelineNote = note ?? DEFAULT_NOTE[status] ?? null;
    await supabase
      .from("ef_order_timeline")
      .insert({ order_id, status, note: timelineNote, at });

    const delivery = await dispatchWebhooks(
      supabase,
      {
        id: order_id,
        event_id: current.event_id,
        stand_id: current.stand_id,
        stand_name: current.stand_name,
        customer_name: current.customer_name,
        status,
        total: Number(current.total),
      },
      { previous_status: current.status, note: timelineNote },
    );

    const payload = {
      order: updated,
      previous_status: current.status,
      timeline_entry: { status, note: timelineNote, at },
      webhooks: delivery,
    };
    return {
      content: [{ type: "text", text: JSON.stringify(payload, null, 2) }],
      structuredContent: payload,
    };
  },
});
