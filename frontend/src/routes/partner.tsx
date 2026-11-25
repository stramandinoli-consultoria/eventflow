import { createFileRoute, Link, useNavigate } from "@tanstack/react-router";
import { useStore, useCurrentUser, statusLabel, type Product, type OrderStatus } from "@/lib/store";
import { useShallow } from "zustand/react/shallow";
import { useEffect, useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Badge } from "@/components/ui/badge";
import { Plus, Trash2, ChevronRight, MessageSquare, Package, ChefHat, DollarSign } from "lucide-react";
import { toast } from "sonner";

export const Route = createFileRoute("/partner")({ component: PartnerPage });

const statusVariant: Record<OrderStatus, "default" | "secondary" | "destructive" | "outline"> = {
  awaiting_payment: "destructive", preparing: "default", ready: "outline", completed: "outline", cancelled: "destructive",
};

function PartnerPage() {
  const user = useCurrentUser();
  const navigate = useNavigate();
  const partner = useStore((s) => (user?.partnerId ? s.partners.find((p) => p.id === user.partnerId) : null));
  const stand = useStore((s) => (partner ? s.stands.find((x) => x.id === partner.standId) : null));
  const products = useStore(useShallow((s) => (partner ? s.products.filter((p) => p.standId === partner.standId) : [])));
  const orders = useStore(useShallow((s) => (partner ? s.orders.filter((o) => o.standId === partner.standId) : [])));
  const upsertProduct = useStore((s) => s.upsertProduct);
  const removeProduct = useStore((s) => s.removeProduct);
  const advanceOrder = useStore((s) => s.advanceOrder);
  const cancelOrder = useStore((s) => s.cancelOrder);

  const [form, setForm] = useState<Partial<Product>>({ name: "", description: "", price: 0, prepMinutes: 5, image: "", available: true });

  useEffect(() => {
    if (!user) navigate({ to: "/auth" });
    else if (user.role !== "partner") navigate({ to: "/" });
  }, [user, navigate]);

  if (!user || !partner || !stand) return null;

  const saveProduct = () => {
    if (!form.name || !form.price) return toast.error("Nome e preço obrigatórios");
    upsertProduct({
      id: form.id ?? "pr-" + Date.now(), standId: partner.standId,
      name: form.name!, description: form.description ?? "", price: Number(form.price),
      prepMinutes: Number(form.prepMinutes ?? 5),
      image: form.image || "https://images.unsplash.com/photo-1546069901-ba9599a7e63c?w=600&q=80",
      available: form.available ?? true,
    });
    setForm({ name: "", description: "", price: 0, prepMinutes: 5, image: "", available: true });
    toast.success("Produto salvo");
  };

  const active = orders.filter((o) => !["completed", "cancelled"].includes(o.status));
  const revenue = orders.filter((o) => o.status !== "cancelled").reduce((s, o) => s + o.total, 0);

  return (
    <div className="max-w-7xl mx-auto px-6 py-10">
      <div className="flex items-center gap-4 mb-8">
        <img src={partner.logo} className="w-16 h-16 rounded-2xl object-cover border border-border" alt={partner.companyName} />
        <div>
          <h1 className="text-3xl font-bold">{partner.companyName}</h1>
          <p className="text-muted-foreground text-sm">Stand: {stand.name} · {partner.contactName}</p>
        </div>
      </div>

      <div className="grid grid-cols-2 md:grid-cols-3 gap-4 mb-10">
        {[
          { l: "Pedidos ativos", v: active.length, i: ChefHat },
          { l: "Receita", v: `R$ ${revenue.toFixed(2)}`, i: DollarSign },
          { l: "Produtos", v: products.length, i: Package },
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

      <div className="grid lg:grid-cols-[1fr_380px] gap-8">
        <section>
          <h2 className="text-xl font-bold mb-4">Fila de pedidos em tempo real</h2>
          <div className="space-y-3">
            {orders.map((o) => (
              <div key={o.id} className="rounded-2xl border border-border gradient-card p-4">
                <div className="flex items-start justify-between gap-3 mb-3">
                  <div>
                    <div className="flex items-center gap-2 mb-1">
                      <span className="font-semibold">#{o.id}</span>
                      <Badge variant={statusVariant[o.status]}>{statusLabel[o.status]}</Badge>
                    </div>
                    <p className="text-sm text-muted-foreground">{o.customerName} · {o.customerPhone}</p>
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
                      <Button size="sm" className="gradient-hero" onClick={() => {
                        advanceOrder(o.id);
                        const label = o.status === "awaiting_payment" ? "Pagamento confirmado → preparando" : o.status === "preparing" ? "Pedido pronto! WhatsApp enviado" : "Finalizado";
                        toast.success(`#${o.id}: ${label}`, { icon: o.status === "preparing" ? <MessageSquare className="w-4 h-4" /> : undefined });
                      }}>
                        {o.status === "awaiting_payment" ? "Confirmar pagamento" : o.status === "preparing" ? "Marcar pronto" : "Concluir"}
                        <ChevronRight className="w-3 h-3 ml-1" />
                      </Button>
                      <Button size="sm" variant="ghost" className="text-destructive" onClick={() => { cancelOrder(o.id); toast.error("Pedido cancelado"); }}>
                        <Trash2 className="w-3 h-3" />
                      </Button>
                    </>
                  )}
                </div>
              </div>
            ))}
            {orders.length === 0 && <p className="text-sm text-muted-foreground text-center py-8">Nenhum pedido ainda.</p>}
          </div>
        </section>

        <aside className="space-y-6">
          <div className="rounded-2xl border border-border gradient-card p-5">
            <h3 className="font-semibold mb-3 flex items-center gap-2"><Plus className="w-4 h-4 text-primary" /> Novo produto</h3>
            <div className="space-y-3">
              <div><Label className="text-xs">Nome</Label><Input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} /></div>
              <div><Label className="text-xs">Descrição</Label><Textarea rows={2} value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} /></div>
              <div className="grid grid-cols-2 gap-2">
                <div><Label className="text-xs">Preço R$</Label><Input type="number" step="0.01" value={form.price} onChange={(e) => setForm({ ...form, price: Number(e.target.value) })} /></div>
                <div><Label className="text-xs">Preparo (min)</Label><Input type="number" value={form.prepMinutes} onChange={(e) => setForm({ ...form, prepMinutes: Number(e.target.value) })} /></div>
              </div>
              <div><Label className="text-xs">URL da imagem</Label><Input value={form.image} onChange={(e) => setForm({ ...form, image: e.target.value })} placeholder="https://..." /></div>
              <Button className="w-full gradient-hero" onClick={saveProduct}>{form.id ? "Atualizar" : "Adicionar"} produto</Button>
            </div>
          </div>

          <div className="rounded-2xl border border-border gradient-card p-5">
            <h3 className="font-semibold mb-3">Cardápio ({products.length})</h3>
            <div className="space-y-2 max-h-96 overflow-auto">
              {products.map((p) => (
                <div key={p.id} className="flex gap-3 p-2 rounded-lg bg-secondary/50 border border-border">
                  <img src={p.image} alt={p.name} className="w-12 h-12 rounded object-cover" />
                  <div className="flex-1 min-w-0">
                    <p className="text-sm font-medium truncate">{p.name}</p>
                    <p className="text-xs text-primary font-bold">R$ {p.price.toFixed(2)}</p>
                  </div>
                  <div className="flex gap-1">
                    <Button size="icon" variant="ghost" className="h-7 w-7" onClick={() => setForm(p)}>✎</Button>
                    <Button size="icon" variant="ghost" className="h-7 w-7 text-destructive" onClick={() => { removeProduct(p.id); toast.success("Removido"); }}>
                      <Trash2 className="w-3 h-3" />
                    </Button>
                  </div>
                </div>
              ))}
              {products.length === 0 && <p className="text-xs text-muted-foreground text-center py-4">Nenhum produto cadastrado.</p>}
            </div>
          </div>
        </aside>
      </div>
    </div>
  );
}
