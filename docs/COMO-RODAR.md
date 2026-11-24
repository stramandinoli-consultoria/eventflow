# Como rodar o EventFlow (API .NET 10 + front-end)

> **Criado em:** 24/11/2026 às 20:19 (horário de Brasília, UTC−3)

## O que você precisa instalado

| Ferramenta | Versão | Para quê |
| --- | --- | --- |
| .NET SDK | 10.0.404 (fixado em `backend/global.json`) | Rodar a API |
| PostgreSQL | 15 ou mais novo | Banco de dados |
| Node.js | 20 ou mais novo | Rodar o front-end |
| Docker (opcional) | — | Subir tudo com um comando |

## Jeito rápido (desenvolvimento)

### 1. Banco de dados
O PostgreSQL precisa estar rodando em `localhost:5432` com usuário `postgres` e senha `postgres`.
Não precisa criar o banco: a API cria o banco `eventflow`, as tabelas e os dados de exemplo sozinha na primeira vez.

> Usa outro usuário ou senha? Altere `ConnectionStrings:Default` em
> `backend/src/EventFlow.Api/appsettings.Development.json`.

### 2. API
```powershell
cd backend
dotnet run --project src/EventFlow.Api --launch-profile http
```
- API em **http://localhost:5080**
- Documentação interativa da API (Scalar): **http://localhost:5080/scalar**
- Saúde: http://localhost:5080/health

### 3. Front-end (em outro terminal)
```powershell
cd frontend
npm install
npm run dev
```
Abra **http://localhost:5174**. O Vite encaminha `/api`, `/hubs` e `/mcp` para a API, então não há configuração extra.

### Contas de exemplo
| Perfil | E-mail | Senha |
| --- | --- | --- |
| Admin | admin@eventflow.app | admin123 |
| Cliente | cliente@demo.com | demo123 |
| Parceiro (Burger Republic) | burger@demo.com | demo123 |

## Jeito com Docker (tudo de uma vez)
```powershell
docker compose up --build
```
- Front: http://localhost:8081
- API: http://localhost:5080 (documentação em /scalar)
- Banco: localhost:5433

> Os arquivos Docker foram escritos, mas **não foram testados nesta máquina** (o Docker não está instalado aqui).

## Testes
```powershell
cd backend
dotnet test          # 17 testes de domínio + 16 testes de integração (usam o banco eventflow_tests)
cd ../frontend
npm run build        # confere os tipos (TypeScript) e gera o build
```
Os testes de integração apagam e recriam o banco `eventflow_tests` a cada execução. Para usar outro servidor,
defina a variável `EVENTFLOW_TEST_DB` (ex.: `Host=localhost;Port=5432;Username=postgres;Password=postgres`).

## Configurações importantes (produção)

Defina por variável de ambiente (o `__` separa as seções):

| Variável | Exemplo | Observação |
| --- | --- | --- |
| `ConnectionStrings__Default` | `Host=...;Database=eventflow;Username=...;Password=...` | Obrigatória |
| `Jwt__Key` | texto aleatório com 32+ caracteres | **Obrigatória e secreta.** Sem ela a API não sobe |
| `Database__MigrateOnStartup` | `true` | Aplica as migrations ao iniciar |
| `Database__Seed` | `false` | Em produção, deixe `false` para não criar os dados de exemplo |
| `Cors__Origins__0` | `https://app.seudominio.com.br` | Só se o front estiver em outro domínio |
| `Docs__Enabled` | `false` | Liga a documentação `/scalar` fora do ambiente de desenvolvimento |

No front, se ele ficar em domínio diferente da API, gere o build com `VITE_API_URL=https://api.seudominio.com.br`.

## Mudando o banco (migrations)
```powershell
cd backend
dotnet tool restore
dotnet ef migrations add NomeDaMudanca --project src/EventFlow.Infrastructure --startup-project src/EventFlow.Api --output-dir Persistence/Migrations
```

## Integração com agentes de IA (MCP)
- Endpoint: `POST http://localhost:5080/mcp` (ou `/mcp` pelo front).
- Autenticação: cabeçalho `Authorization: Bearer <accessToken>` obtido em `POST /api/auth/login`.
- Lista de ferramentas e exemplos: página **/docs/mcp** do site.

## WhatsApp
Por enquanto a mensagem de "pedido pronto" é **só registrada no log da API** (`[WhatsApp] Para ****7777: ...`).
Para enviar de verdade, crie uma implementação de `IWhatsAppSender` (Z-API, Evolution API ou Meta Cloud API) em
`backend/src/EventFlow.Infrastructure/Notifications/` e troque o registro em `DependencyInjection.cs`.

## Problemas comuns
| Sintoma | Causa provável |
| --- | --- |
| Toast "Não foi possível conectar ao servidor" | A API não está rodando na porta 5080 |
| API não sobe: "Jwt:Key precisa ter pelo menos 32 caracteres" | Falta `Jwt__Key` fora do ambiente de desenvolvimento |
| API não sobe: erro de conexão com o banco | PostgreSQL parado ou usuário/senha diferentes |
| `/admin` volta para o login | Só a conta de admin acessa a área administrativa |
