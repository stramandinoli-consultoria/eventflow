import { create } from "zustand";
import { persist } from "zustand/middleware";

export type StandType = "food" | "drink" | "beer" | "dessert" | "vip" | "stage" | "restroom" | "entrance" | "cashier";

export interface Product {
  id: string;
  standId: string;
  name: string;
  description: string;
  price: number;
  prepMinutes: number;
  image: string;
  available: boolean;
}

export interface Stand {
  id: string;
  eventId: string;
  name: string;
  category: StandType;
  description: string;
  cover: string;
  rating: number;
  x: number;
  y: number;
  width: number;
  height: number;
}

export interface EventItem {
  id: string;
  name: string;
  banner: string;
  date: string;
  location: string;
  description: string;
  status: "upcoming" | "live" | "ended";
}

export type OrderStatus = "received" | "preparing" | "ready" | "completed" | "cancelled";

export interface OrderItem {
  productId: string;
  productName: string;
  qty: number;
  unitPrice: number;
}

export interface TimelineEvent {
  status: OrderStatus;
  at: string;
  note?: string;
}

export interface Order {
  id: string;
  eventId: string;
  standId: string;
  standName: string;
  customerName: string;
  customerPhone: string;
  items: OrderItem[];
  total: number;
  status: OrderStatus;
  timeline: TimelineEvent[];
  createdAt: string;
}

interface State {
  events: EventItem[];
  stands: Stand[];
  products: Product[];
  orders: Order[];
  upsertStand: (s: Stand) => void;
  moveStand: (id: string, x: number, y: number) => void;
  removeStand: (id: string) => void;
  addOrder: (o: Omit<Order, "id" | "createdAt" | "timeline" | "status">) => Order;
  advanceOrder: (id: string) => void;
  cancelOrder: (id: string) => void;
}

const now = () => new Date().toISOString();

const seedEvents: EventItem[] = [
  {
    id: "ev1",
    name: "Festival Sabores da Cidade",
    banner: "https://images.unsplash.com/photo-1555939594-58d7cb561ad1?w=1600&q=80",
    date: "2026-06-14 17:00",
    location: "Parque Ibirapuera, São Paulo",
    description: "Três dias de comida de rua, cervejas artesanais e shows ao vivo.",
    status: "live",
  },
  {
    id: "ev2",
    name: "Brew & BBQ Fest",
    banner: "https://images.unsplash.com/photo-1504674900247-0877df9cc836?w=1600&q=80",
    date: "2026-07-22 16:00",
    location: "Centro de Eventos, Curitiba",
    description: "O maior encontro de cervejarias independentes e churrasco autoral.",
    status: "upcoming",
  },
  {
    id: "ev3",
    name: "GastroNight Rooftop",
    banner: "https://images.unsplash.com/photo-1414235077428-338989a2e8c0?w=1600&q=80",
    date: "2026-08-03 19:00",
    location: "Edifício Copan, São Paulo",
    description: "Drinks autorais e cozinha internacional sob as estrelas.",
    status: "upcoming",
  },
];

const seedStands: Stand[] = [
  { id: "s1", eventId: "ev1", name: "Taco Loco", category: "food", description: "Tacos mexicanos autênticos com salsas artesanais.", cover: "https://images.unsplash.com/photo-1565299585323-38d6b0865b47?w=800&q=80", rating: 4.8, x: 80, y: 120, width: 120, height: 90 },
  { id: "s2", eventId: "ev1", name: "Beer Station", category: "beer", description: "12 chopps artesanais sempre gelados.", cover: "https://images.unsplash.com/photo-1535958636474-b021ee887b13?w=800&q=80", rating: 4.7, x: 260, y: 80, width: 120, height: 90 },
  { id: "s3", eventId: "ev1", name: "Burger Republic", category: "food", description: "Burgers artesanais 180g, blend exclusivo.", cover: "https://images.unsplash.com/photo-1568901346375-23c9450c58cd?w=800&q=80", rating: 4.9, x: 440, y: 160, width: 120, height: 90 },
  { id: "s4", eventId: "ev1", name: "Sweet Cloud", category: "dessert", description: "Sorvetes, donuts e brownies recém-feitos.", cover: "https://images.unsplash.com/photo-1488477181946-6428a0291777?w=800&q=80", rating: 4.6, x: 620, y: 110, width: 120, height: 90 },
  { id: "s5", eventId: "ev1", name: "Sakura Drinks", category: "drink", description: "Drinks autorais com gin, sake e frutas.", cover: "https://images.unsplash.com/photo-1551024709-8f23befc6f87?w=800&q=80", rating: 4.5, x: 180, y: 290, width: 120, height: 90 },
  { id: "s6", eventId: "ev1", name: "Palco Principal", category: "stage", description: "Shows ao vivo durante todo o evento.", cover: "", rating: 0, x: 380, y: 320, width: 220, height: 100 },
  { id: "s7", eventId: "ev1", name: "Banheiros", category: "restroom", description: "", cover: "", rating: 0, x: 680, y: 290, width: 80, height: 80 },
];

const seedProducts: Product[] = [
  { id: "p1", standId: "s1", name: "Taco al Pastor (3un)", description: "Carne marinada, abacaxi, coentro e cebola roxa.", price: 32, prepMinutes: 8, image: "https://images.unsplash.com/photo-1565299585323-38d6b0865b47?w=600&q=80", available: true },
  { id: "p2", standId: "s1", name: "Nachos Supreme", description: "Tortilhas, queijo derretido, guacamole e jalapeños.", price: 28, prepMinutes: 6, image: "https://images.unsplash.com/photo-1582169296194-e4d644c48063?w=600&q=80", available: true },
  { id: "p3", standId: "s2", name: "Chopp IPA 500ml", description: "Cervejaria local, lúpulo cítrico.", price: 22, prepMinutes: 2, image: "https://images.unsplash.com/photo-1608270586620-248524c67de9?w=600&q=80", available: true },
  { id: "p4", standId: "s2", name: "Chopp Pilsen 500ml", description: "Clássica e refrescante.", price: 18, prepMinutes: 2, image: "https://images.unsplash.com/photo-1535958636474-b021ee887b13?w=600&q=80", available: true },
  { id: "p5", standId: "s3", name: "Smash Burger Duplo", description: "180g, queijo cheddar, picles e molho da casa.", price: 38, prepMinutes: 12, image: "https://images.unsplash.com/photo-1568901346375-23c9450c58cd?w=600&q=80", available: true },
  { id: "p6", standId: "s3", name: "Fritas Trufadas", description: "Batatas rústicas, parmesão e óleo trufado.", price: 24, prepMinutes: 7, image: "https://images.unsplash.com/photo-1573080496219-bb080dd4f877?w=600&q=80", available: true },
  { id: "p7", standId: "s4", name: "Brownie com Sorvete", description: "Brownie quente com sorvete de baunilha.", price: 22, prepMinutes: 4, image: "https://images.unsplash.com/photo-1606313564200-e75d5e30476c?w=600&q=80", available: true },
  { id: "p8", standId: "s5", name: "Gin Tropical", description: "Gin, maracujá, hortelã e gengibre.", price: 32, prepMinutes: 5, image: "https://images.unsplash.com/photo-1551024709-8f23befc6f87?w=600&q=80", available: true },
];

const seedOrders: Order[] = [
  {
    id: "ord-1042",
    eventId: "ev1",
    standId: "s3",
    standName: "Burger Republic",
    customerName: "Marina Costa",
    customerPhone: "+5511999990001",
    items: [{ productId: "p5", productName: "Smash Burger Duplo", qty: 1, unitPrice: 38 }, { productId: "p6", productName: "Fritas Trufadas", qty: 1, unitPrice: 24 }],
    total: 62,
    status: "preparing",
    createdAt: new Date(Date.now() - 1000 * 60 * 7).toISOString(),
    timeline: [
      { status: "received", at: new Date(Date.now() - 1000 * 60 * 7).toISOString() },
      { status: "preparing", at: new Date(Date.now() - 1000 * 60 * 4).toISOString() },
    ],
  },
  {
    id: "ord-1043",
    eventId: "ev1",
    standId: "s2",
    standName: "Beer Station",
    customerName: "Rafael Lima",
    customerPhone: "+5511999990002",
    items: [{ productId: "p3", productName: "Chopp IPA 500ml", qty: 2, unitPrice: 22 }],
    total: 44,
    status: "ready",
    createdAt: new Date(Date.now() - 1000 * 60 * 12).toISOString(),
    timeline: [
      { status: "received", at: new Date(Date.now() - 1000 * 60 * 12).toISOString() },
      { status: "preparing", at: new Date(Date.now() - 1000 * 60 * 10).toISOString() },
      { status: "ready", at: new Date(Date.now() - 1000 * 60 * 2).toISOString(), note: "Notificação WhatsApp enviada" },
    ],
  },
];

const nextStatus: Record<OrderStatus, OrderStatus> = {
  received: "preparing",
  preparing: "ready",
  ready: "completed",
  completed: "completed",
  cancelled: "cancelled",
};

export const useStore = create<State>()(
  persist(
    (set, get) => ({
      events: seedEvents,
      stands: seedStands,
      products: seedProducts,
      orders: seedOrders,
      upsertStand: (s) =>
        set((st) => {
          const exists = st.stands.find((x) => x.id === s.id);
          return { stands: exists ? st.stands.map((x) => (x.id === s.id ? s : x)) : [...st.stands, s] };
        }),
      moveStand: (id, x, y) =>
        set((st) => ({ stands: st.stands.map((s) => (s.id === id ? { ...s, x, y } : s)) })),
      removeStand: (id) => set((st) => ({ stands: st.stands.filter((s) => s.id !== id) })),
      addOrder: (o) => {
        const order: Order = {
          ...o,
          id: "ord-" + Math.floor(1044 + Math.random() * 9000),
          status: "received",
          createdAt: now(),
          timeline: [{ status: "received", at: now() }],
        };
        set((st) => ({ orders: [order, ...st.orders] }));
        return order;
      },
      advanceOrder: (id) =>
        set((st) => ({
          orders: st.orders.map((o) => {
            if (o.id !== id) return o;
            const ns = nextStatus[o.status];
            if (ns === o.status) return o;
            return {
              ...o,
              status: ns,
              timeline: [...o.timeline, { status: ns, at: now(), note: ns === "ready" ? "Notificação WhatsApp enviada" : undefined }],
            };
          }),
        })),
      cancelOrder: (id) =>
        set((st) => ({
          orders: st.orders.map((o) =>
            o.id === id ? { ...o, status: "cancelled", timeline: [...o.timeline, { status: "cancelled", at: now() }] } : o
          ),
        })),
    }),
    { name: "eventflow-store", version: 1 }
  )
);

export const statusLabel: Record<OrderStatus, string> = {
  received: "Pedido Recebido",
  preparing: "Em Preparo",
  ready: "Pronto para Retirada",
  completed: "Finalizado",
  cancelled: "Cancelado",
};

export const standTypeLabel: Record<StandType, string> = {
  food: "Comida",
  drink: "Drinks",
  beer: "Chopp",
  dessert: "Sobremesas",
  vip: "Área VIP",
  stage: "Palco",
  restroom: "Banheiros",
  entrance: "Entrada",
  cashier: "Caixa",
};

export const standTypeColor: Record<StandType, string> = {
  food: "oklch(0.74 0.18 50)",
  drink: "oklch(0.65 0.18 290)",
  beer: "oklch(0.78 0.17 85)",
  dessert: "oklch(0.7 0.18 350)",
  vip: "oklch(0.68 0.2 25)",
  stage: "oklch(0.55 0.05 280)",
  restroom: "oklch(0.5 0.02 240)",
  entrance: "oklch(0.55 0.15 145)",
  cashier: "oklch(0.5 0.1 200)",
};
