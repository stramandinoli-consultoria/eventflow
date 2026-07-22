import { defineTool } from "@lovable.dev/mcp-js";
import { z } from "zod";

// Lista simples de eventos publicados. Enquanto os dados do EventFlow
// permanecerem no navegador (localStorage), esta ferramenta retorna a lista
// curada abaixo. Quando as tabelas de eventos migrarem para o banco,
// substitua por uma consulta ao Supabase usando o token do usuário.
const EVENTS = [
  {
    id: "ev1",
    name: "Festival Sabores da Cidade",
    date: "2026-06-14 17:00",
    location: "Parque Ibirapuera, São Paulo",
    status: "live",
  },
  {
    id: "ev2",
    name: "Brew & BBQ Fest",
    date: "2026-07-22 16:00",
    location: "Centro de Eventos, Curitiba",
    status: "upcoming",
  },
  {
    id: "ev3",
    name: "GastroNight Rooftop",
    date: "2026-08-03 19:00",
    location: "Edifício Copan, São Paulo",
    status: "upcoming",
  },
];

export default defineTool({
  name: "list_events",
  title: "Listar eventos",
  description: "Retorna os eventos gastronômicos disponíveis no EventFlow.",
  inputSchema: {
    status: z
      .enum(["live", "upcoming", "ended"])
      .optional()
      .describe("Filtra por status do evento."),
  },
  annotations: { readOnlyHint: true, idempotentHint: true, openWorldHint: false },
  handler: async ({ status }, ctx) => {
    if (!ctx.isAuthenticated()) {
      return { content: [{ type: "text", text: "Não autenticado." }], isError: true };
    }
    const items = status ? EVENTS.filter((e) => e.status === status) : EVENTS;
    return {
      content: [{ type: "text", text: JSON.stringify(items, null, 2) }],
      structuredContent: { events: items },
    };
  },
});
