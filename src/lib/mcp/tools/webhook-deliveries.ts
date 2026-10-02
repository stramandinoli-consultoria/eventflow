import { defineTool } from "@lovable.dev/mcp-js";
import { z } from "zod";
import { supabaseForUser } from "../supabase";
import { attemptDelivery, processDueDeliveries } from "../notify";

export const listWebhookDeliveries = defineTool({
  name: "list_webhook_deliveries",
  title: "Auditar entregas de webhooks",
  description:
    "Lista as entregas de webhooks do usuário conectado com status, número de tentativas, próximo reenvio e o histórico de cada tentativa.",
  inputSchema: {
    status: z.enum(["pending", "delivered", "failed"]).optional().describe("Filtra por status da entrega."),
    webhook_id: z.string().uuid().optional().describe("Filtra por webhook."),
    order_id: z.string().optional().describe("Filtra por pedido."),
    limit: z.number().int().min(1).max(100).optional().describe("Máximo de entregas (padrão: 25)."),
    offset: z.number().int().min(0).optional().describe("Quantas entregas pular (padrão: 0)."),
  },
  annotations: { readOnlyHint: true, idempotentHint: true, openWorldHint: false },
  handler: async ({ status, webhook_id, order_id, limit, offset }, ctx) => {
    if (!ctx.isAuthenticated()) return { content: [{ type: "text", text: "Não autenticado." }], isError: true };
    const supabase = supabaseForUser(ctx);
    const size = limit ?? 25;
    const from = offset ?? 0;
    let q = supabase
      .from("ef_webhook_deliveries")
      .select("id, webhook_id, order_id, status, attempts, max_attempts, next_attempt_at, last_error, last_response_status, created_at, updated_at", { count: "exact" })
      .order("created_at", { ascending: false })
      .range(from, from + size - 1);
    if (status) q = q.eq("status", status);
    if (webhook_id) q = q.eq("webhook_id", webhook_id);
    if (order_id) q = q.eq("order_id", order_id);
    const { data, error, count } = await q;
    if (error) return { content: [{ type: "text", text: error.message }], isError: true };
    const ids = (data ?? []).map((d) => d.id);
    const { data: attempts } = ids.length
      ? await supabase
          .from("ef_webhook_attempts")
          .select("delivery_id, attempt, ok, response_status, error, duration_ms, at")
          .in("delivery_id", ids)
          .order("attempt", { ascending: true })
      : { data: [] };
    const deliveries = (data ?? []).map((d) => ({
      ...d,
      attempt_log: (attempts ?? [])
        .filter((a) => a.delivery_id === d.id)
        .map(({ attempt, ok, response_status, error, duration_ms, at }) => ({ attempt, ok, response_status, error, duration_ms, at })),
    }));
    const payload = {
      deliveries,
      pagination: { total: count ?? deliveries.length, limit: size, offset: from, has_more: from + deliveries.length < (count ?? 0) },
    };
    return { content: [{ type: "text", text: JSON.stringify(payload, null, 2) }], structuredContent: payload };
  },
});

export const retryWebhookDeliveries = defineTool({
  name: "retry_webhook_deliveries",
  title: "Reenviar webhooks",
  description:
    "Reenvia agora as entregas pendentes que já passaram do horário de reenvio, ou força o reenvio de uma entrega específica (inclusive falhas definitivas).",
  inputSchema: {
    delivery_id: z.string().uuid().optional().describe("Entrega específica a reenviar agora."),
  },
  annotations: { readOnlyHint: false, destructiveHint: false, openWorldHint: true },
  handler: async ({ delivery_id }, ctx) => {
    if (!ctx.isAuthenticated()) return { content: [{ type: "text", text: "Não autenticado." }], isError: true };
    const supabase = supabaseForUser(ctx);
    let results;
    if (delivery_id) {
      const { data: d } = await supabase
        .from("ef_webhook_deliveries")
        .select("id, webhook_id, owner_email, payload, attempts, max_attempts")
        .eq("id", delivery_id)
        .maybeSingle();
      if (!d) return { content: [{ type: "text", text: `Entrega ${delivery_id} não encontrada.` }], isError: true };
      // Reenvio manual ganha uma tentativa extra além do limite.
      results = [await attemptDelivery(supabase, { ...d, max_attempts: Math.max(d.max_attempts, d.attempts + 1) })];
    } else {
      results = await processDueDeliveries(supabase, 50);
    }
    const payload = { processed: results.length, results };
    return { content: [{ type: "text", text: JSON.stringify(payload, null, 2) }], structuredContent: payload };
  },
});
