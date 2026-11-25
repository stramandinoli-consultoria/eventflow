import { create } from "zustand";
import { toast } from "sonner";
import { HubConnectionBuilder, HubConnectionState, LogLevel, type HubConnection } from "@microsoft/signalr";
import { apiUrl, auth, errorMessage, request, type SessionUser } from "./api";

// Estado do app. A interface (useStore, tipos e ações) é a mesma da versão
// anterior, para as telas não mudarem; a diferença é que os dados agora vêm
// da API .NET e as mudanças chegam em tempo real pelo SignalR.

export type StandType = "food" | "drink" | "beer" | "dessert" | "vip" | "stage" | "restroom" | "entrance" | "cashier";
export type UserRole = "customer" | "partner" | "admin";

export interface User {
  id: string;
  name: string;
  email: string;
  phone?: string;
  role: UserRole;
  partnerId?: string; // when role=partner, links to Partner
}

export interface Partner {
  id: string;
  companyName: string;
  cnpj: string;
  contactName: string;
  email: string;
  phone: string;
  address: string;
  category: StandType;
  description: string;
  logo: string;
  eventId: string;   // which event they belong to
  standId: string;   // their stand on the map
}

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

export type OrderStatus = "awaiting_payment" | "preparing" | "ready" | "completed" | "cancelled";

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
  customerId?: string;
  customerName: string;
  customerPhone: string;
  items: OrderItem[];
  total: number;
  status: OrderStatus;
  timeline: TimelineEvent[];
  createdAt: string;
}

export type NewOrder = Omit<Order, "id" | "createdAt" | "timeline" | "status">;

interface State {
  events: EventItem[];
  stands: Stand[];
  products: Product[];
  orders: Order[];
  partners: Partner[];
  currentUser: User | null;
  currentUserId: string | null;
  loaded: boolean;

  // auth
  login: (email: string, password: string) => Promise<User | null>;
  register: (u: { name: string; email: string; password: string; phone?: string }) => Promise<User>;
  logout: () => void;

  // events
  upsertEvent: (e: EventItem) => void;
  removeEvent: (id: string) => void;

  // partners
  upsertPartner: (p: Partner) => void;
  /** Cadastra/atualiza o parceiro e, num cadastro novo, cria o login da empresa. */
  savePartner: (p: Partner, loginPassword?: string) => Promise<void>;
  removePartner: (id: string) => void;

  // products
  upsertProduct: (p: Product) => void;
  removeProduct: (id: string) => void;

  // stands
  upsertStand: (s: Stand) => void;
  /** Salva o stand imediatamente (sem esperar a digitação parar). */
  saveStand: (s: Stand) => Promise<void>;
  moveStand: (id: string, x: number, y: number) => void;
  removeStand: (id: string) => void;

  // orders
  addOrder: (o: NewOrder) => Promise<Order>;
  advanceOrder: (id: string) => void;
  cancelOrder: (id: string) => void;
}

// ---------- Conversões ----------
const toUser = (u: SessionUser | null): User | null =>
  u ? { id: u.id, name: u.name, email: u.email, phone: u.phone ?? undefined, role: u.role, partnerId: u.partnerId ?? undefined } : null;

type ApiOrder = Omit<Order, "customerId" | "timeline"> & {
  customerId?: string | null;
  timeline: { status: OrderStatus; at: string; note?: string | null }[];
};
const toOrder = (o: ApiOrder): Order => ({
  ...o,
  customerId: o.customerId ?? undefined,
  timeline: o.timeline.map((t) => ({ status: t.status, at: t.at, note: t.note ?? undefined })),
});

const byCreatedDesc = (a: Order, b: Order) => +new Date(b.createdAt) - +new Date(a.createdAt);
const upsertById = <T extends { id: string }>(list: T[], item: T) =>
  list.some((x) => x.id === item.id) ? list.map((x) => (x.id === item.id ? item : x)) : [...list, item];

// ---------- Chaves dos pedidos feitos sem login (guardadas neste aparelho) ----------
const ORDER_KEYS = "eventflow-order-keys";
const orderKeys = {
  all(): Record<string, string> {
    try { return JSON.parse(localStorage.getItem(ORDER_KEYS) ?? "{}"); } catch { return {}; }
  },
  add(id: string, key: string) {
    try { localStorage.setItem(ORDER_KEYS, JSON.stringify({ ...orderKeys.all(), [id]: key })); } catch { /* ignore */ }
  },
};

// ---------- Escritas com espera (editor de mapa salva enquanto o usuário arrasta/digita) ----------
const pendingStandWrites = new Map<string, ReturnType<typeof setTimeout>>();
function debounceStandWrite(id: string, write: () => Promise<unknown>, delay = 350) {
  clearTimeout(pendingStandWrites.get(id));
  pendingStandWrites.set(id, setTimeout(() => {
    pendingStandWrites.delete(id);
    write().catch(fail);
  }, delay));
}

function fail(error: unknown) {
  toast.error(errorMessage(error));
  void loadCatalog();
  void loadOrders();
}

const run = (promise: Promise<unknown>) => { promise.catch(fail); };

const standBody = (s: Stand) => ({
  eventId: s.eventId, name: s.name, category: s.category, description: s.description, cover: s.cover,
  rating: s.rating, x: s.x, y: s.y, width: s.width, height: s.height,
});

const partnerBody = (p: Partner, loginPassword?: string) => ({
  companyName: p.companyName, cnpj: p.cnpj, contactName: p.contactName, email: p.email, phone: p.phone,
  address: p.address, category: p.category, description: p.description, logo: p.logo, eventId: p.eventId,
  standId: p.standId || null, loginPassword: loginPassword || null,
});

export const useStore = create<State>()((set, get) => ({
  events: [],
  stands: [],
  products: [],
  orders: [],
  partners: [],
  currentUser: toUser(auth.currentUser()),
  currentUserId: auth.currentUser()?.id ?? null,
  loaded: false,

  login: async (email, password) => {
    try {
      return toUser(await auth.login(email, password));
    } catch {
      return null;
    }
  },
  register: async (u) => toUser(await auth.register(u))!,
  logout: () => { void auth.logout(); },

  upsertEvent: (e) => {
    set((st) => ({ events: upsertById(st.events, e) }));
    run(request("PUT", `/api/events/${encodeURIComponent(e.id)}`, {
      name: e.name, date: e.date, location: e.location, description: e.description, banner: e.banner, status: e.status,
    }));
  },
  removeEvent: (id) => {
    set((st) => ({ events: st.events.filter((e) => e.id !== id), stands: st.stands.filter((s) => s.eventId !== id) }));
    run(request("DELETE", `/api/events/${encodeURIComponent(id)}`));
  },

  upsertPartner: (p) => {
    const current = get().partners.find((x) => x.id === p.id);
    set((st) => ({ partners: upsertById(st.partners, p) }));
    const onlyStandChanged = current && JSON.stringify({ ...current, standId: p.standId }) === JSON.stringify(p);
    run(onlyStandChanged
      ? request("PUT", `/api/partners/${encodeURIComponent(p.id)}/stand`, { standId: p.standId || null })
      : request("PUT", `/api/partners/${encodeURIComponent(p.id)}`, partnerBody(p)));
  },
  savePartner: async (p, loginPassword) => {
    const saved = await request<Partner>("PUT", `/api/partners/${encodeURIComponent(p.id)}`, partnerBody(p, loginPassword));
    set((st) => ({ partners: upsertById(st.partners, saved) }));
  },
  removePartner: (id) => {
    set((st) => ({ partners: st.partners.filter((p) => p.id !== id) }));
    run(request("DELETE", `/api/partners/${encodeURIComponent(id)}`));
  },

  upsertProduct: (p) => {
    set((st) => ({ products: upsertById(st.products, p) }));
    run(request("PUT", `/api/products/${encodeURIComponent(p.id)}`, {
      standId: p.standId, name: p.name, description: p.description, price: p.price, prepMinutes: p.prepMinutes,
      image: p.image, available: p.available,
    }));
  },
  removeProduct: (id) => {
    set((st) => ({ products: st.products.filter((p) => p.id !== id) }));
    run(request("DELETE", `/api/products/${encodeURIComponent(id)}`));
  },

  upsertStand: (s) => {
    set((st) => ({ stands: upsertById(st.stands, s) }));
    debounceStandWrite(s.id, () => request("PUT", `/api/stands/${encodeURIComponent(s.id)}`, standBody(get().stands.find((x) => x.id === s.id) ?? s)));
  },
  saveStand: async (s) => {
    clearTimeout(pendingStandWrites.get(s.id));
    pendingStandWrites.delete(s.id);
    const saved = await request<Stand>("PUT", `/api/stands/${encodeURIComponent(s.id)}`, standBody(s));
    set((st) => ({ stands: upsertById(st.stands, saved) }));
  },
  moveStand: (id, x, y) => {
    set((st) => ({ stands: st.stands.map((s) => (s.id === id ? { ...s, x, y } : s)) }));
    // Se o stand ainda não foi criado no servidor, o PUT pendente já leva a posição nova.
    const write = pendingStandWrites.has(id)
      ? () => request("PUT", `/api/stands/${encodeURIComponent(id)}`, standBody(get().stands.find((s) => s.id === id)!))
      : () => { const s = get().stands.find((st) => st.id === id)!; return request("PATCH", `/api/stands/${encodeURIComponent(id)}/position`, { x: s.x, y: s.y }); };
    debounceStandWrite(id, write, 250);
  },
  removeStand: (id) => {
    clearTimeout(pendingStandWrites.get(id));
    pendingStandWrites.delete(id);
    set((st) => ({ stands: st.stands.filter((s) => s.id !== id) }));
    run(request("DELETE", `/api/stands/${encodeURIComponent(id)}`));
  },

  addOrder: async (o) => {
    const placed = await request<{ order: ApiOrder; accessKey: string }>("POST", "/api/orders", {
      standId: o.standId, customerName: o.customerName, customerPhone: o.customerPhone,
      items: o.items.map((i) => ({ productId: i.productId, qty: i.qty })),
    });
    const order = toOrder(placed.order);
    orderKeys.add(order.id, placed.accessKey);
    set((st) => ({ orders: [order, ...st.orders.filter((x) => x.id !== order.id)] }));
    void joinOrder(order.id, placed.accessKey);
    return order;
  },
  advanceOrder: (id) => {
    run(request<ApiOrder>("POST", `/api/orders/${encodeURIComponent(id)}/advance`).then(applyOrder));
  },
  cancelOrder: (id) => {
    run(request<ApiOrder>("POST", `/api/orders/${encodeURIComponent(id)}/cancel`).then(applyOrder));
  },
}));

function applyOrder(o: ApiOrder) {
  const order = toOrder(o);
  useStore.setState((st) => ({ orders: upsertById(st.orders, order).sort(byCreatedDesc) }));
}

// ---------- Carga dos dados ----------
async function loadCatalog() {
  const [events, stands, products, partners] = await Promise.all([
    request<EventItem[]>("GET", "/api/events"),
    request<Stand[]>("GET", "/api/stands"),
    request<Product[]>("GET", "/api/products"),
    request<Partner[]>("GET", "/api/partners"),
  ]);
  useStore.setState((st) => ({
    events,
    products,
    partners,
    // Stands com alteração ainda não enviada mantêm a versão local (evita "pulos" ao arrastar).
    stands: [
      ...stands.map((s) => (pendingStandWrites.has(s.id) ? st.stands.find((x) => x.id === s.id) ?? s : s)),
      ...st.stands.filter((s) => pendingStandWrites.has(s.id) && !stands.some((x) => x.id === s.id)),
    ],
  }));
}

async function loadOrders() {
  const keys = Object.entries(orderKeys.all()).map(([id, key]) => ({ id, key }));
  const [mine, keyed] = await Promise.all([
    auth.currentUser() ? request<{ items: ApiOrder[] }>("GET", "/api/orders?limit=200").then((p) => p.items) : Promise.resolve([]),
    keys.length ? request<ApiOrder[]>("POST", "/api/orders/lookup", { orders: keys }) : Promise.resolve([]),
  ]);
  const merged = new Map<string, Order>();
  for (const o of [...mine, ...keyed]) merged.set(o.id, toOrder(o));
  useStore.setState({ orders: [...merged.values()].sort(byCreatedDesc) });
}

async function loadAll() {
  await Promise.all([loadCatalog(), loadOrders()]);
}

// ---------- Tempo real (SignalR) ----------
let hub: HubConnection | null = null;
let catalogTimer: ReturnType<typeof setTimeout> | undefined;

function joinOrder(id: string, key: string) {
  if (hub?.state !== HubConnectionState.Connected) return Promise.resolve();
  return hub.invoke("JoinOrder", id, key).catch(() => undefined);
}

async function connectHub() {
  await hub?.stop().catch(() => undefined);
  const connection = new HubConnectionBuilder()
    .withUrl(`${apiUrl}/hubs/orders`, { accessTokenFactory: async () => (await auth.accessToken()) ?? "" })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build();
  hub = connection;

  connection.on("orderChanged", (o: ApiOrder) => applyOrder(o));
  connection.on("catalogChanged", () => {
    clearTimeout(catalogTimer);
    catalogTimer = setTimeout(() => void loadCatalog().catch(() => undefined), 300);
  });
  const joinKeyedOrders = () =>
    Promise.all(Object.entries(orderKeys.all()).map(([id, key]) => joinOrder(id, key)));
  connection.onreconnected(() => { void joinKeyedOrders(); void loadAll().catch(() => undefined); });

  try {
    await connection.start();
    await joinKeyedOrders();
  } catch {
    /* sem tempo real; os dados continuam vindo pela API */
  }
}

let started = false;

/** Carrega os dados da API e liga o tempo real. Resolve quando a primeira carga termina. */
export async function startSync() {
  if (started) return;
  started = true;

  auth.onChange((user) => {
    useStore.setState({ currentUser: toUser(user), currentUserId: user?.id ?? null });
    // Novo usuário: outros pedidos visíveis e outros grupos de tempo real.
    void loadOrders().catch(() => undefined);
    void connectHub();
  });

  try {
    await loadAll();
  } catch (error) {
    toast.error(errorMessage(error));
  } finally {
    useStore.setState({ loaded: true });
  }
  void connectHub();
}

export const statusLabel: Record<OrderStatus, string> = {
  awaiting_payment: "Aguardando pagamento",
  preparing: "Em preparo",
  ready: "Pronto para retirada",
  completed: "Finalizado",
  cancelled: "Cancelado",
};

export const standTypeLabel: Record<StandType, string> = {
  food: "Comida", drink: "Drinks", beer: "Chopp", dessert: "Sobremesas",
  vip: "Área VIP", stage: "Palco", restroom: "Banheiros", entrance: "Entrada", cashier: "Caixa",
};

export const standTypeColor: Record<StandType, string> = {
  food: "oklch(0.74 0.18 50)", drink: "oklch(0.65 0.18 290)", beer: "oklch(0.78 0.17 85)",
  dessert: "oklch(0.7 0.18 350)", vip: "oklch(0.68 0.2 25)", stage: "oklch(0.55 0.05 280)",
  restroom: "oklch(0.5 0.02 240)", entrance: "oklch(0.55 0.15 145)", cashier: "oklch(0.5 0.1 200)",
};

export const useCurrentUser = () => useStore((s) => s.currentUser);
