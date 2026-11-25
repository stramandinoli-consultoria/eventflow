// Cliente HTTP da API .NET (EventFlow.Api).
// - Envia o JWT em todas as chamadas e renova a sessão sozinho quando o token expira.
// - Erros da API (ProblemDetails) viram ApiError com a mensagem pronta para exibir.

const API_URL = (import.meta.env.VITE_API_URL as string | undefined)?.replace(/\/$/, "") ?? "";
const SESSION_KEY = "eventflow-session";

export interface SessionUser {
  id: string;
  name: string;
  email: string;
  phone?: string | null;
  role: "customer" | "partner" | "admin";
  partnerId?: string | null;
}

interface Session {
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
  user: SessionUser;
}

export class ApiError extends Error {
  constructor(public status: number, message: string, public fields?: Record<string, string[]>) {
    super(message);
  }
}

const storage = {
  get<T>(key: string): T | null {
    try {
      const raw = localStorage.getItem(key);
      return raw ? (JSON.parse(raw) as T) : null;
    } catch {
      return null;
    }
  },
  set(key: string, value: unknown) {
    try {
      if (value === null) localStorage.removeItem(key);
      else localStorage.setItem(key, JSON.stringify(value));
    } catch {
      /* armazenamento indisponível (modo privado etc.) */
    }
  },
};

let session: Session | null = storage.get<Session>(SESSION_KEY);
const sessionListeners = new Set<(user: SessionUser | null) => void>();

function setSession(next: Session | null) {
  session = next;
  storage.set(SESSION_KEY, next);
  sessionListeners.forEach((l) => l(next?.user ?? null));
}

export const auth = {
  currentUser: (): SessionUser | null => session?.user ?? null,
  onChange(listener: (user: SessionUser | null) => void) {
    sessionListeners.add(listener);
    return () => sessionListeners.delete(listener);
  },
  async login(email: string, password: string) {
    setSession(await request<Session>("POST", "/api/auth/login", { email, password }, { auth: false }));
    return session!.user;
  },
  async register(data: { name: string; email: string; password: string; phone?: string }) {
    setSession(await request<Session>("POST", "/api/auth/register", data, { auth: false }));
    return session!.user;
  },
  async logout() {
    const refreshToken = session?.refreshToken;
    setSession(null);
    if (refreshToken) await request("POST", "/api/auth/logout", { refreshToken }, { auth: false }).catch(() => undefined);
  },
  /** Token válido para a próxima chamada (renova se estiver para expirar). */
  async accessToken(): Promise<string | null> {
    if (!session) return null;
    if (new Date(session.accessTokenExpiresAt).getTime() - Date.now() < 60_000) await refresh();
    return session?.accessToken ?? null;
  },
};

let refreshing: Promise<boolean> | null = null;

/** Troca o refresh token por uma nova sessão. Várias chamadas simultâneas compartilham a mesma renovação. */
function refresh(): Promise<boolean> {
  if (!session) return Promise.resolve(false);
  refreshing ??= (async () => {
    try {
      setSession(await request<Session>("POST", "/api/auth/refresh", { refreshToken: session!.refreshToken }, { auth: false }));
      return true;
    } catch {
      setSession(null);
      return false;
    } finally {
      refreshing = null;
    }
  })();
  return refreshing;
}

interface RequestOptions {
  auth?: boolean;
  headers?: Record<string, string>;
}

export async function request<T = unknown>(method: string, path: string, body?: unknown, options: RequestOptions = {}): Promise<T> {
  const useAuth = options.auth !== false;
  const send = async () => {
    const headers: Record<string, string> = { Accept: "application/json", ...options.headers };
    if (body !== undefined) headers["Content-Type"] = "application/json";
    const token = useAuth ? await auth.accessToken() : null;
    if (token) headers.Authorization = `Bearer ${token}`;
    return fetch(`${API_URL}${path}`, { method, headers, body: body === undefined ? undefined : JSON.stringify(body) });
  };

  let response: Response;
  try {
    response = await send();
    if (response.status === 401 && useAuth && session && (await refresh())) response = await send();
  } catch {
    throw new ApiError(0, "Não foi possível conectar ao servidor. Verifique se a API está rodando.");
  }

  if (response.status === 204) return undefined as T;
  const text = await response.text();
  const data = text ? JSON.parse(text) : undefined;
  if (!response.ok) {
    throw new ApiError(response.status, data?.detail ?? data?.title ?? "Algo deu errado. Tente novamente.", data?.errors);
  }
  return data as T;
}

export const apiUrl = API_URL;
export const errorMessage = (error: unknown) =>
  error instanceof Error ? error.message : "Algo deu errado. Tente novamente.";
