import { createFileRoute, Link, notFound } from "@tanstack/react-router";
import { useStore, standTypeLabel, type Stand, type StandType } from "@/lib/store";
import { useShallow } from "zustand/react/shallow";
import { EventMap } from "@/components/EventMap";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { ChevronLeft, Plus, Trash2, Download } from "lucide-react";
import { toast } from "sonner";

export const Route = createFileRoute("/admin/events/$eventId/map")({
  component: MapBuilder,
});

function MapBuilder() {
  const { eventId } = Route.useParams();
  const event = useStore((s) => s.events.find((e) => e.id === eventId));
  const stands = useStore(useShallow((s) => s.stands.filter((x) => x.eventId === eventId)));
  const partners = useStore(useShallow((s) => s.partners.filter((p) => p.eventId === eventId)));
  const upsertStand = useStore((s) => s.upsertStand);
  const removeStand = useStore((s) => s.removeStand);
  const upsertPartner = useStore((s) => s.upsertPartner);
  const [selectedId, setSelectedId] = useState<string | undefined>();
  if (!event) throw notFound();

  const selected = stands.find((s) => s.id === selectedId);
  const linkedPartner = selected ? partners.find((p) => p.standId === selected.id) : null;

  const linkPartner = (partnerId: string) => {
    if (!selected) return;
    // unlink any partner currently on this stand, then link the chosen one
    partners.forEach((p) => {
      if (p.standId === selected.id && p.id !== partnerId) upsertPartner({ ...p, standId: "" });
    });
    const p = partners.find((x) => x.id === partnerId);
    if (p) upsertPartner({ ...p, standId: selected.id });
  };

  const addStand = (type: StandType) => {
    const id = "s" + Date.now();
    const stand: Stand = {
      id, eventId, name: `Novo ${standTypeLabel[type]}`, category: type,
      description: "", cover: "", rating: 4.5, x: 40, y: 40, width: 120, height: 90,
    };
    upsertStand(stand);
    setSelectedId(id);
  };

  const update = (patch: Partial<Stand>) => {
    if (!selected) return;
    upsertStand({ ...selected, ...patch });
  };

  const exportJson = () => {
    const data = {
      eventId,
      map: {
        stands: stands.map((s) => ({ id: s.id, name: s.name, type: s.category, x: s.x, y: s.y, width: s.width, height: s.height })),
      },
    };
    navigator.clipboard.writeText(JSON.stringify(data, null, 2));
    toast.success("Mapa JSON copiado para a área de transferência");
  };

  return (
    <div className="max-w-7xl mx-auto px-4 py-5 sm:px-6 sm:py-8">
      <Link to="/admin" className="inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground mb-4">
        <ChevronLeft className="w-4 h-4" /> Voltar ao admin
      </Link>

      <div className="grid grid-cols-1 gap-4 mb-6 sm:grid-cols-[minmax(0,1fr)_auto] sm:items-center">
        <div className="min-w-0">
          <h1 className="truncate text-2xl font-bold sm:text-3xl">{event.name}</h1>
          <p className="text-muted-foreground">Construtor visual de mapa — arraste para reposicionar.</p>
        </div>
        <Button className="w-full sm:w-auto" variant="outline" onClick={exportJson}><Download className="w-4 h-4 mr-2" /> Exportar JSON</Button>
      </div>

      <div className="grid lg:grid-cols-[1fr_320px] gap-6">
        <div>
          <EventMap eventId={eventId} editable onSelect={(s) => setSelectedId(s.id)} selectedId={selectedId} />
          <p className="text-xs text-muted-foreground mt-3">Toque numa barraca para editar. Arraste para mover (snap a 20px).</p>
        </div>

        <aside className="space-y-5">
          <div className="rounded-2xl border border-border gradient-card p-5">
            <h3 className="font-semibold mb-3 flex items-center gap-2"><Plus className="w-4 h-4 text-primary" /> Adicionar elemento</h3>
            <div className="grid grid-cols-2 gap-2">
              {(Object.keys(standTypeLabel) as StandType[]).map((t) => (
                <Button key={t} size="sm" variant="outline" className="justify-start text-xs" onClick={() => addStand(t)}>
                  + {standTypeLabel[t]}
                </Button>
              ))}
            </div>
          </div>

          {selected ? (
            <div className="rounded-2xl border border-border gradient-card p-5 space-y-3">
              <div className="flex items-center justify-between">
                <h3 className="font-semibold">Editar elemento</h3>
                <Button size="icon" variant="ghost" className="text-destructive h-7 w-7" onClick={() => { removeStand(selected.id); setSelectedId(undefined); }}>
                  <Trash2 className="w-4 h-4" />
                </Button>
              </div>
              <div>
                <Label className="text-xs">Nome</Label>
                <Input value={selected.name} onChange={(e) => update({ name: e.target.value })} />
              </div>
              <div>
                <Label className="text-xs">Categoria</Label>
                <Select value={selected.category} onValueChange={(v: StandType) => update({ category: v })}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {(Object.keys(standTypeLabel) as StandType[]).map((t) => (
                      <SelectItem key={t} value={t}>{standTypeLabel[t]}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="grid grid-cols-2 gap-2">
                <div>
                  <Label className="text-xs">Largura</Label>
                  <Input type="number" value={selected.width} onChange={(e) => update({ width: Number(e.target.value) })} />
                </div>
                <div>
                  <Label className="text-xs">Altura</Label>
                  <Input type="number" value={selected.height} onChange={(e) => update({ height: Number(e.target.value) })} />
                </div>
              </div>
              <div className="space-y-1 pt-2 border-t border-border">
                <Label className="text-xs">Empresa parceira vinculada</Label>
                <Select value={linkedPartner?.id ?? "__none"} onValueChange={(v) => v !== "__none" && linkPartner(v)}>
                  <SelectTrigger><SelectValue placeholder="Nenhuma" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="__none">— Nenhuma —</SelectItem>
                    {partners.map((p) => (
                      <SelectItem key={p.id} value={p.id}>{p.companyName}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                {linkedPartner && (
                  <p className="text-xs text-success">✓ {linkedPartner.companyName} ({linkedPartner.email})</p>
                )}
              </div>
              <div className="text-xs text-muted-foreground pt-2 border-t border-border">
                Posição: x={selected.x}, y={selected.y}
              </div>
            </div>
          ) : (
            <div className="rounded-2xl border border-dashed border-border p-6 text-center text-sm text-muted-foreground">
              Selecione uma barraca no mapa para editar suas propriedades e vincular um parceiro.
            </div>
          )}
        </aside>
      </div>
    </div>
  );
}
