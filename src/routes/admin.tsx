import { createFileRoute, Link } from "@tanstack/react-router";
import { useStore, statusLabel, standTypeLabel, type OrderStatus } from "@/lib/store";
import { useMemo, useState } from "react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { ArrowRight, DollarSign, ShoppingBag, Users, ChefHat, MapPinned, Trash2, MessageSquare, ChevronRight } from "lucide-react";
import { toast } from "sonner";

export const Route = createFileRoute("/admin")({
  component: Admin,
});

const statusVariant: Record<OrderStatus, "default" | "secondary" | "destructive" | "outline"> = {
  awaiting_payment: "secondary", preparing: "default", ready: "outline", completed: "outline", cancelled: "destructive",
};

function Admin() {
  const events = useStore((s) => s.events);
  const orders = useStore((s) => s.orders);
  const stands = useStore((s) => s.stands);
  const advanceOrder = useStore((s) => s.advanceOrder);
  const cancelOrder = useStore((s) => s.cancelOrder);
  const [filter, setFilter] = useState<OrderStatus | "all">("all");

  const stats = useMemo(() => {
    const revenue = orders.filter((o) => o.status !== "cancelled").reduce((s, o) => s + o.total, 0);
    return {
      revenue,
      orders: orders.length,
      active: orders.filter((o) => ["awaiting_payment", "preparing"].includes(o.status)).length,
      stands: stands.filter((s) => ["food", "drink", "beer", "dessert"].includes(s.category)).length,
    };
  }, [orders, stands]);

  const filtered = filter === "all" ? orders : orders.filter((o) => o.status === filter);

  const ranking = useMemo(() => {
    const map = new Map<string, { name: string; total: number; count: number }>();
    for (const o of orders) {
      if (o.status === "cancelled") continue;
      const cur = map.get(o.standId) ?? { name: o.standName, total: 0, count: 0 };
      cur.total += o.total; cur.count += 1;
      map.set(o.standId, cur);
    }
    return [...map.values()].sort((a, b) => b.total - a.total).slice(0, 5);
  }, [orders]);

  return (
    <div className="max-w-7xl mx-auto px-6 py-10">
      <div className="flex items-center justify-between mb-8">
        <div>
          <h1 className="text-3xl font-bold">Painel Administrativo</h1>
          <p className="text-muted-foreground">Acompanhe vendas, pedidos e gerencie eventos.</p>
        </div>
      </div>

      <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-10">
        {[
          { l: "Receita", v: `R$ ${stats.revenue.toFixed(2)}`, i: DollarSign },
          { l: "Pedidos", v: stats.orders, i: ShoppingBag },
          { l: "Em andamento", v: stats.active, i: ChefHat },
          { l: "Stands ativos", v: stats.stands, i: Users },
        ].map((s) => (
          <div key={s.l} className="rounded-2xl border border-border gradient-card p-5">
            <div className="flex items-center justify-between mb-2">
              <span className="text-xs text-muted-foreground uppercase tracking-wide">{s.l}</span>
              <s.i className="w-4 h-4 text-primary" />
            </div>
            <p className="text-2xl font-bold">{s.v}</p>
          </div>
        ))}
      </div>

      <div className="grid lg:grid-cols-[1fr_360px] gap-8">
        <section>
          <div className="flex items-center justify-between mb-4">
            <h2 className="text-xl font-bold">Pedidos em tempo real</h2>
            <div className="flex gap-1">
              {(["all", "awaiting_payment", "preparing", "ready", "completed"] as const).map((f) => (
                <button
                  key={f}
                  onClick={() => setFilter(f)}
                  className={`text-xs px-3 py-1.5 rounded-lg transition ${filter === f ? "bg-primary text-primary-foreground" : "bg-secondary text-muted-foreground hover:text-foreground"}`}
                >
                  {f === "all" ? "Todos" : statusLabel[f]}
                </button>
              ))}
            </div>
          </div>

          <div className="space-y-3">
            {filtered.map((o) => (
              <div key={o.id} className="rounded-2xl border border-border gradient-card p-4">
                <div className="flex items-start justify-between gap-3 mb-3">
                  <div>
                    <div className="flex items-center gap-2 mb-1">
                      <span className="font-semibold">#{o.id}</span>
                      <Badge variant={statusVariant[o.status]}>{statusLabel[o.status]}</Badge>
                    </div>
                    <p className="text-sm text-muted-foreground">{o.standName} • {o.customerName}</p>
                  </div>
                  <span className="font-bold text-primary">R$ {o.total.toFixed(2)}</span>
                </div>
                <p className="text-xs text-muted-foreground mb-3">
                  {o.items.map((i) => `${i.qty}× ${i.productName}`).join(" · ")}
                </p>
                <div className="flex flex-wrap gap-2">
                  <Link to="/orders/$orderId" params={{ orderId: o.id }}>
                    <Button size="sm" variant="outline">Ver timeline</Button>
                  </Link>
                  {!["completed", "cancelled"].includes(o.status) && (
                    <>
                      <Button
                        size="sm"
                        className="gradient-hero"
                        onClick={() => {
                          advanceOrder(o.id);
                          const next = o.status === "awaiting_payment" ? "preparing" : o.status === "preparing" ? "ready" : "completed";
                          toast.success(`Pedido #${o.id}: ${statusLabel[next as OrderStatus]}`, {
                            description: next === "ready" ? `WhatsApp enviado para ${o.customerPhone}` : undefined,
                            icon: next === "ready" ? <MessageSquare className="w-4 h-4" /> : undefined,
                          });
                        }}
                      >
                        Avançar <ChevronRight className="w-3 h-3 ml-1" />
                      </Button>
                      <Button size="sm" variant="ghost" className="text-destructive" onClick={() => { cancelOrder(o.id); toast.error("Pedido cancelado"); }}>
                        <Trash2 className="w-3 h-3" />
                      </Button>
                    </>
                  )}
                </div>
              </div>
            ))}
            {filtered.length === 0 && <p className="text-sm text-muted-foreground text-center py-8">Nenhum pedido nesse filtro.</p>}
          </div>
        </section>

        <aside className="space-y-6">
          <div className="rounded-2xl border border-border gradient-card p-5">
            <h3 className="font-semibold mb-4">Eventos</h3>
            <div className="space-y-2">
              {events.map((e) => (
                <Link
                  key={e.id}
                  to="/admin/events/$eventId/map"
                  params={{ eventId: e.id }}
                  className="flex items-center justify-between p-3 rounded-lg bg-secondary hover:bg-muted transition group"
                >
                  <div className="flex items-center gap-3 min-w-0">
                    <MapPinned className="w-4 h-4 text-primary shrink-0" />
                    <div className="min-w-0">
                      <p className="text-sm font-medium truncate">{e.name}</p>
                      <p className="text-xs text-muted-foreground">Editar mapa</p>
                    </div>
                  </div>
                  <ArrowRight className="w-4 h-4 text-muted-foreground group-hover:text-primary group-hover:translate-x-1 transition" />
                </Link>
              ))}
            </div>
          </div>

          <div className="rounded-2xl border border-border gradient-card p-5">
            <h3 className="font-semibold mb-4">Ranking de vendas</h3>
            <div className="space-y-3">
              {ranking.map((r, i) => (
                <div key={r.name} className="flex items-center gap-3">
                  <span className="w-6 h-6 rounded-full gradient-hero grid place-items-center text-xs font-bold text-primary-foreground">{i + 1}</span>
                  <div className="flex-1 min-w-0">
                    <p className="text-sm font-medium truncate">{r.name}</p>
                    <p className="text-xs text-muted-foreground">{r.count} pedidos</p>
                  </div>
                  <span className="text-sm font-bold text-primary">R$ {r.total.toFixed(2)}</span>
                </div>
              ))}
              {ranking.length === 0 && <p className="text-xs text-muted-foreground">Sem vendas ainda.</p>}
            </div>
          </div>

          <div className="rounded-2xl border border-border gradient-card p-5">
            <h3 className="font-semibold mb-2">Categorias de stand</h3>
            <div className="flex flex-wrap gap-2">
              {Object.values(standTypeLabel).map((c) => (
                <Badge key={c} variant="secondary" className="text-xs">{c}</Badge>
              ))}
            </div>
          </div>
        </aside>
      </div>
    </div>
  );
}
