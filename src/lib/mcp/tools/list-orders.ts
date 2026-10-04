import { defineTool } from "@lovable.dev/mcp-js";
import { z } from "zod";
import { supabaseForUser } from "../supabase";
import { ORDER_STATUSES } from "./update-order-status";

export default defineTool({
  name: "list_orders",
  title: "Listar pedidos",
  description:
    "Lista os pedidos do EventFlow com paginação e ordenação por data de criação, filtrando por evento, empresa parceira (barraca) e status. Inclui opcionalmente a timeline de cada pedido.",
  inputSchema: {
    event_id: z.string().optional().describe("ID do evento, ex.: ev1."),
    stand_name: z.string().optional().describe("Nome da empresa parceira / barraca (busca parcial)."),
    status: z.enum(ORDER_STATUSES).optional().describe("Filtra por status do pedido."),
    include_timeline: z.boolean().optional().describe("Inclui a timeline de cada pedido (padrão: true)."),
    limit: z.number().int().min(1).max(100).optional().describe("Pedidos por página (padrão: 25)."),
    page: z.number().int().min(1).optional().describe("Número da página, começando em 1 (padrão: 1)."),
    sort: z.enum(["created_at_desc", "created_at_asc"]).optional().describe("Ordenação por data de criação (padrão: mais recentes primeiro)."),
  },
  annotations: { readOnlyHint: true, idempotentHint: true, openWorldHint: false },
  handler: async ({ event_id, stand_name, status, include_timeline, limit, page, sort }, ctx) => {
    if (!ctx.isAuthenticated()) {
      return { content: [{ type: "text", text: "Não autenticado." }], isError: true };
    }
    const supabase = supabaseForUser(ctx);
    const size = limit ?? 25;
    const current = page ?? 1;
    const from = (current - 1) * size;
    const ascending = sort === "created_at_asc";
    let query = supabase
      .from("ef_orders")
      .select("*", { count: "exact" })
      .order("created_at", { ascending })
      .order("id", { ascending })
      .range(from, from + size - 1);
    if (event_id) query = query.eq("event_id", event_id);
    if (status) query = query.eq("status", status);
    if (stand_name) query = query.ilike("stand_name", `%${stand_name}%`);

    const { data: orders, error, count } = await query;
    if (error) return { content: [{ type: "text", text: error.message }], isError: true };

    let result: unknown[] = orders ?? [];
    if (include_timeline !== false && orders?.length) {
      const { data: timeline } = await supabase
        .from("ef_order_timeline")
        .select("order_id, status, note, at")
        .in("order_id", orders.map((o) => o.id))
        .order("at", { ascending: true });
      result = orders.map((o) => ({
        ...o,
        timeline: (timeline ?? [])
          .filter((t) => t.order_id === o.id)
          .map(({ status, note, at }) => ({ status, note, at })),
      }));
    }

    const total = count ?? result.length;
    const totalPages = Math.max(1, Math.ceil(total / size));
    const payload = {
      orders: result,
      pagination: {
        page: current,
        limit: size,
        total,
        total_pages: totalPages,
        has_next: current < totalPages,
        sort: ascending ? "created_at_asc" : "created_at_desc",
      },
    };
    return {
      content: [{ type: "text", text: JSON.stringify(payload, null, 2) }],
      structuredContent: JSON.parse(JSON.stringify(payload)),
    };
  },
});
