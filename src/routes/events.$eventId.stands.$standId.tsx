import { createFileRoute, Link, notFound, useNavigate } from "@tanstack/react-router";
import { useStore } from "@/lib/store";
import { useState } from "react";
import { Star, Plus, Minus, ShoppingBag, Clock, ChevronLeft } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { toast } from "sonner";

export const Route = createFileRoute("/events/$eventId/stands/$standId")({
  component: StandPage,
});

function StandPage() {
  const { eventId, standId } = Route.useParams();
  const navigate = useNavigate();
  const stand = useStore((s) => s.stands.find((x) => x.id === standId));
  const products = useStore(useShallow((s) => s.products.filter((p) => p.standId === standId)));
  const addOrder = useStore((s) => s.addOrder);
  const [cart, setCart] = useState<Record<string, number>>({});
  const [name, setName] = useState("");
  const [phone, setPhone] = useState("");
  if (!stand) throw notFound();

  const inc = (id: string) => setCart((c) => ({ ...c, [id]: (c[id] ?? 0) + 1 }));
  const dec = (id: string) => setCart((c) => ({ ...c, [id]: Math.max(0, (c[id] ?? 0) - 1) }));

  const items = Object.entries(cart)
    .filter(([, q]) => q > 0)
    .map(([id, qty]) => {
      const p = products.find((x) => x.id === id)!;
      return { productId: id, productName: p.name, qty, unitPrice: p.price };
    });
  const total = items.reduce((s, i) => s + i.qty * i.unitPrice, 0);

  const place = () => {
    if (!name || !phone) { toast.error("Informe nome e WhatsApp"); return; }
    if (items.length === 0) { toast.error("Adicione produtos"); return; }
    const order = addOrder({ eventId, standId, standName: stand.name, customerName: name, customerPhone: phone, items, total });
    toast.success(`Pedido #${order.id} criado!`);
    navigate({ to: "/orders/$orderId", params: { orderId: order.id } });
  };

  return (
    <div className="max-w-7xl mx-auto px-6 py-8">
      <Link to="/events/$eventId" params={{ eventId }} className="inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground mb-4">
        <ChevronLeft className="w-4 h-4" /> Voltar ao mapa
      </Link>

      <div className="relative rounded-3xl overflow-hidden h-56 mb-8 border border-border">
        <img src={stand.cover} alt={stand.name} className="w-full h-full object-cover" />
        <div className="absolute inset-0 bg-gradient-to-t from-background via-background/40 to-transparent" />
        <div className="absolute bottom-0 left-0 p-8">
          <h1 className="text-4xl font-bold mb-2">{stand.name}</h1>
          <p className="text-muted-foreground max-w-xl">{stand.description}</p>
          <div className="flex items-center gap-1 mt-2 text-warning"><Star className="w-4 h-4 fill-warning" /> {stand.rating.toFixed(1)}</div>
        </div>
      </div>

      <div className="grid lg:grid-cols-[1fr_380px] gap-8">
        <div>
          <h2 className="text-2xl font-bold mb-4">Cardápio</h2>
          <div className="grid sm:grid-cols-2 gap-4">
            {products.map((p) => (
              <div key={p.id} className="rounded-2xl border border-border gradient-card overflow-hidden">
                <img src={p.image} alt={p.name} className="w-full h-40 object-cover" />
                <div className="p-4">
                  <div className="flex justify-between items-start gap-2 mb-1">
                    <h3 className="font-semibold">{p.name}</h3>
                    <span className="font-bold text-primary">R$ {p.price.toFixed(2)}</span>
                  </div>
                  <p className="text-xs text-muted-foreground line-clamp-2 mb-3">{p.description}</p>
                  <div className="flex items-center justify-between">
                    <span className="text-xs text-muted-foreground flex items-center gap-1"><Clock className="w-3 h-3" />~{p.prepMinutes} min</span>
                    <div className="flex items-center gap-2">
                      <Button size="icon" variant="outline" className="h-8 w-8" onClick={() => dec(p.id)} disabled={!cart[p.id]}><Minus className="w-3 h-3" /></Button>
                      <span className="w-6 text-center text-sm font-semibold">{cart[p.id] ?? 0}</span>
                      <Button size="icon" className="h-8 w-8" onClick={() => inc(p.id)}><Plus className="w-3 h-3" /></Button>
                    </div>
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>

        <aside className="lg:sticky lg:top-20 lg:self-start rounded-2xl border border-border gradient-card p-5">
          <h3 className="font-semibold flex items-center gap-2 mb-4"><ShoppingBag className="w-4 h-4 text-primary" /> Seu pedido</h3>
          {items.length === 0 ? (
            <p className="text-sm text-muted-foreground py-6 text-center">Adicione itens do cardápio</p>
          ) : (
            <div className="space-y-2 mb-4 max-h-60 overflow-auto">
              {items.map((i) => (
                <div key={i.productId} className="flex justify-between text-sm">
                  <span>{i.qty}× {i.productName}</span>
                  <span className="font-medium">R$ {(i.qty * i.unitPrice).toFixed(2)}</span>
                </div>
              ))}
            </div>
          )}
          <div className="flex justify-between font-bold text-lg border-t border-border pt-3 mb-4">
            <span>Total</span><span className="text-primary">R$ {total.toFixed(2)}</span>
          </div>
          <div className="space-y-3 mb-4">
            <div><Label htmlFor="n" className="text-xs">Seu nome</Label><Input id="n" value={name} onChange={(e) => setName(e.target.value)} placeholder="Marina Costa" /></div>
            <div><Label htmlFor="p" className="text-xs">WhatsApp</Label><Input id="p" value={phone} onChange={(e) => setPhone(e.target.value)} placeholder="+55 11 99999-0000" /></div>
          </div>
          <Button className="w-full gradient-hero shadow-glow font-semibold" onClick={place}>Fazer pedido</Button>
        </aside>
      </div>
    </div>
  );
}
