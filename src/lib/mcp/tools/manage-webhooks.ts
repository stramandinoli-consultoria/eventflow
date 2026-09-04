import { defineTool } from "@lovable.dev/mcp-js";
import { z } from "zod";
import { supabaseForUser } from "../supabase";

export const subscribeOrderUpdates = defineTool({
  name: "subscribe_order_updates",
  title: "Assinar avisos de pedidos",
  description:
    "Cadastra um endereço de webhook que recebe um POST em JSON sempre que o status de um pedido mudar. Pode filtrar por evento e/ou empresa parceira.",
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
      .select()
      .maybeSingle();
    if (error) return { content: [{ type: "text", text: error.message }], isError: true };
    return {
      content: [{ type: "text", text: JSON.stringify(data, null, 2) }],
      structuredContent: { webhook: data },
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
      .select("*")
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
