import { createFileRoute, Link } from "@tanstack/react-router";
import { useStore } from "@/lib/store";
import { Calendar, MapPin, ArrowRight, Sparkles } from "lucide-react";
import { Badge } from "@/components/ui/badge";

export const Route = createFileRoute("/")({
  component: Index,
  head: () => ({
    meta: [
      { title: "EventFlow — Eventos gastronômicos sem fila" },
      { name: "description", content: "Descubra festivais, peça do mapa interativo e acompanhe seu pedido em tempo real." },
    ],
  }),
});

function Index() {
  const events = useStore((s) => s.events);
  return (
    <div className="max-w-7xl mx-auto px-6 py-12">
      <section className="relative rounded-3xl overflow-hidden mb-16 border border-border shadow-elevated">
        <div className="absolute inset-0 gradient-hero opacity-90" />
        <div className="absolute inset-0 bg-[radial-gradient(circle_at_30%_40%,oklch(1_0_0/0.15),transparent)]" />
        <div className="relative px-8 md:px-16 py-20 md:py-28 max-w-3xl">
          <Badge className="bg-background/30 backdrop-blur border-0 text-primary-foreground mb-6">
            <Sparkles className="w-3 h-3 mr-1" /> Mapa interativo + Pedidos em tempo real
          </Badge>
          <h1 className="text-5xl md:text-6xl font-bold text-primary-foreground leading-[1.05] mb-6">
            O festival inteiro<br /> na palma da mão.
          </h1>
          <p className="text-lg text-primary-foreground/90 mb-8 max-w-xl">
            Explore o mapa do evento, peça direto da barraca e receba no WhatsApp quando estiver pronto.
            Adeus filas — boas-vindas ao próximo capítulo dos eventos gastronômicos.
          </p>
          <div className="flex gap-3">
            <a href="#events" className="inline-flex items-center gap-2 px-5 py-3 rounded-xl bg-background text-foreground font-semibold hover:bg-background/90 transition">
              Ver eventos <ArrowRight className="w-4 h-4" />
            </a>
            <Link to="/admin" className="inline-flex items-center px-5 py-3 rounded-xl bg-background/20 backdrop-blur text-primary-foreground font-semibold border border-primary-foreground/20 hover:bg-background/30 transition">
              Sou organizador
            </Link>
          </div>
        </div>
      </section>

      <section id="events">
        <div className="flex items-end justify-between mb-8">
          <div>
            <h2 className="text-3xl font-bold">Eventos em destaque</h2>
            <p className="text-muted-foreground mt-1">Escolha um evento e mergulhe no mapa.</p>
          </div>
        </div>
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          {events.map((ev) => (
            <Link
              key={ev.id}
              to="/events/$eventId"
              params={{ eventId: ev.id }}
              className="group rounded-2xl overflow-hidden border border-border gradient-card hover:shadow-glow transition-all hover:-translate-y-1"
            >
              <div className="relative h-48 overflow-hidden">
                <img src={ev.banner} alt={ev.name} className="w-full h-full object-cover group-hover:scale-110 transition-transform duration-700" />
                <div className="absolute inset-0 bg-gradient-to-t from-card to-transparent" />
                <Badge className="absolute top-3 left-3" variant={ev.status === "live" ? "default" : "secondary"}>
                  {ev.status === "live" ? "🔴 Ao vivo" : ev.status === "upcoming" ? "Em breve" : "Encerrado"}
                </Badge>
              </div>
              <div className="p-5">
                <h3 className="font-semibold text-lg mb-2">{ev.name}</h3>
                <p className="text-sm text-muted-foreground line-clamp-2 mb-4">{ev.description}</p>
                <div className="space-y-2 text-sm text-muted-foreground">
                  <div className="flex items-center gap-2"><Calendar className="w-4 h-4 text-primary" />{ev.date}</div>
                  <div className="flex items-center gap-2"><MapPin className="w-4 h-4 text-primary" />{ev.location}</div>
                </div>
              </div>
            </Link>
          ))}
        </div>
      </section>

      <section className="mt-20 grid md:grid-cols-3 gap-6">
        {[
          { t: "Mapa drag-and-drop", d: "Construa o layout do evento arrastando barracas, palcos e áreas VIP." },
          { t: "Pedidos em tempo real", d: "Stand recebe, prepara e marca como pronto. Cliente é notificado." },
          { t: "WhatsApp + Timeline", d: "Histórico completo do pedido e aviso automático quando estiver pronto." },
        ].map((f) => (
          <div key={f.t} className="rounded-2xl border border-border p-6 gradient-card">
            <div className="w-10 h-10 rounded-lg gradient-hero mb-4 shadow-glow" />
            <h3 className="font-semibold mb-1">{f.t}</h3>
            <p className="text-sm text-muted-foreground">{f.d}</p>
          </div>
        ))}
      </section>
    </div>
  );
}
