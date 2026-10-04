import { auth, defineMcp } from "@lovable.dev/mcp-js";

import whoamiTool from "./tools/whoami";
import listEventsTool from "./tools/list-events";
import listOrdersTool from "./tools/list-orders";
import getOrderTool from "./tools/get-order";
import updateOrderStatusTool from "./tools/update-order-status";
import bulkUpdateOrderStatusTool from "./tools/bulk-update-order-status";
import {
  subscribeOrderUpdates,
  listOrderSubscriptions,
  unsubscribeOrderUpdates,
} from "./tools/manage-webhooks";
import { listWebhookDeliveries, retryWebhookDeliveries } from "./tools/webhook-deliveries";

// A URL do Supabase é reescrita para o proxy .lovable.cloud na publicação,
// e mcp-js rejeitaria essa URL como issuer (RFC 8414). O project ref é o
// único valor que sobrevive à publicação sem reescrita.
const projectRef = import.meta.env.VITE_SUPABASE_PROJECT_ID ?? "project-ref-unset";

export default defineMcp({
  name: "orderup-events",
  title: "OrderUp Events",
  version: "0.4.0",
  instructions:
    "Ferramentas do OrderUp Events. Use `list_orders` (paginado, ordenado por data) e `get_order` para consultar pedidos com timeline; `update_order_status` ou `bulk_update_order_status` para mudar status (a timeline é registrada automaticamente); `subscribe_order_updates` para receber webhooks assinados com HMAC; `list_webhook_deliveries` e `retry_webhook_deliveries` para auditar e reenviar entregas. Documentação completa em /docs/mcp.",
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
    bulkUpdateOrderStatusTool,
    subscribeOrderUpdates,
    listOrderSubscriptions,
    unsubscribeOrderUpdates,
    listWebhookDeliveries,
    retryWebhookDeliveries,
  ],
});
