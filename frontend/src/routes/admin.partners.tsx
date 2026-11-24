import { createFileRoute, Link } from "@tanstack/react-router";
import { useStore, standTypeLabel, type Partner, type StandType, type Stand } from "@/lib/store";
import { errorMessage } from "@/lib/api";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Plus, Trash2, Building2, ChevronLeft } from "lucide-react";
import { toast } from "sonner";

export const Route = createFileRoute("/admin/partners")({ component: PartnersAdmin });

const emptyForm: Partial<Partner & { loginPassword: string }> = {
  category: "food", logo: "", loginPassword: "demo123",
};

function PartnersAdmin() {
  const partners = useStore((s) => s.partners);
  const events = useStore((s) => s.events);
  const removePartner = useStore((s) => s.removePartner);
  const saveStand = useStore((s) => s.saveStand);
  const savePartner = useStore((s) => s.savePartner);
  const [form, setForm] = useState<Partial<Partner & { loginPassword: string }>>(emptyForm);

  const save = async () => {
    const required = ["companyName", "cnpj", "contactName", "email", "phone", "address", "eventId", "category"] as const;
    for (const k of required) if (!form[k]) return toast.error(`Preencha: ${k}`);

    const partnerId = form.id ?? "pt-" + Date.now();
    const standId = form.standId ?? "s-" + Date.now();

    const existingStand = useStore.getState().stands.find((s) => s.id === standId);
    const stand: Stand = {
      id: standId, eventId: form.eventId!, name: form.companyName!,
      category: form.category as StandType, description: form.description ?? "",
      cover: form.logo || "https://images.unsplash.com/photo-1546069901-ba9599a7e63c?w=800&q=80",
      rating: 4.5,
      x: existingStand?.x ?? 40 + Math.floor(Math.random() * 20) * 20,
      y: existingStand?.y ?? 40 + Math.floor(Math.random() * 14) * 20,
      width: existingStand?.width ?? 120, height: existingStand?.height ?? 90,
    };

    const partner: Partner = {
      id: partnerId, companyName: form.companyName!, cnpj: form.cnpj!, contactName: form.contactName!,
      email: form.email!, phone: form.phone!, address: form.address!,
      category: form.category as StandType, description: form.description ?? "",
      logo: stand.cover, eventId: form.eventId!, standId,
    };

    try {
      await saveStand(stand);
      // Num cadastro novo, a API cria o login da empresa (senha guardada com hash).
      await savePartner(partner, form.id ? undefined : form.loginPassword || "demo123");
    } catch (error) {
      return toast.error(errorMessage(error));
    }

    setForm(emptyForm);
    toast.success("Parceiro cadastrado! Login criado.");
  };

  return (
    <div className="max-w-7xl mx-auto px-6 py-10">
      <Link to="/admin" className="inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground mb-4">
        <ChevronLeft className="w-4 h-4" /> Voltar ao admin
      </Link>
      <h1 className="text-3xl font-bold mb-2 flex items-center gap-2"><Building2 className="w-7 h-7 text-primary" /> Empresas parceiras</h1>
      <p className="text-muted-foreground mb-8">Cadastre empresas/stands e gere o login da empresa.</p>

      <div className="grid lg:grid-cols-[1fr_420px] gap-8">
        <section className="space-y-3">
          {partners.map((p) => {
            const ev = events.find((e) => e.id === p.eventId);
            return (
              <div key={p.id} className="flex gap-4 p-4 rounded-2xl border border-border gradient-card">
                <img src={p.logo} alt={p.companyName} className="w-20 h-20 rounded-xl object-cover" />
                <div className="flex-1">
                  <p className="font-semibold">{p.companyName}</p>
                  <p className="text-xs text-muted-foreground">CNPJ {p.cnpj} · {standTypeLabel[p.category]}</p>
                  <p className="text-xs text-muted-foreground">{p.contactName} · {p.email} · {p.phone}</p>
                  <p className="text-xs text-primary mt-1">Evento: {ev?.name ?? "—"}</p>
                  <div className="flex gap-2 mt-3">
                    <Button size="sm" variant="ghost" onClick={() => setForm({ ...p, loginPassword: "" })}>Editar</Button>
                    <Button size="sm" variant="ghost" className="text-destructive" onClick={() => { removePartner(p.id); toast.success("Removido"); }}>
                      <Trash2 className="w-3 h-3" />
                    </Button>
                  </div>
                </div>
              </div>
            );
          })}
          {partners.length === 0 && <p className="text-sm text-muted-foreground">Nenhum parceiro cadastrado.</p>}
        </section>

        <aside className="rounded-2xl border border-border gradient-card p-5 h-fit sticky top-20">
          <h3 className="font-semibold mb-3 flex items-center gap-2"><Plus className="w-4 h-4 text-primary" /> {form.id ? "Editar" : "Novo"} parceiro</h3>
          <div className="space-y-3">
            <div><Label>Razão social</Label><Input value={form.companyName ?? ""} onChange={(e) => setForm({ ...form, companyName: e.target.value })} /></div>
            <div className="grid grid-cols-2 gap-2">
              <div><Label>CNPJ</Label><Input value={form.cnpj ?? ""} onChange={(e) => setForm({ ...form, cnpj: e.target.value })} /></div>
              <div>
                <Label>Categoria</Label>
                <Select value={form.category} onValueChange={(v) => setForm({ ...form, category: v as StandType })}>
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {(["food", "drink", "beer", "dessert"] as StandType[]).map((c) => (
                      <SelectItem key={c} value={c}>{standTypeLabel[c]}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>
            <div><Label>Responsável</Label><Input value={form.contactName ?? ""} onChange={(e) => setForm({ ...form, contactName: e.target.value })} /></div>
            <div className="grid grid-cols-2 gap-2">
              <div><Label>E-mail (login)</Label><Input type="email" value={form.email ?? ""} onChange={(e) => setForm({ ...form, email: e.target.value })} /></div>
              <div><Label>WhatsApp</Label><Input value={form.phone ?? ""} onChange={(e) => setForm({ ...form, phone: e.target.value })} /></div>
            </div>
            <div><Label>Endereço</Label><Input value={form.address ?? ""} onChange={(e) => setForm({ ...form, address: e.target.value })} /></div>
            <div><Label>Descrição</Label><Textarea rows={2} value={form.description ?? ""} onChange={(e) => setForm({ ...form, description: e.target.value })} /></div>
            <div><Label>URL do logo</Label><Input value={form.logo ?? ""} onChange={(e) => setForm({ ...form, logo: e.target.value })} placeholder="https://..." /></div>
            <div>
              <Label>Evento</Label>
              <Select value={form.eventId} onValueChange={(v) => setForm({ ...form, eventId: v })}>
                <SelectTrigger><SelectValue placeholder="Selecione" /></SelectTrigger>
                <SelectContent>
                  {events.map((e) => <SelectItem key={e.id} value={e.id}>{e.name}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
            {!form.id && (
              <div><Label>Senha de acesso</Label><Input value={form.loginPassword ?? ""} onChange={(e) => setForm({ ...form, loginPassword: e.target.value })} placeholder="demo123" /></div>
            )}
            <Button className="w-full gradient-hero" onClick={save}>{form.id ? "Atualizar" : "Cadastrar parceiro + login"}</Button>
            {form.id && <Button variant="ghost" className="w-full" onClick={() => setForm(emptyForm)}>Cancelar</Button>}
          </div>
        </aside>
      </div>
    </div>
  );
}
