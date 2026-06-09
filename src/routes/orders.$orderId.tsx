import { createFileRoute, notFound } from "@tanstack/react-router";
import { useStore, statusLabel, type OrderStatus } from "@/lib/store";
import { Check, Clock, ChefHat, Bell, PartyPopper, XCircle, MessageSquare } from "lucide-react";
import { Badge } from "@/components/ui/badge";

export const Route = createFileRoute("/orders/$orderId")({
  component: OrderPage,
});

const ICONS: Record<OrderStatus, any> = {
  awaiting_payment: Clock, preparing: ChefHat, ready: Bell, completed: PartyPopper, cancelled: XCircle,
};

const STEPS: OrderStatus[] = ["awaiting_payment", "preparing", "ready", "completed"];

function OrderPage() {
  const { orderId } = Route.useParams();
  const order = useStore((s) => s.orders.find((o) => o.id === orderId));
  if (!order) throw notFound();

  const currentStep = STEPS.indexOf(order.status);

  return (
    <div className="max-w-3xl mx-auto px-6 py-12">
      <div className="text-center mb-10">
        <Badge className="mb-3">Pedido #{order.id}</Badge>
        <h1 className="text-4xl font-bold mb-2">{statusLabel[order.status]}</h1>
        <p className="text-muted-foreground">{order.standName} • {order.customerName}</p>
      </div>

      {order.status !== "cancelled" && (
        <div className="rounded-2xl border border-border gradient-card p-6 mb-6">
          <div className="flex justify-between mb-4">
            {STEPS.map((st, i) => {
              const Icon = ICONS[st];
              const done = i <= currentStep;
              return (
                <div key={st} className="flex flex-col items-center flex-1 relative">
                  {i < STEPS.length - 1 && (
                    <div className={`absolute top-5 left-1/2 w-full h-0.5 ${i < currentStep ? "bg-primary" : "bg-border"}`} />
                  )}
                  <div className={`relative w-10 h-10 rounded-full grid place-items-center transition ${done ? "gradient-hero shadow-glow" : "bg-secondary border border-border"}`}>
                    <Icon className={`w-5 h-5 ${done ? "text-primary-foreground" : "text-muted-foreground"}`} />
                  </div>
                  <span className={`text-xs mt-2 text-center ${done ? "text-foreground font-medium" : "text-muted-foreground"}`}>{statusLabel[st]}</span>
                </div>
              );
            })}
          </div>
        </div>
      )}

      <div className="rounded-2xl border border-border gradient-card p-6 mb-6">
        <h2 className="font-semibold mb-4">Timeline</h2>
        <div className="space-y-4">
          {order.timeline.map((t, i) => {
            const Icon = ICONS[t.status];
            return (
              <div key={i} className="flex gap-4">
                <div className="w-9 h-9 rounded-full bg-primary/15 text-primary grid place-items-center shrink-0">
                  <Icon className="w-4 h-4" />
                </div>
                <div className="flex-1 pb-4 border-b border-border last:border-0">
                  <p className="font-medium">{statusLabel[t.status]}</p>
                  <p className="text-xs text-muted-foreground">{new Date(t.at).toLocaleString("pt-BR")}</p>
                  {t.note && (
                    <p className="text-xs text-success mt-1 flex items-center gap-1"><MessageSquare className="w-3 h-3" /> {t.note}</p>
                  )}
                </div>
              </div>
            );
          })}
        </div>
      </div>

      <div className="rounded-2xl border border-border gradient-card p-6">
        <h2 className="font-semibold mb-4">Itens do pedido</h2>
        <div className="space-y-2 mb-4">
          {order.items.map((i) => (
            <div key={i.productId} className="flex justify-between text-sm">
              <span>{i.qty}× {i.productName}</span>
              <span className="font-medium">R$ {(i.qty * i.unitPrice).toFixed(2)}</span>
            </div>
          ))}
        </div>
        <div className="flex justify-between font-bold text-lg border-t border-border pt-3">
          <span>Total</span><span className="text-primary">R$ {order.total.toFixed(2)}</span>
        </div>
      </div>

      {order.status === "ready" && (
        <div className="mt-6 p-4 rounded-xl border-2 border-success bg-success/10 text-center">
          <Bell className="w-6 h-6 mx-auto text-success mb-2" />
          <p className="font-semibold text-success">Seu pedido está pronto!</p>
          <p className="text-sm text-muted-foreground">Retire no stand {order.standName}. Enviamos um WhatsApp para você.</p>
        </div>
      )}
    </div>
  );
}
