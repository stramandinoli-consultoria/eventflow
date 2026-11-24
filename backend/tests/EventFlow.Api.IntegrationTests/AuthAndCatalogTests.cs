using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace EventFlow.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public class AuthAndCatalogTests(ApiFixture api)
{
    [Fact]
    public async Task Login_com_senha_errada_retorna_401_com_mensagem()
    {
        var response = await api.Client().PostAsJsonAsync("/api/auth/login", new { email = "admin@eventflow.app", password = "errada" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("Credenciais inválidas", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Contas_de_exemplo_entram_com_o_papel_certo()
    {
        Assert.Equal("admin", (await api.AdminAsync()).User.Role);
        Assert.Equal("customer", (await api.CustomerAsync()).User.Role);
        var partner = await api.PartnerAsync();
        Assert.Equal("partner", partner.User.Role);
        Assert.Equal("pt1", partner.User.PartnerId);
    }

    [Fact]
    public async Task Cadastro_cria_cliente_e_nao_aceita_email_repetido()
    {
        var body = new { name = "Nova Cliente", email = $"nova{Guid.NewGuid():N}@teste.com", password = "segredo1", phone = "+55 11 90000-0000" };
        var created = await api.Client().PostAsJsonAsync("/api/auth/register", body);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var auth = (await created.Content.ReadFromJsonAsync<AuthResult>(ApiFixture.Json))!;
        Assert.Equal("customer", auth.User.Role);

        var me = await api.Client(auth.AccessToken).GetFromJsonAsync<UserResult>("/api/auth/me", ApiFixture.Json);
        Assert.Equal(body.email, me!.Email);

        var again = await api.Client().PostAsJsonAsync("/api/auth/register", body);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Refresh_token_vale_uma_unica_vez()
    {
        var session = await api.CustomerAsync();

        var first = await api.Client().PostAsJsonAsync("/api/auth/refresh", new { refreshToken = session.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var reused = await api.Client().PostAsJsonAsync("/api/auth/refresh", new { refreshToken = session.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);
    }

    [Fact]
    public async Task Catalogo_publico_traz_os_dados_de_exemplo_no_formato_do_front()
    {
        var client = api.Client();
        var events = await client.GetFromJsonAsync<JsonElement>("/api/events");
        var ev1 = events.EnumerateArray().First(e => e.GetProperty("id").GetString() == "ev1");
        Assert.Equal("2026-06-14 17:00", ev1.GetProperty("date").GetString());
        Assert.Equal("live", ev1.GetProperty("status").GetString());

        // Outros testes podem criar stands no ev1; conferimos que os 8 de exemplo estão lá.
        string[] seeded = ["s1", "s2", "s3", "s4", "s5", "s6", "s7", "s8"];
        var stands = await client.GetFromJsonAsync<JsonElement>("/api/stands?eventId=ev1");
        var standIds = stands.EnumerateArray().Select(s => s.GetProperty("id").GetString()).ToList();
        Assert.All(seeded, id => Assert.Contains(id, standIds));
        var burger = stands.EnumerateArray().Single(s => s.GetProperty("id").GetString() == "s3");
        Assert.Equal((440, 160), (burger.GetProperty("x").GetInt32(), burger.GetProperty("y").GetInt32()));

        var map = await client.GetFromJsonAsync<JsonElement>("/api/events/ev1/map");
        var mapIds = map.GetProperty("map").GetProperty("stands").EnumerateArray().Select(s => s.GetProperty("id").GetString()).ToList();
        Assert.All(seeded, id => Assert.Contains(id, mapIds));
    }

    [Fact]
    public async Task Dados_cadastrais_do_parceiro_so_aparecem_para_admin()
    {
        var anonymous = await api.Client().GetFromJsonAsync<List<PartnerResult>>("/api/partners", ApiFixture.Json);
        Assert.Equal("", anonymous!.Single(p => p.Id == "pt1").Cnpj);

        var admin = await api.AdminAsync();
        var full = await api.Client(admin.AccessToken).GetFromJsonAsync<List<PartnerResult>>("/api/partners", ApiFixture.Json);
        Assert.Equal("12.345.678/0001-90", full!.Single(p => p.Id == "pt1").Cnpj);
    }

    [Fact]
    public async Task So_admin_altera_eventos_e_a_data_e_validada()
    {
        var body = new { name = "Feira Teste", date = "2026-10-10 18:00", location = "São Paulo", description = "", banner = "", status = "upcoming" };
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Client().PutAsJsonAsync("/api/events/ev-teste", body)).StatusCode);

        var customer = await api.CustomerAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await api.Client(customer.AccessToken).PutAsJsonAsync("/api/events/ev-teste", body)).StatusCode);

        var admin = api.Client((await api.AdminAsync()).AccessToken);
        var invalid = await admin.PutAsJsonAsync("/api/events/ev-teste", body with { date = "amanhã" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var created = await admin.PutAsJsonAsync("/api/events/ev-teste", body);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var ev = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.StartsWith("https://", ev.GetProperty("banner").GetString()); // banner padrão quando vazio

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync("/api/events/ev-teste")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/events/ev-teste")).StatusCode);
    }

    [Fact]
    public async Task Admin_cadastra_parceiro_com_login_e_parceiro_so_mexe_no_proprio_cardapio()
    {
        var admin = api.Client((await api.AdminAsync()).AccessToken);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var standId = $"s-{suffix}";
        var email = $"parceiro{suffix}@teste.com";

        var stand = await admin.PutAsJsonAsync($"/api/stands/{standId}", new
        {
            eventId = "ev1", name = "Pastel do Zé", category = "food", description = "", cover = "", rating = 4.5,
            x = 40, y = 40, width = 120, height = 90,
        });
        Assert.Equal(HttpStatusCode.OK, stand.StatusCode);

        var partner = await admin.PutAsJsonAsync($"/api/partners/pt-{suffix}", new
        {
            companyName = "Pastel do Zé LTDA", cnpj = "11.111.111/0001-11", contactName = "Zé", email, phone = "+5511911111111",
            address = "Rua A, 1", category = "food", description = "", logo = "", eventId = "ev1", standId, loginPassword = "senha123",
        });
        Assert.Equal(HttpStatusCode.OK, partner.StatusCode);

        var session = await api.LoginAsync(email, "senha123");
        var zé = api.Client(session.AccessToken);
        var product = new { standId, name = "Pastel de carne", description = "", price = 12.5, prepMinutes = 5, image = "", available = true };
        Assert.Equal(HttpStatusCode.OK, (await zé.PutAsJsonAsync($"/api/products/pr-{suffix}", product)).StatusCode);

        var otherStand = product with { standId = "s1" };
        Assert.Equal(HttpStatusCode.Forbidden, (await zé.PutAsJsonAsync($"/api/products/pr2-{suffix}", otherStand)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await zé.DeleteAsync("/api/products/p1")).StatusCode);
    }
}
