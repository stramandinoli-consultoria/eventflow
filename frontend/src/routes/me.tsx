import { createFileRoute, Link, useNavigate } from "@tanstack/react-router";
import { useStore, useCurrentUser, statusLabel } from "@/lib/store";
import { useShallow } from "zustand/react/shallow";
import { useEffect } from "react";
import { Calendar, MapPin, ArrowRight, Receipt } from "lucide-react";
import { Badge } from "@/components/ui/badge";

export const Route = createFileRoute("/me")({ component: MePage });

function MePage() {
  const user = useCurrentUser();
  const navigate = useNavigate();
  const events = useStore((s) => s.events);
  const myOrders = useStore(useShallow((s) => s.orders.filter((o) => o.customerId === user?.id || o.customerPhone === user?.phone)));

  useEffect(() => {
    if (!user) navigate({ to: "/auth" });
  }, [user, navigate]);

  if (!user) return null;

  return (
    <div className="max-w-7xl mx-auto px-6 py-10">
      <div className="rounded-3xl gradient-hero p-8 mb-10 shadow-elevated">
        <p className="text-primary-foreground/80 text-sm mb-1">Olá,</p>
        <h1 className="text-4xl font-bold text-primary-foreground">{user.name}</h1>
        <p className="text-primary-foreground/80 mt-2">Escolha um evento da semana e faça seu pedido sem fila.</p>
      </div>

      <section className="mb-12">
        <h2 className="text-2xl font-bold mb-4">Eventos da semana</h2>
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          {events.map((ev) => (
            <Link key={ev.id} to="/events/$eventId" params={{ eventId: ev.id }}
              className="group rounded-2xl overflow-hidden border border-border gradient-card hover:shadow-glow transition-all hover:-translate-y-1">
              <div className="relative h-44 overflow-hidden">
                <img src={ev.banner} alt={ev.name} className="w-full h-full object-cover group-hover:scale-110 transition-transform duration-700" />
                <Badge className="absolute top-3 left-3" variant={ev.status === "live" ? "default" : "secondary"}>
                  {ev.status === "live" ? "🔴 Ao vivo" : "Em breve"}
                </Badge>
              </div>
              <div className="p-5">
                <h3 className="font-semibold text-lg mb-2">{ev.name}</h3>
                <div className="space-y-1 text-sm text-muted-foreground">
                  <div className="flex items-center gap-2"><Calendar className="w-4 h-4 text-primary" />{ev.date}</div>
                  <div className="flex items-center gap-2"><MapPin className="w-4 h-4 text-primary" />{ev.location}</div>
                </div>
                <div className="mt-4 flex items-center text-primary text-sm font-medium">
                  Entrar no mapa <ArrowRight className="w-4 h-4 ml-1 group-hover:translate-x-1 transition" />
                </div>
              </div>
            </Link>
          ))}
        </div>
      </section>

      <section>
        <h2 className="text-2xl font-bold mb-4 flex items-center gap-2"><Receipt className="w-6 h-6 text-primary" /> Meus pedidos</h2>
        {myOrders.length === 0 ? (
          <p className="text-sm text-muted-foreground">Você ainda não fez nenhum pedido.</p>
        ) : (
          <div className="space-y-3">
            {myOrders.map((o) => (
              <Link key={o.id} to="/orders/$orderId" params={{ orderId: o.id }}
                className="flex items-center justify-between p-4 rounded-2xl border border-border gradient-card hover:shadow-glow transition">
                <div>
                  <p className="font-semibold">#{o.id} · {o.standName}</p>
                  <p className="text-xs text-muted-foreground">{new Date(o.createdAt).toLocaleString("pt-BR")}</p>
                </div>
                <div className="text-right">
                  <Badge variant={o.status === "ready" ? "default" : "secondary"}>{statusLabel[o.status]}</Badge>
                  <p className="text-sm font-bold text-primary mt-1">R$ {o.total.toFixed(2)}</p>
                </div>
              </Link>
            ))}
          </div>
        )}
      </section>
    </div>
  );
}
