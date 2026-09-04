import { supabase } from "@/integrations/supabase/client";
import { useStore, type Order, type OrderItem, type OrderStatus, type TimelineEvent } from "./store";

// Espelha os pedidos do app no banco (tabelas ef_orders / ef_order_timeline)
// para que as ferramentas MCP possam consultar e atualizar status em tempo real.

type DbOrder = {
  id: string;
  event_id: string;
  stand_id: string;
  stand_name: string;
  customer_name: string;
  customer_phone: string | null;
  items: unknown;
  total: number | string;
  status: string;
  created_at: string;
};

type DbTimeline = { order_id: string; status: string; note: string | null; at: string };

let applyingRemote = false;
let started = false;

const snapshot = (o: Order) => JSON.stringify({ s: o.status, t: o.timeline.length });
const lastPushed = new Map<string, string>();

function toOrder(row: DbOrder, timeline: DbTimeline[]): Order {
  return {
    id: row.id,
    eventId: row.event_id,
    standId: row.stand_id,
    standName: row.stand_name,
    customerName: row.customer_name,
    customerPhone: row.customer_phone ?? "",
    items: (Array.isArray(row.items) ? row.items : []) as OrderItem[],
    total: Number(row.total),
    status: row.status as OrderStatus,
    createdAt: row.created_at,
    timeline: timeline
      .filter((t) => t.order_id === row.id)
      .map((t) => ({ status: t.status as OrderStatus, at: t.at, note: t.note ?? undefined })) as TimelineEvent[],
  };
}

async function pull() {
  const { data: rows } = await supabase
    .from("ef_orders")
    .select("*")
    .order("created_at", { ascending: false });
  if (!rows) return;
  const { data: timeline } = await supabase
    .from("ef_order_timeline")
    .select("order_id, status, note, at")
    .order("at", { ascending: true });

  const remote = (rows as unknown as DbOrder[]).map((r) => toOrder(r, (timeline ?? []) as DbTimeline[]));
  if (!remote.length) return;

  applyingRemote = true;
  useStore.setState((st) => {
    const byId = new Map(st.orders.map((o) => [o.id, o] as const));
    for (const r of remote) {
      const local = byId.get(r.id);
      // O banco é a fonte da verdade para status/timeline; mantém itens locais
      // quando o pedido nasceu no navegador.
      byId.set(r.id, local ? { ...local, status: r.status, timeline: r.timeline.length ? r.timeline : local.timeline } : r);
      lastPushed.set(r.id, snapshot(byId.get(r.id)!));
    }
    const merged = Array.from(byId.values()).sort(
      (a, b) => +new Date(b.createdAt) - +new Date(a.createdAt),
    );
    return { orders: merged };
  });
  applyingRemote = false;
}

async function pushOrder(order: Order) {
  await supabase.from("ef_orders").upsert({
    id: order.id,
    event_id: order.eventId,
    stand_id: order.standId,
    stand_name: order.standName,
    customer_name: order.customerName,
    customer_phone: order.customerPhone,
    items: order.items as unknown as import("@/integrations/supabase/types").Json,
    total: order.total,
    status: order.status,
    created_at: order.createdAt,
    updated_at: new Date().toISOString(),
  });

  const { data: existing } = await supabase
    .from("ef_order_timeline")
    .select("status, at")
    .eq("order_id", order.id);
  const known = new Set((existing ?? []).map((t) => `${t.status}|${new Date(t.at).toISOString()}`));
  const missing = order.timeline.filter(
    (t) => !known.has(`${t.status}|${new Date(t.at).toISOString()}`),
  );
  if (missing.length) {
    await supabase.from("ef_order_timeline").insert(
      missing.map((t) => ({ order_id: order.id, status: t.status, note: t.note ?? null, at: t.at })),
    );
  }
  lastPushed.set(order.id, snapshot(order));
}

export function startOrderSync() {
  if (started || typeof window === "undefined") return;
  started = true;

  void (async () => {
    // Envia o que já existe localmente e só depois lê o estado remoto.
    for (const o of useStore.getState().orders) await pushOrder(o);
    await pull();
  })();

  useStore.subscribe((state, prev) => {
    if (applyingRemote || state.orders === prev.orders) return;
    for (const o of state.orders) {
      if (lastPushed.get(o.id) !== snapshot(o)) void pushOrder(o);
    }
  });

  let timer: ReturnType<typeof setTimeout> | null = null;
  const schedulePull = () => {
    if (timer) clearTimeout(timer);
    timer = setTimeout(() => void pull(), 250);
  };

  supabase
    .channel("ef-orders-sync")
    .on("postgres_changes", { event: "*", schema: "public", table: "ef_orders" }, schedulePull)
    .on("postgres_changes", { event: "*", schema: "public", table: "ef_order_timeline" }, schedulePull)
    .subscribe();
}
