import { auth, defineMcp } from "@lovable.dev/mcp-js";

import whoamiTool from "./tools/whoami";
import listEventsTool from "./tools/list-events";
import listOrdersTool from "./tools/list-orders";
import getOrderTool from "./tools/get-order";
import updateOrderStatusTool from "./tools/update-order-status";
import {
  subscribeOrderUpdates,
  listOrderSubscriptions,
  unsubscribeOrderUpdates,
} from "./tools/manage-webhooks";

// A URL do Supabase é reescrita para o proxy .lovable.cloud na publicação,
// e mcp-js rejeitaria essa URL como issuer (RFC 8414). O project ref é o
// único valor que sobrevive à publicação sem reescrita.
const projectRef = import.meta.env.VITE_SUPABASE_PROJECT_ID ?? "project-ref-unset";

export default defineMcp({
  name: "orderup-events",
  title: "OrderUp Events",
  version: "0.3.0",
  instructions:
    "Ferramentas do EventFlow para clientes MCP autenticados. Use `list_orders` e `get_order` para consultar pedidos por evento/empresa com timeline, `update_order_status` para mudar o status (a timeline é registrada automaticamente) e `subscribe_order_updates` para receber webhooks em tempo real.",
  auth: auth.oauth.issuer({
    issuer: `https://${projectRef}.supabase.co/auth/v1`,
    acceptedAudiences: "authenticated",
  }),
  tools: [
    whoamiTool,
    listEventsTool,
    listOrdersTool,
    getOrderTool,
    updateOrderStatusTool,
    subscribeOrderUpdates,
    listOrderSubscriptions,
    unsubscribeOrderUpdates,
  ],
});
