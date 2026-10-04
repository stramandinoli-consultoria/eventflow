// Exemplos de request/response exibidos na documentação /docs/mcp.
// O catálogo (nomes, descrições, parâmetros) é lido automaticamente do manifesto MCP.
export const MCP_EXAMPLES: Record<string, { request: unknown; response: unknown }> = {
  whoami: { request: {}, response: { id: "8c1f…", email: "voce@empresa.com" } },
  list_events: {
    request: { status: "live" },
    response: { events: [{ id: "ev1", name: "Festival Sabores", status: "live" }] },
  },
  list_orders: {
    request: { event_id: "ev1", stand_name: "burger", status: "preparing", limit: 10, page: 1, sort: "created_at_desc" },
    response: {
      orders: [
        {
          id: "ord-1042", event_id: "ev1", stand_name: "Burger Republic", customer_name: "Ana", status: "preparing", total: 58.9,
          created_at: "2026-10-02T20:10:00Z",
          timeline: [
            { status: "awaiting_payment", note: null, at: "2026-10-02T20:10:00Z" },
            { status: "preparing", note: "Pagamento confirmado no caixa", at: "2026-10-02T20:12:00Z" },
          ],
        },
      ],
      pagination: { page: 1, limit: 10, total: 37, total_pages: 4, has_next: true, sort: "created_at_desc" },
    },
  },
  get_order: {
    request: { order_id: "ord-1042" },
    response: { order: { id: "ord-1042", status: "preparing", items: [{ name: "Smash Duplo", qty: 2 }], timeline: ["…"] } },
  },
  update_order_status: {
    request: { order_id: "ord-1042", status: "ready", note: "Retirar no balcão 3" },
    response: {
      order: { id: "ord-1042", status: "ready" },
      previous_status: "preparing",
      timeline_entry: { status: "ready", note: "Retirar no balcão 3", at: "2026-10-02T20:25:00Z" },
      webhooks: { delivered: 1, queued_for_retry: 0, failed: 0 },
    },
  },
  bulk_update_order_status: {
    request: { order_ids: ["ord-1042", "ord-1043", "ord-9999"], status: "completed" },
    response: {
      status: "completed", updated: 2, skipped: 0, failed: 1,
      results: [
        { order_id: "ord-1042", ok: true, previous_status: "ready" },
        { order_id: "ord-1043", ok: true, previous_status: "ready" },
        { order_id: "ord-9999", ok: false, error: "não encontrado" },
      ],
      webhooks: { delivered: 2, queued_for_retry: 0, failed: 0 },
    },
  },
  subscribe_order_updates: {
    request: { url: "https://meu-agente.com/hooks/pedidos", event_id: "ev1" },
    response: {
      webhook: { id: "5f0c…", url: "https://meu-agente.com/hooks/pedidos", event_id: "ev1", secret: "a3f9…(64 hex)" },
      signature: { header: "X-EventFlow-Signature", format: "t=<unix_timestamp>,v1=<hex_hmac_sha256>" },
      retry_policy: "Até 6 tentativas com espera exponencial (30s, 60s, 120s, 240s, 480s).",
    },
  },
  list_order_subscriptions: {
    request: {},
    response: { webhooks: [{ id: "5f0c…", url: "https://meu-agente.com/hooks/pedidos", active: true, last_status: "ok" }] },
  },
  unsubscribe_order_updates: { request: { webhook_id: "5f0c…" }, response: "Webhook 5f0c… removido." },
  list_webhook_deliveries: {
    request: { status: "pending", limit: 10 },
    response: {
      deliveries: [
        {
          id: "d1…", order_id: "ord-1042", status: "pending", attempts: 2, next_attempt_at: "2026-10-02T20:27:00Z", last_error: "http_503",
          attempt_log: [
            { attempt: 1, ok: false, response_status: 503, duration_ms: 120 },
            { attempt: 2, ok: false, response_status: 503, duration_ms: 98 },
          ],
        },
      ],
      pagination: { total: 1, limit: 10, offset: 0, has_more: false },
    },
  },
  retry_webhook_deliveries: {
    request: { delivery_id: "d1…" },
    response: { processed: 1, results: [{ id: "d1…", attempt: 3, status: "delivered", response_status: 200 }] },
  },
};

export const WEBHOOK_BODY_EXAMPLE = {
  type: "order.status_changed",
  at: "2026-10-02T20:25:00Z",
  order: { id: "ord-1042", event_id: "ev1", stand_id: "st1", stand_name: "Burger Republic", customer_name: "Ana", status: "ready", total: 58.9 },
  previous_status: "preparing",
  note: "Retirar no balcão 3",
};

export const VERIFY_SNIPPET = `import crypto from "node:crypto";

// rawBody = corpo da requisição exatamente como recebido (string)
export function verify(rawBody, header, secret) {
  const parts = Object.fromEntries(header.split(",").map((p) => p.split("=")));
  const t = Number(parts.t);
  if (Math.abs(Date.now() / 1000 - t) > 300) return false; // janela de 5 min
  const expected = crypto.createHmac("sha256", secret).update(\`\${t}.\${rawBody}\`).digest("hex");
  return crypto.timingSafeEqual(Buffer.from(expected), Buffer.from(parts.v1 ?? ""));
}`;
