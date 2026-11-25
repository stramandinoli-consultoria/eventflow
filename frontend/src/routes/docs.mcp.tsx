import { createFileRoute } from "@tanstack/react-router";
import manifest from "@/lib/mcp/manifest.json";
import { MCP_EXAMPLES, VERIFY_SNIPPET, WEBHOOK_BODY_EXAMPLE } from "@/lib/mcp/docs-examples";

type Prop = { type?: string; description?: string; enum?: string[]; minimum?: number; maximum?: number; items?: { type?: string } };
type Tool = {
  name: string;
  title?: string;
  description?: string;
  annotations?: { readOnlyHint?: boolean; destructiveHint?: boolean };
  inputSchema: { properties?: Record<string, Prop>; required?: string[] };
};

export const Route = createFileRoute("/docs/mcp")({
  head: () => ({
    meta: [
      { title: "Documentação MCP — EventFlow" },
      { name: "description", content: "Ferramentas MCP do EventFlow com parâmetros e exemplos de request/response para integrar agentes de IA." },
      { property: "og:title", content: "Documentação MCP — EventFlow" },
      { property: "og:description", content: "Ferramentas MCP com exemplos de request/response, webhooks assinados e reenvio automático." },
      { property: "og:type", content: "article" },
      { name: "twitter:card", content: "summary" },
    ],
  }),
  component: McpDocs,
});

const pretty = (v: unknown) => (typeof v === "string" ? v : JSON.stringify(v, null, 2));

function Code({ children }: { children: string }) {
  return <pre className="overflow-x-auto rounded-lg border border-border bg-muted/40 p-3 text-xs leading-relaxed">{children}</pre>;
}

function McpDocs() {
  const tools = manifest.mcp.tools as unknown as Tool[];
  const endpoint = typeof window !== "undefined" ? `${window.location.origin}${manifest.path}` : manifest.path;
  return (
    <div className="mx-auto max-w-5xl px-4 py-10 space-y-10">
      <header className="space-y-3">
        <p className="text-sm font-medium text-primary">Integração com agentes</p>
        <h1 className="text-3xl font-bold">Documentação MCP — {manifest.mcp.server.title}</h1>
        <p className="text-muted-foreground">
          Versão {manifest.mcp.server.version} · {tools.length} ferramentas · gerada automaticamente a partir do catálogo do servidor.
        </p>
        <div className="rounded-lg border border-border bg-card p-4 text-sm space-y-1">
          <div><strong>Endpoint:</strong> <code>{endpoint}</code></div>
          <div><strong>Autenticação:</strong> Bearer JWT — o mesmo token da API, obtido em <code>POST /api/auth/login</code>.</div>
        </div>
        <nav className="flex flex-wrap gap-2 pt-2">
          {tools.map((t) => (
            <a key={t.name} href={`#${t.name}`} className="rounded-full border border-border px-3 py-1 text-xs hover:bg-muted">{t.name}</a>
          ))}
          <a href="#webhooks" className="rounded-full border border-primary px-3 py-1 text-xs text-primary">webhooks</a>
        </nav>
      </header>

      {tools.map((t) => {
        const props = Object.entries(t.inputSchema.properties ?? {});
        const required = new Set(t.inputSchema.required ?? []);
        const ex = MCP_EXAMPLES[t.name];
        return (
          <section key={t.name} id={t.name} className="scroll-mt-20 rounded-xl border border-border bg-card p-6 space-y-4">
            <div className="flex flex-wrap items-center gap-2">
              <h2 className="text-xl font-semibold font-mono">{t.name}</h2>
              {t.annotations?.readOnlyHint ? (
                <span className="rounded bg-secondary px-2 py-0.5 text-xs">somente leitura</span>
              ) : (
                <span className="rounded bg-primary/15 px-2 py-0.5 text-xs text-primary">altera dados</span>
              )}
              {t.annotations?.destructiveHint && <span className="rounded bg-destructive/15 px-2 py-0.5 text-xs text-destructive">remove dados</span>}
            </div>
            <p className="text-sm text-muted-foreground">{t.title} — {t.description}</p>
            {props.length > 0 ? (
              <table className="w-full text-sm">
                <thead className="text-left text-muted-foreground">
                  <tr><th className="py-1 pr-4">Parâmetro</th><th className="py-1 pr-4">Tipo</th><th className="py-1">Descrição</th></tr>
                </thead>
                <tbody>
                  {props.map(([name, p]) => (
                    <tr key={name} className="border-t border-border align-top">
                      <td className="py-2 pr-4 font-mono">{name}{required.has(name) && <span className="text-primary">*</span>}</td>
                      <td className="py-2 pr-4 font-mono text-xs">{p.enum ? p.enum.join(" | ") : p.type === "array" ? `${p.items?.type}[]` : p.type}</td>
                      <td className="py-2">{p.description}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            ) : (
              <p className="text-sm text-muted-foreground">Sem parâmetros.</p>
            )}
            <div className="grid gap-4 md:grid-cols-2">
              <div className="space-y-1">
                <p className="text-xs font-semibold uppercase text-muted-foreground">Request (tools/call)</p>
                <Code>{pretty({ jsonrpc: "2.0", id: 1, method: "tools/call", params: { name: t.name, arguments: ex?.request ?? {} } })}</Code>
              </div>
              <div className="space-y-1">
                <p className="text-xs font-semibold uppercase text-muted-foreground">Response (structuredContent)</p>
                <Code>{pretty(ex?.response ?? {})}</Code>
              </div>
            </div>
          </section>
        );
      })}

      <section id="webhooks" className="scroll-mt-20 rounded-xl border border-border bg-card p-6 space-y-4">
        <h2 className="text-xl font-semibold">Webhooks: assinatura e reenvio</h2>
        <p className="text-sm text-muted-foreground">
          Cada aviso é um POST JSON com os cabeçalhos <code>X-EventFlow-Signature: t=&lt;timestamp&gt;,v1=&lt;hmac&gt;</code>,{" "}
          <code>X-EventFlow-Delivery</code> e <code>X-EventFlow-Attempt</code>. A assinatura é HMAC-SHA256 de{" "}
          <code>{"`${t}.${corpo}`"}</code> com o <code>secret</code> recebido ao assinar. Responda 2xx para confirmar; qualquer outra resposta
          ou tempo esgotado (8s) agenda um reenvio com espera exponencial (30s, 60s, 120s, 240s, 480s — até 6 tentativas). Todas as tentativas
          ficam registradas e podem ser consultadas com <code>list_webhook_deliveries</code>.
        </p>
        <div className="grid gap-4 md:grid-cols-2">
          <div className="space-y-1"><p className="text-xs font-semibold uppercase text-muted-foreground">Corpo do aviso</p><Code>{pretty(WEBHOOK_BODY_EXAMPLE)}</Code></div>
          <div className="space-y-1"><p className="text-xs font-semibold uppercase text-muted-foreground">Validação (Node.js)</p><Code>{VERIFY_SNIPPET}</Code></div>
        </div>
      </section>
    </div>
  );
}
