import { defineTool } from "@lovable.dev/mcp-js";
import { z } from "zod";
import { supabaseForUser } from "../supabase";

export default defineTool({
  name: "list_orders",
  title: "Listar pedidos",
  description:
    "Lista os pedidos do EventFlow, com filtro por evento, empresa parceira (barraca) e status. Inclui opcionalmente a timeline de cada pedido.",
  inputSchema: {
    event_id: z.string().optional().describe("ID do evento, ex.: ev1."),
    stand_name: z.string().optional().describe("Nome da empresa parceira / barraca (busca parcial)."),
    status: z
      .enum(["awaiting_payment", "preparing", "ready", "completed", "cancelled"])
      .optional()
      .describe("Filtra por status do pedido."),
    include_timeline: z.boolean().optional().describe("Inclui a timeline de cada pedido (padrão: true)."),
    limit: z.number().int().min(1).max(100).optional().describe("Máximo de pedidos (padrão: 25)."),
  },
  annotations: { readOnlyHint: true, idempotentHint: true, openWorldHint: false },
  handler: async ({ event_id, stand_name, status, include_timeline, limit }, ctx) => {
    if (!ctx.isAuthenticated()) {
      return { content: [{ type: "text", text: "Não autenticado." }], isError: true };
    }
    const supabase = supabaseForUser(ctx);
    let query = supabase
      .from("ef_orders")
      .select("*")
      .order("created_at", { ascending: false })
      .limit(limit ?? 25);
    if (event_id) query = query.eq("event_id", event_id);
    if (status) query = query.eq("status", status);
    if (stand_name) query = query.ilike("stand_name", `%${stand_name}%`);

    const { data: orders, error } = await query;
    if (error) return { content: [{ type: "text", text: error.message }], isError: true };

    let result: unknown = orders ?? [];
    if (include_timeline !== false && orders?.length) {
      const { data: timeline } = await supabase
        .from("ef_order_timeline")
        .select("order_id, status, note, at")
        .in("order_id", orders.map((o) => o.id))
        .order("at", { ascending: true });
      result = orders.map((o) => ({
        ...o,
        timeline: (timeline ?? []).filter((t) => t.order_id === o.id),
      }));
    }

    return {
      content: [{ type: "text", text: JSON.stringify(result, null, 2) }],
      structuredContent: { orders: result },
    };
  },
});
