using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using EventFlow.Api.Infrastructure;
using EventFlow.Application.Abstractions;
using EventFlow.Application.Common;
using EventFlow.Application.Features.Events;
using EventFlow.Application.Features.Orders;
using EventFlow.Application.Features.Webhooks;
using EventFlow.Domain;
using EventFlow.Infrastructure.Webhooks;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace EventFlow.Api.Mcp;

/// <summary>
/// Ferramentas MCP do EventFlow (mesmos nomes e parâmetros da versão Lovable).
/// O endpoint /mcp exige o mesmo JWT da API (Authorization: Bearer ...).
/// </summary>
[McpServerToolType]
public sealed class EventFlowTools
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    [McpServerTool(Name = "whoami", Title = "Quem sou eu", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Retorna o usuário autenticado no EventFlow (id e e-mail), útil para verificar a conexão MCP.")]
    public static CallToolResult WhoAmI(ICurrentUser user) =>
        Ok(new { userId = user.Id, email = user.Email, role = user.Role, clientId = (string?)null });

    [McpServerTool(Name = "list_events", Title = "Listar eventos", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Retorna os eventos gastronômicos disponíveis no EventFlow.")]
    public static async Task<CallToolResult> ListEvents(IDispatcher dispatcher,
        [Description("Filtra por status do evento: live, upcoming ou ended.")] string? status = null,
        CancellationToken ct = default)
    {
        if (!EnumCodes.TryParse<EventStatus>(status, out var parsed)) return Fail("status inválido.");
        var result = await dispatcher.Send(new ListEventsQuery(parsed), ct);
        return result.IsSuccess ? Ok(new { events = result.Value }) : Fail(result.Error!);
    }

    [McpServerTool(Name = "list_orders", Title = "Listar pedidos", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Lista os pedidos do EventFlow com paginação e ordenação por data de criação, filtrando por evento, empresa parceira (barraca) e status. Inclui opcionalmente a timeline de cada pedido.")]
    public static async Task<CallToolResult> ListOrders(IDispatcher dispatcher,
        [Description("ID do evento, ex.: ev1.")] string? event_id = null,
        [Description("Nome da empresa parceira / barraca (busca parcial).")] string? stand_name = null,
        [Description("Filtra por status do pedido: awaiting_payment, preparing, ready, completed ou cancelled.")] string? status = null,
        [Description("Inclui a timeline de cada pedido (padrão: true).")] bool include_timeline = true,
        [Description("Pedidos por página, de 1 a 100 (padrão: 25).")] int limit = 25,
        [Description("Número da página, começando em 1 (padrão: 1).")] int page = 1,
        [Description("Ordenação por data de criação: created_at_desc (padrão) ou created_at_asc.")] string sort = "created_at_desc",
        CancellationToken ct = default)
    {
        if (!EnumCodes.TryParse<OrderStatus>(status, out var parsed)) return Fail("status inválido.");
        if (limit is < 1 or > 100) return Fail("limit deve estar entre 1 e 100.");
        var ascending = sort == "created_at_asc";
        var result = await dispatcher.Send(new ListOrdersQuery(event_id, null, stand_name, parsed, page, limit, ascending), ct);
        if (!result.IsSuccess) return Fail(result.Error!);

        var paged = result.Value;
        return Ok(new
        {
            orders = paged.Items.Select(o => include_timeline ? o : o with { Timeline = [] }),
            pagination = new
            {
                page = paged.Page, limit = paged.Limit, total = paged.Total, total_pages = paged.TotalPages,
                has_next = paged.HasNext, sort = ascending ? "created_at_asc" : "created_at_desc",
            },
        });
    }

    [McpServerTool(Name = "get_order", Title = "Detalhar pedido", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Retorna um pedido do EventFlow com itens, status atual e a timeline completa.")]
    public static async Task<CallToolResult> GetOrder(IDispatcher dispatcher,
        [Description("ID do pedido, ex.: ord-1042.")] string order_id, CancellationToken ct = default)
    {
        var result = await dispatcher.Send(new GetOrderQuery(order_id.Trim(), null), ct);
        return result.IsSuccess ? Ok(new { order = result.Value }) : Fail($"Pedido {order_id} não encontrado.");
    }

    [McpServerTool(Name = "update_order_status", Title = "Atualizar status do pedido", ReadOnly = false, Destructive = false, OpenWorld = true)]
    [Description("Atualiza o status de um pedido (ex.: marcar como pronto), registra automaticamente a timeline e dispara os webhooks cadastrados.")]
    public static async Task<CallToolResult> UpdateOrderStatus(IDispatcher dispatcher,
        [Description("ID do pedido, ex.: ord-1042.")] string order_id,
        [Description("Novo status: awaiting_payment, preparing, ready, completed ou cancelled.")] string status,
        [Description("Observação registrada na timeline (até 280 caracteres).")] string? note = null,
        CancellationToken ct = default)
    {
        if (!EnumCodes.TryParse<OrderStatus>(status, out var parsed) || parsed is null) return Fail("status inválido.");
        var result = await dispatcher.Send(new ChangeOrderStatusCommand(order_id.Trim(), parsed.Value, note), ct);
        if (!result.IsSuccess) return Fail(result.Error!);
        var r = result.Value;
        return Ok(new { order = r.Order, previous_status = r.PreviousStatus, timeline_entry = r.TimelineEntry, webhooks = r.Webhooks });
    }

    [McpServerTool(Name = "bulk_update_order_status", Title = "Atualizar status em lote", ReadOnly = false, Destructive = false, OpenWorld = true)]
    [Description("Atualiza o status de vários pedidos de uma vez, registra a timeline de cada um e dispara os webhooks. Retorna o resultado individual de cada pedido.")]
    public static async Task<CallToolResult> BulkUpdateOrderStatus(IDispatcher dispatcher,
        [Description("IDs dos pedidos, ex.: [\"ord-1042\", \"ord-1043\"] (até 100).")] string[] order_ids,
        [Description("Novo status aplicado a todos os pedidos.")] string status,
        [Description("Observação registrada na timeline de cada pedido.")] string? note = null,
        CancellationToken ct = default)
    {
        if (!EnumCodes.TryParse<OrderStatus>(status, out var parsed) || parsed is null) return Fail("status inválido.");
        var result = await dispatcher.Send(new BulkChangeOrderStatusCommand(order_ids, parsed.Value, note), ct);
        return result.IsSuccess ? Ok(result.Value) : Fail(result.Error!);
    }

    [McpServerTool(Name = "subscribe_order_updates", Title = "Assinar avisos de pedidos", ReadOnly = false, OpenWorld = true)]
    [Description("Cadastra um webhook que recebe um POST em JSON assinado com HMAC-SHA256 sempre que o status de um pedido mudar, com reenvio automático em caso de falha. Pode filtrar por evento e/ou empresa parceira.")]
    public static async Task<CallToolResult> SubscribeOrderUpdates(IDispatcher dispatcher,
        [Description("URL https que receberá o POST de notificação.")] string url,
        [Description("Notificar apenas pedidos deste evento.")] string? event_id = null,
        [Description("Notificar apenas pedidos desta empresa parceira.")] string? stand_name = null,
        CancellationToken ct = default)
    {
        var result = await dispatcher.Send(new SubscribeWebhookCommand(url, event_id, stand_name), ct);
        if (!result.IsSuccess) return Fail(result.Error!);
        return Ok(new
        {
            webhook = result.Value.Webhook,
            secret = result.Value.Secret,
            signature = new
            {
                header = WebhookDispatcher.SignatureHeader,
                format = "t=<unix_timestamp>,v1=<hex_hmac_sha256>",
                signed_content = "`${t}.${raw_body}`",
                algorithm = "HMAC-SHA256 com o campo `secret` (guarde-o: ele só é exibido agora)",
            },
            retry_policy = "Até 6 tentativas com espera exponencial (30s, 60s, 120s, 240s, 480s).",
        });
    }

    [McpServerTool(Name = "list_order_subscriptions", Title = "Listar avisos cadastrados", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Lista os webhooks de notificação de pedidos cadastrados pelo usuário conectado.")]
    public static async Task<CallToolResult> ListOrderSubscriptions(IDispatcher dispatcher, CancellationToken ct = default)
    {
        var result = await dispatcher.Send(new ListWebhooksQuery(), ct);
        return result.IsSuccess ? Ok(new { webhooks = result.Value }) : Fail(result.Error!);
    }

    [McpServerTool(Name = "unsubscribe_order_updates", Title = "Remover aviso de pedidos", ReadOnly = false, Destructive = true, OpenWorld = false)]
    [Description("Remove um webhook de notificação de pedidos cadastrado pelo usuário conectado.")]
    public static async Task<CallToolResult> UnsubscribeOrderUpdates(IDispatcher dispatcher,
        [Description("ID (uuid) do webhook a remover.")] Guid webhook_id, CancellationToken ct = default)
    {
        var result = await dispatcher.Send(new UnsubscribeWebhookCommand(webhook_id), ct);
        return result.IsSuccess ? Ok(new { removed = webhook_id }) : Fail(result.Error!);
    }

    [McpServerTool(Name = "list_webhook_deliveries", Title = "Auditar entregas de webhooks", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Lista as entregas de webhooks do usuário conectado com status, número de tentativas, próximo reenvio e o histórico de cada tentativa.")]
    public static async Task<CallToolResult> ListWebhookDeliveries(IDispatcher dispatcher,
        [Description("Filtra por status da entrega: pending, delivered ou failed.")] string? status = null,
        [Description("Filtra por webhook (uuid).")] Guid? webhook_id = null,
        [Description("Filtra por pedido.")] string? order_id = null,
        [Description("Máximo de entregas, de 1 a 100 (padrão: 25).")] int limit = 25,
        [Description("Quantas entregas pular (padrão: 0).")] int offset = 0,
        CancellationToken ct = default)
    {
        if (!EnumCodes.TryParse<DeliveryStatus>(status, out var parsed)) return Fail("status inválido.");
        var result = await dispatcher.Send(new ListDeliveriesQuery(parsed, webhook_id, order_id, limit, offset), ct);
        if (!result.IsSuccess) return Fail(result.Error!);
        var page = result.Value;
        return Ok(new
        {
            deliveries = page.Deliveries,
            pagination = new { total = page.Total, limit = page.Limit, offset = page.Offset, has_more = page.HasMore },
        });
    }

    [McpServerTool(Name = "retry_webhook_deliveries", Title = "Reenviar webhooks", ReadOnly = false, Destructive = false, OpenWorld = true)]
    [Description("Reenvia agora as entregas pendentes que já passaram do horário de reenvio, ou força o reenvio de uma entrega específica (inclusive falhas definitivas).")]
    public static async Task<CallToolResult> RetryWebhookDeliveries(IDispatcher dispatcher,
        [Description("Entrega específica (uuid) a reenviar agora.")] Guid? delivery_id = null,
        CancellationToken ct = default)
    {
        var result = await dispatcher.Send(new RetryDeliveriesCommand(delivery_id), ct);
        return result.IsSuccess ? Ok(new { processed = result.Value.Count, results = result.Value }) : Fail(result.Error!);
    }

    private static CallToolResult Ok(object payload)
    {
        var element = JsonSerializer.SerializeToElement(payload, Json);
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = element.GetRawText() }],
            StructuredContent = element,
        };
    }

    private static CallToolResult Fail(Error error) => Fail(error.Message);

    private static CallToolResult Fail(string message) => new()
    {
        Content = [new TextContentBlock { Text = message }],
        IsError = true,
    };
}
