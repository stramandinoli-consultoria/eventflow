// Wrapper tipado para o namespace beta `supabase.auth.oauth`, usado pela rota
// de consentimento OAuth 2.1. Mantém o código com tipagem em vez de acessar
// APIs internas do SDK.
import { supabase } from "@/integrations/supabase/client";

type OAuthResult<T> = { data: T | null; error: { message: string } | null };

interface AuthorizationDetails {
  client?: { name?: string; client_id?: string; redirect_uri?: string } | null;
  redirect_url?: string | null;
  redirect_to?: string | null;
  scopes?: string[] | null;
}

interface OAuthApi {
  getAuthorizationDetails: (id: string) => Promise<OAuthResult<AuthorizationDetails>>;
  approveAuthorization: (id: string) => Promise<OAuthResult<AuthorizationDetails>>;
  denyAuthorization: (id: string) => Promise<OAuthResult<AuthorizationDetails>>;
}

export function getOAuthApi(): OAuthApi {
  const authAny = (supabase.auth as unknown as { oauth?: OAuthApi }).oauth;
  if (!authAny) {
    throw new Error(
      "Supabase auth.oauth não disponível. Verifique se o servidor OAuth está ativo.",
    );
  }
  return authAny;
}

export type { AuthorizationDetails };
