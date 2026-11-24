import { createFileRoute, Link, useNavigate } from "@tanstack/react-router";
import { useStore, type EventItem } from "@/lib/store";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Plus, Trash2, MapPinned, ChevronLeft } from "lucide-react";
import { toast } from "sonner";

export const Route = createFileRoute("/admin/events/")({ component: EventsAdmin });

function EventsAdmin() {
  const events = useStore((s) => s.events);
  const upsertEvent = useStore((s) => s.upsertEvent);
  const removeEvent = useStore((s) => s.removeEvent);
  const navigate = useNavigate();
  const [form, setForm] = useState<Partial<EventItem>>({ status: "upcoming", banner: "" });

  const save = () => {
    if (!form.name || !form.date || !form.location) return toast.error("Preencha nome, data e local");
    const ev: EventItem = {
      id: form.id ?? "ev-" + Date.now(),
      name: form.name!, date: form.date!, location: form.location!,
      description: form.description ?? "",
      banner: form.banner || "https://images.unsplash.com/photo-1492684223066-81342ee5ff30?w=1600&q=80",
      status: (form.status as EventItem["status"]) ?? "upcoming",
    };
    upsertEvent(ev);
    setForm({ status: "upcoming", banner: "" });
    toast.success("Evento salvo");
    navigate({ to: "/admin/events/$eventId/map", params: { eventId: ev.id } });
  };

  return (
    <div className="max-w-7xl mx-auto px-6 py-10">
      <Link to="/admin" className="inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground mb-4">
        <ChevronLeft className="w-4 h-4" /> Voltar ao admin
      </Link>
      <h1 className="text-3xl font-bold mb-2">Eventos</h1>
      <p className="text-muted-foreground mb-8">Crie eventos e abra o construtor de mapa drag-and-drop.</p>

      <div className="grid lg:grid-cols-[1fr_380px] gap-8">
        <section className="space-y-3">
          {events.map((e) => (
            <div key={e.id} className="flex gap-4 p-4 rounded-2xl border border-border gradient-card">
              <img src={e.banner} alt={e.name} className="w-24 h-24 rounded-xl object-cover" />
              <div className="flex-1">
                <p className="font-semibold">{e.name}</p>
                <p className="text-xs text-muted-foreground">{e.date} · {e.location}</p>
                <p className="text-xs text-muted-foreground line-clamp-2 mt-1">{e.description}</p>
                <div className="flex gap-2 mt-3">
                  <Link to="/admin/events/$eventId/map" params={{ eventId: e.id }}>
                    <Button size="sm" variant="outline"><MapPinned className="w-3 h-3 mr-1" /> Editar mapa</Button>
                  </Link>
                  <Button size="sm" variant="ghost" onClick={() => setForm(e)}>Editar</Button>
                  <Button size="sm" variant="ghost" className="text-destructive" onClick={() => { removeEvent(e.id); toast.success("Evento removido"); }}>
                    <Trash2 className="w-3 h-3" />
                  </Button>
                </div>
              </div>
            </div>
          ))}
        </section>

        <aside className="rounded-2xl border border-border gradient-card p-5 h-fit sticky top-20">
          <h3 className="font-semibold mb-3 flex items-center gap-2"><Plus className="w-4 h-4 text-primary" /> {form.id ? "Editar" : "Novo"} evento</h3>
          <div className="space-y-3">
            <div><Label>Nome</Label><Input value={form.name ?? ""} onChange={(e) => setForm({ ...form, name: e.target.value })} /></div>
            <div><Label>Data e hora</Label><Input value={form.date ?? ""} onChange={(e) => setForm({ ...form, date: e.target.value })} placeholder="2026-09-10 18:00" /></div>
            <div><Label>Local</Label><Input value={form.location ?? ""} onChange={(e) => setForm({ ...form, location: e.target.value })} /></div>
            <div><Label>Descrição</Label><Textarea rows={3} value={form.description ?? ""} onChange={(e) => setForm({ ...form, description: e.target.value })} /></div>
            <div><Label>URL do banner</Label><Input value={form.banner ?? ""} onChange={(e) => setForm({ ...form, banner: e.target.value })} placeholder="https://..." /></div>
            <div>
              <Label>Status</Label>
              <Select value={form.status} onValueChange={(v) => setForm({ ...form, status: v as EventItem["status"] })}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="upcoming">Em breve</SelectItem>
                  <SelectItem value="live">Ao vivo</SelectItem>
                  <SelectItem value="ended">Encerrado</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <Button className="w-full gradient-hero" onClick={save}>Salvar e abrir mapa</Button>
            {form.id && <Button variant="ghost" className="w-full" onClick={() => setForm({ status: "upcoming", banner: "" })}>Cancelar edição</Button>}
          </div>
        </aside>
      </div>
    </div>
  );
}