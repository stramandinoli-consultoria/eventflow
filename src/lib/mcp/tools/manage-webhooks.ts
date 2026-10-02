import { defineTool } from "@lovable.dev/mcp-js";
import { z } from "zod";
import { supabaseForUser } from "../supabase";

export const subscribeOrderUpdates = defineTool({
  name: "subscribe_order_updates",
  title: "Assinar avisos de pedidos",
  description:
    "Cadastra um webhook que recebe um POST em JSON assinado com HMAC-SHA256 sempre que o status de um pedido mudar, com reenvio automático em caso de falha. Pode filtrar por evento e/ou empresa parceira.",
  inputSchema: {
    url: z.string().url().describe("URL https que receberá o POST de notificação."),
    event_id: z.string().optional().describe("Notificar apenas pedidos deste evento."),
    stand_name: z.string().optional().describe("Notificar apenas pedidos desta empresa parceira."),
  },
  annotations: { readOnlyHint: false, openWorldHint: true },
  handler: async ({ url, event_id, stand_name }, ctx) => {
    if (!ctx.isAuthenticated()) {
      return { content: [{ type: "text", text: "Não autenticado." }], isError: true };
    }
    const supabase = supabaseForUser(ctx);
    const { data, error } = await supabase
      .from("ef_webhooks")
      .insert({ url, event_id: event_id ?? null, stand_name: stand_name ?? null })
      .select("id, url, event_id, stand_name, active, created_at, secret")
      .maybeSingle();
    if (error) return { content: [{ type: "text", text: error.message }], isError: true };
    const payload = {
      webhook: data,
      signature: {
        header: "X-EventFlow-Signature",
        format: "t=<unix_timestamp>,v1=<hex_hmac_sha256>",
        signed_content: "`${t}.${raw_body}`",
        algorithm: "HMAC-SHA256 com o campo `secret` (guarde-o: ele só é exibido agora)",
      },
      retry_policy: "Até 6 tentativas com espera exponencial (30s, 60s, 120s, 240s, 480s).",
    };
    return {
      content: [{ type: "text", text: JSON.stringify(payload, null, 2) }],
      structuredContent: payload,
    };
  },
});

export const listOrderSubscriptions = defineTool({
  name: "list_order_subscriptions",
  title: "Listar avisos cadastrados",
  description: "Lista os webhooks de notificação de pedidos cadastrados pelo usuário conectado.",
  inputSchema: {},
  annotations: { readOnlyHint: true, idempotentHint: true, openWorldHint: false },
  handler: async (_input, ctx) => {
    if (!ctx.isAuthenticated()) {
      return { content: [{ type: "text", text: "Não autenticado." }], isError: true };
    }
    const supabase = supabaseForUser(ctx);
    const { data, error } = await supabase
      .from("ef_webhooks")
      .select("id, url, event_id, stand_name, active, last_status, last_sent_at, created_at")
      .order("created_at", { ascending: false });
    if (error) return { content: [{ type: "text", text: error.message }], isError: true };
    return {
      content: [{ type: "text", text: JSON.stringify(data ?? [], null, 2) }],
      structuredContent: { webhooks: data ?? [] },
    };
  },
});

export const unsubscribeOrderUpdates = defineTool({
  name: "unsubscribe_order_updates",
  title: "Remover aviso de pedidos",
  description: "Remove um webhook de notificação de pedidos cadastrado pelo usuário conectado.",
  inputSchema: { webhook_id: z.string().uuid().describe("ID do webhook a remover.") },
  annotations: { readOnlyHint: false, destructiveHint: true, openWorldHint: false },
  handler: async ({ webhook_id }, ctx) => {
    if (!ctx.isAuthenticated()) {
      return { content: [{ type: "text", text: "Não autenticado." }], isError: true };
    }
    const supabase = supabaseForUser(ctx);
    const { error } = await supabase.from("ef_webhooks").delete().eq("id", webhook_id);
    if (error) return { content: [{ type: "text", text: error.message }], isError: true };
    return { content: [{ type: "text", text: `Webhook ${webhook_id} removido.` }] };
  },
});
