import { createFileRoute, Link, notFound } from "@tanstack/react-router";
import { useStore, standTypeLabel } from "@/lib/store";
import { useShallow } from "zustand/react/shallow";
import { EventMap } from "@/components/EventMap";
import { Calendar, MapPin, Star, ArrowRight } from "lucide-react";
import { Badge } from "@/components/ui/badge";

export const Route = createFileRoute("/events/$eventId/")({
  component: EventPage,
});

function EventPage() {
  const { eventId } = Route.useParams();
  const event = useStore((s) => s.events.find((e) => e.id === eventId));
  const stands = useStore(useShallow((s) => s.stands.filter((x) => x.eventId === eventId && ["food", "drink", "beer", "dessert"].includes(x.category))));
  if (!event) throw notFound();

  return (
    <div className="max-w-7xl mx-auto px-6 py-8">
      <div className="relative rounded-3xl overflow-hidden h-64 mb-8 border border-border">
        <img src={event.banner} alt={event.name} className="w-full h-full object-cover" />
        <div className="absolute inset-0 bg-gradient-to-t from-background via-background/60 to-transparent" />
        <div className="absolute bottom-0 left-0 right-0 p-8">
          <Badge className="mb-3" variant={event.status === "live" ? "default" : "secondary"}>
            {event.status === "live" ? "🔴 Ao vivo agora" : "Em breve"}
          </Badge>
          <h1 className="text-4xl md:text-5xl font-bold mb-3">{event.name}</h1>
          <div className="flex flex-wrap gap-4 text-sm text-muted-foreground">
            <span className="flex items-center gap-2"><Calendar className="w-4 h-4 text-primary" />{event.date}</span>
            <span className="flex items-center gap-2"><MapPin className="w-4 h-4 text-primary" />{event.location}</span>
          </div>
        </div>
      </div>

      <div className="grid lg:grid-cols-[1fr_360px] gap-8">
        <div>
          <div className="flex items-center justify-between mb-4">
            <h2 className="text-2xl font-bold">Mapa do evento</h2>
            <p className="text-sm text-muted-foreground">Clique numa barraca para ver o cardápio</p>
          </div>
          <EventMap eventId={eventId} />
        </div>

        <aside>
          <h2 className="text-2xl font-bold mb-4">Barracas</h2>
          <div className="space-y-3">
            {stands.map((s) => (
              <Link
                key={s.id}
                to="/events/$eventId/stands/$standId"
                params={{ eventId, standId: s.id }}
                className="flex gap-3 p-3 rounded-xl border border-border gradient-card hover:shadow-glow hover:border-primary/50 transition group"
              >
                <img src={s.cover} alt={s.name} className="w-16 h-16 rounded-lg object-cover" />
                <div className="flex-1 min-w-0">
                  <div className="flex items-center justify-between">
                    <p className="font-semibold truncate">{s.name}</p>
                    <ArrowRight className="w-4 h-4 text-muted-foreground group-hover:text-primary group-hover:translate-x-1 transition" />
                  </div>
                  <p className="text-xs text-muted-foreground line-clamp-1">{standTypeLabel[s.category]}</p>
                  <div className="flex items-center gap-1 mt-1 text-xs text-warning">
                    <Star className="w-3 h-3 fill-warning" /> {s.rating.toFixed(1)}
                  </div>
                </div>
              </Link>
            ))}
          </div>
        </aside>
      </div>
    </div>
  );
}
