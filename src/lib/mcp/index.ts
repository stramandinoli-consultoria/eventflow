import { auth, defineMcp } from "@lovable.dev/mcp-js";

import whoamiTool from "./tools/whoami";
import listEventsTool from "./tools/list-events";

// A URL do Supabase é reescrita para o proxy .lovable.cloud na publicação,
// e mcp-js rejeitaria essa URL como issuer (RFC 8414). O project ref é o
// único valor que sobrevive à publicação sem reescrita.
const projectRef = import.meta.env.VITE_SUPABASE_PROJECT_ID ?? "project-ref-unset";

export default defineMcp({
  name: "eventflow-mcp",
  title: "EventFlow MCP",
  version: "0.1.0",
  instructions:
    "Ferramentas do EventFlow para clientes MCP autenticados. Use `whoami` para validar a conexão e `list_events` para listar eventos.",
  auth: auth.oauth.issuer({
    issuer: `https://${projectRef}.supabase.co/auth/v1`,
    acceptedAudiences: "authenticated",
  }),
  tools: [whoamiTool, listEventsTool],
});
