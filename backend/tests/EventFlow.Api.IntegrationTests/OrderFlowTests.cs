using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace EventFlow.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class OrderFlowTests(ApiFixture api)
{
    private static object NewOrder(string standId = "s3", string productId = "p5", int qty = 2) => new
    {
        standId, customerName = "Teste", customerPhone = "+5511900000000",
        items = new[] { new { productId, qty } },
    };

    private async Task<PlacedResult> PlaceAsync(HttpClient? client = null, object? body = null)
    {
        var response = await (client ?? api.Client()).PostAsJsonAsync("/api/orders", body ?? NewOrder());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PlacedResult>(ApiFixture.Json))!;
    }

    [Fact]
    public async Task Pedido_sem_login_so_e_visto_com_a_chave()
    {
        var placed = await PlaceAsync();

        Assert.Matches("^ord-\\d+$", placed.Order.Id);
        Assert.Equal(76m, placed.Order.Total); // 2 × R$ 38 (preço do banco)
        Assert.Equal("awaiting_payment", placed.Order.Status);

        Assert.Equal(HttpStatusCode.NotFound, (await api.Client().GetAsync($"/api/orders/{placed.Order.Id}")).StatusCode);

        var withKey = new HttpRequestMessage(HttpMethod.Get, $"/api/orders/{placed.Order.Id}");
        withKey.Headers.Add("X-Order-Key", placed.AccessKey);
        Assert.Equal(HttpStatusCode.OK, (await api.Client().SendAsync(withKey)).StatusCode);

        var lookup = await api.Client().PostAsJsonAsync("/api/orders/lookup", new
        {
            orders = new[] { new { id = placed.Order.Id, key = placed.AccessKey }, new { id = "ord-1042", key = "chave-errada" } },
        });
        var found = await lookup.Content.ReadFromJsonAsync<List<OrderResult>>(ApiFixture.Json);
        Assert.Equal(placed.Order.Id, Assert.Single(found!).Id);
    }

    [Fact]
    public async Task Pedido_rejeita_produto_de_outra_barraca_e_campos_vazios()
    {
        var wrong = await api.Client().PostAsJsonAsync("/api/orders", NewOrder(productId: "p1"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, wrong.StatusCode);

        var empty = await api.Client().PostAsJsonAsync("/api/orders", new { standId = "s3", customerName = "", customerPhone = "", items = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
    }

    [Fact]
    public async Task Parceiro_avanca_pedidos_do_proprio_stand_e_nao_de_outros()
    {
        var placed = await PlaceAsync();
        var partner = api.Client((await api.PartnerAsync()).AccessToken);

        var advanced = await partner.PostAsync($"/api/orders/{placed.Order.Id}/advance", null);
        Assert.Equal(HttpStatusCode.OK, advanced.StatusCode);
        var order = (await advanced.Content.ReadFromJsonAsync<OrderResult>(ApiFixture.Json))!;
        Assert.Equal("preparing", order.Status);
        Assert.Equal("Pagamento confirmado no caixa", order.Timeline[^1].Note);

        var other = await PlaceAsync(body: NewOrder("s2", "p3", 1));
        Assert.Equal(HttpStatusCode.Forbidden, (await partner.PostAsync($"/api/orders/{other.Order.Id}/advance", null)).StatusCode);

        var customer = api.Client((await api.CustomerAsync()).AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.PostAsync($"/api/orders/{placed.Order.Id}/cancel", null)).StatusCode);
    }

    [Fact]
    public async Task Cliente_logado_ve_so_os_proprios_pedidos()
    {
        var customer = api.Client((await api.CustomerAsync()).AccessToken);
        var placed = await PlaceAsync(customer);
        Assert.Equal("u-cliente", placed.Order.CustomerId);

        var page = await customer.GetFromJsonAsync<JsonElement>("/api/orders?limit=200", ApiFixture.Json);
        var ids = page.GetProperty("items").EnumerateArray().Select(o => o.GetProperty("customerId").GetString()).Distinct();
        Assert.Equal(["u-cliente"], ids);
    }

    [Fact]
    public async Task Mudanca_de_status_chega_em_tempo_real_pelo_SignalR()
    {
        var placed = await PlaceAsync();
        var received = new TaskCompletionSource<OrderResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var hub = new HubConnectionBuilder()
            .WithUrl(new Uri(api.Factory.Server.BaseAddress, "/hubs/orders"), o =>
            {
                o.HttpMessageHandlerFactory = _ => api.Factory.Server.CreateHandler();
                o.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
            })
            .AddJsonProtocol(o => o.PayloadSerializerOptions = ApiFixture.Json)
            .Build();
        hub.On<OrderResult>("orderChanged", o => { if (o.Id == placed.Order.Id) received.TrySetResult(o); });
        await hub.StartAsync();
        Assert.True(await hub.InvokeAsync<bool>("JoinOrder", placed.Order.Id, placed.AccessKey));
        Assert.False(await hub.InvokeAsync<bool>("JoinOrder", "ord-1042", "chave-errada"));

        var partner = api.Client((await api.PartnerAsync()).AccessToken);
        (await partner.PostAsync($"/api/orders/{placed.Order.Id}/advance", null)).EnsureSuccessStatusCode();

        var update = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("preparing", update.Status);
    }

    [Fact]
    public async Task Webhook_recebe_aviso_assinado_com_HMAC()
    {
        using var receiver = new WebhookReceiver();
        var admin = api.Client((await api.AdminAsync()).AccessToken);
        var subscribe = await admin.PostAsJsonAsync("/api/webhooks", new { url = receiver.Url, eventId = "ev1", standName = "burger republic" });
        Assert.Equal(HttpStatusCode.Created, subscribe.StatusCode);
        var secret = (await subscribe.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("secret").GetString()!;

        var placed = await PlaceAsync();
        var change = await admin.PatchAsJsonAsync($"/api/orders/{placed.Order.Id}/status", new { status = "ready", note = (string?)null });
        Assert.Equal(HttpStatusCode.OK, change.StatusCode);
        var result = await change.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(result.GetProperty("webhooks").GetProperty("delivered").GetInt32() >= 1);

        var (signature, body) = await receiver.Received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var parts = signature.Split(',').Select(p => p.Split('=', 2)).ToDictionary(p => p[0], p => p[1]);
        var expected = Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{parts["t"]}.{body}")));
        Assert.Equal(expected, parts["v1"]);

        var payload = JsonDocument.Parse(body).RootElement;
        Assert.Equal("order.status_changed", payload.GetProperty("type").GetString());
        Assert.Equal("ready", payload.GetProperty("order").GetProperty("status").GetString());
        Assert.Equal("awaiting_payment", payload.GetProperty("previous_status").GetString());
    }

    [Fact]
    public async Task Cliente_nao_pode_cadastrar_webhook()
    {
        var customer = api.Client((await api.CustomerAsync()).AccessToken);
        var response = await customer.PostAsJsonAsync("/api/webhooks", new { url = "https://exemplo.com/hook" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Mcp_exige_login_e_lista_as_11_ferramentas()
    {
        var rpc = new { jsonrpc = "2.0", id = 1, method = "tools/list", @params = new { } };

        var anonymous = await api.Client().SendAsync(McpRequest(rpc));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var admin = api.Client((await api.AdminAsync()).AccessToken);
        var response = await admin.SendAsync(McpRequest(rpc));
        var text = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        foreach (var tool in new[] { "whoami", "list_events", "list_orders", "get_order", "update_order_status",
                     "bulk_update_order_status", "subscribe_order_updates", "list_order_subscriptions",
                     "unsubscribe_order_updates", "list_webhook_deliveries", "retry_webhook_deliveries" })
            Assert.Contains($"\"{tool}\"", text);

        var call = await admin.SendAsync(McpRequest(new
        {
            jsonrpc = "2.0", id = 2, method = "tools/call",
            @params = new { name = "get_order", arguments = new { order_id = "ord-1042" } },
        }));
        Assert.Contains("Marina Costa", await call.Content.ReadAsStringAsync());
    }

    private static HttpRequestMessage McpRequest(object rpc)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp") { Content = JsonContent.Create(rpc) };
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        return request;
    }
}

/// <summary>Servidor HTTP local que recebe o POST do webhook.</summary>
internal sealed class WebhookReceiver : IDisposable
{
    private readonly HttpListener _listener = new();
    public string Url { get; }
    public TaskCompletionSource<(string Signature, string Body)> Received { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public WebhookReceiver()
    {
        var port = Random.Shared.Next(20000, 40000);
        Url = $"http://localhost:{port}/hook/";
        _listener.Prefixes.Add(Url);
        _listener.Start();
        _ = Task.Run(async () =>
        {
            var ctx = await _listener.GetContextAsync();
            using var reader = new StreamReader(ctx.Request.InputStream);
            var body = await reader.ReadToEndAsync();
            Received.TrySetResult((ctx.Request.Headers["X-EventFlow-Signature"] ?? "", body));
            ctx.Response.StatusCode = 200;
            ctx.Response.Close();
        });
    }

    public void Dispose() => _listener.Close();
}
