import { defineTool } from "@lovable.dev/mcp-js";
import { z } from "zod";
import { supabaseForUser } from "../supabase";

export default defineTool({
  name: "get_order",
  title: "Detalhar pedido",
  description: "Retorna um pedido do EventFlow com itens, status atual e a timeline completa.",
  inputSchema: { order_id: z.string().trim().min(1).describe("ID do pedido, ex.: ord-1042.") },
  annotations: { readOnlyHint: true, idempotentHint: true, openWorldHint: false },
  handler: async ({ order_id }, ctx) => {
    if (!ctx.isAuthenticated()) {
      return { content: [{ type: "text", text: "Não autenticado." }], isError: true };
    }
    const supabase = supabaseForUser(ctx);
    const { data: order, error } = await supabase
      .from("ef_orders")
      .select("*")
      .eq("id", order_id)
      .maybeSingle();
    if (error) return { content: [{ type: "text", text: error.message }], isError: true };
    if (!order) {
      return { content: [{ type: "text", text: `Pedido ${order_id} não encontrado.` }], isError: true };
    }
    const { data: timeline } = await supabase
      .from("ef_order_timeline")
      .select("status, note, at")
      .eq("order_id", order_id)
      .order("at", { ascending: true });

    const payload = { ...order, timeline: timeline ?? [] };
    return {
      content: [{ type: "text", text: JSON.stringify(payload, null, 2) }],
      structuredContent: { order: payload },
    };
  },
});
