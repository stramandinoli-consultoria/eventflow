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

export const SIGNATURE_HEADER = "X-EventFlow-Signature";
const BASE_BACKOFF_SECONDS = 30;
const MAX_BACKOFF_SECONDS = 3600;
const TIMEOUT_MS = 8000;

/** Atraso exponencial: 30s, 60s, 120s, 240s, 480s... limitado a 1h. */
export function backoffSeconds(attempt: number): number {
  return Math.min(BASE_BACKOFF_SECONDS * 2 ** Math.max(0, attempt - 1), MAX_BACKOFF_SECONDS);
}

/** HMAC-SHA256 em hex de `${timestamp}.${body}` usando o segredo do webhook. */
export async function signPayload(secret: string, timestamp: number, body: string): Promise<string> {
  const enc = new TextEncoder();
  const key = await crypto.subtle.importKey(
    "raw",
    enc.encode(secret),
    { name: "HMAC", hash: "SHA-256" },
    false,
    ["sign"],
  );
  const sig = await crypto.subtle.sign("HMAC", key, enc.encode(`${timestamp}.${body}`));
  return Array.from(new Uint8Array(sig))
    .map((b) => b.toString(16).padStart(2, "0"))
    .join("");
}

type DeliveryRow = {
  id: string;
  webhook_id: string;
  owner_email: string;
  payload: unknown;
  attempts: number;
  max_attempts: number;
};

/** Executa uma tentativa de entrega, grava a auditoria e agenda o reenvio se falhar. */
export async function attemptDelivery(supabase: SupabaseClient, delivery: DeliveryRow) {
  const { data: hook } = await supabase
    .from("ef_webhooks")
    .select("id, url, secret, active")
    .eq("id", delivery.webhook_id)
    .maybeSingle();

  const attempt = delivery.attempts + 1;
  const started = Date.now();
  let ok = false;
  let responseStatus: number | null = null;
  let error: string | null = null;

  if (!hook || !hook.active) {
    error = "webhook inativo ou removido";
  } else {
    const body = JSON.stringify(delivery.payload);
    const timestamp = Math.floor(Date.now() / 1000);
    const signature = await signPayload(hook.secret, timestamp, body);
    try {
      const res = await fetch(hook.url, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          [SIGNATURE_HEADER]: `t=${timestamp},v1=${signature}`,
          "X-EventFlow-Delivery": delivery.id,
          "X-EventFlow-Attempt": String(attempt),
        },
        body,
        signal: AbortSignal.timeout(TIMEOUT_MS),
      });
      responseStatus = res.status;
      ok = res.ok;
      if (!ok) error = `http_${res.status}`;
    } catch (err) {
      error = String((err as Error).message ?? err).slice(0, 300);
    }
  }

  const now = new Date();
  const exhausted = attempt >= delivery.max_attempts || !hook || !hook.active;
  const status = ok ? "delivered" : exhausted ? "failed" : "pending";
  const next = new Date(now.getTime() + backoffSeconds(attempt) * 1000);

  await supabase.from("ef_webhook_attempts").insert({
    delivery_id: delivery.id,
    owner_email: delivery.owner_email,
    attempt,
    ok,
    response_status: responseStatus,
    error,
    duration_ms: Date.now() - started,
  });
  await supabase
    .from("ef_webhook_deliveries")
    .update({
      attempts: attempt,
      status,
      last_error: error,
      last_response_status: responseStatus,
      next_attempt_at: status === "pending" ? next.toISOString() : now.toISOString(),
      updated_at: now.toISOString(),
    })
    .eq("id", delivery.id);
  if (hook) {
    await supabase
      .from("ef_webhooks")
      .update({ last_status: ok ? "ok" : error, last_sent_at: now.toISOString() })
      .eq("id", hook.id);
  }

  return { id: delivery.id, attempt, status, response_status: responseStatus, error };
}

/** Reenvia as entregas pendentes cujo horário de reenvio já chegou. */
export async function processDueDeliveries(supabase: SupabaseClient, limit = 20) {
  const { data } = await supabase
    .from("ef_webhook_deliveries")
    .select("id, webhook_id, owner_email, payload, attempts, max_attempts")
    .eq("status", "pending")
    .lte("next_attempt_at", new Date().toISOString())
    .order("next_attempt_at", { ascending: true })
    .limit(limit);
  const results = [];
  for (const d of (data ?? []) as DeliveryRow[]) {
    results.push(await attemptDelivery(supabase, d));
  }
  return results;
}

/**
 * Enfileira uma entrega por webhook compatível, tenta imediatamente e
 * aproveita para reenviar entregas atrasadas. Falhas nunca quebram a ferramenta.
 */
export async function dispatchWebhooks(
  supabase: SupabaseClient,
  order: OrderPayload,
  extra: { previous_status?: string | null; note?: string | null } = {},
): Promise<{ delivered: number; queued_for_retry: number; failed: number }> {
  const { data: hooks } = await supabase
    .from("ef_webhooks")
    .select("id, event_id, stand_name, owner_email")
    .eq("active", true);

  const targets = (hooks ?? []).filter(
    (h) =>
      (!h.event_id || h.event_id === order.event_id) &&
      (!h.stand_name || h.stand_name.toLowerCase() === order.stand_name.toLowerCase()),
  );

  const counts = { delivered: 0, queued_for_retry: 0, failed: 0 };
  const at = new Date().toISOString();

  for (const hook of targets) {
    const { data: delivery } = await supabase
      .from("ef_webhook_deliveries")
      .insert({
        webhook_id: hook.id,
        owner_email: hook.owner_email,
        order_id: order.id,
        payload: {
          type: "order.status_changed",
          at,
          order,
          previous_status: extra.previous_status ?? null,
          note: extra.note ?? null,
        },
      })
      .select("id, webhook_id, owner_email, payload, attempts, max_attempts")
      .maybeSingle();
    if (!delivery) continue;
    const r = await attemptDelivery(supabase, delivery as DeliveryRow);
    if (r.status === "delivered") counts.delivered += 1;
    else if (r.status === "pending") counts.queued_for_retry += 1;
    else counts.failed += 1;
  }

  await processDueDeliveries(supabase).catch(() => undefined);
  return counts;
}
