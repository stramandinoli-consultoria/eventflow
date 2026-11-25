# EventFlow — Gestão de Eventos Gastronômicos

Plataforma para festivais, feiras e eventos de comida e bebida. O organizador monta o **mapa interativo** do evento, as empresas vendem pelos seus stands e o visitante faz o pedido pelo celular e acompanha cada etapa **em tempo real**, até a retirada.

---

## O que o sistema faz

### Visitante (cliente)
- Vê os eventos (ao vivo, próximos e encerrados).
- Navega pelo **mapa do evento** com zoom e arraste; ao tocar numa barraca, abre o cardápio com fotos e preços.
- Faz o pedido sem precisar de conta (ou logado, para ver o histórico em **Minha conta**).
- Acompanha a **timeline do pedido**, que muda sozinha quando o stand atualiza o status.

### Empresa parceira (stand)
- Painel com os pedidos do seu stand em tempo real.
- Avança o status do pedido ou cancela.
- Cadastra e edita os produtos do cardápio.

### Administrador (organizador)
- Cria, edita e remove eventos.
- **Editor de mapa drag and drop**: posiciona barracas, palco, banheiros, entrada, caixa e área VIP.
- Cadastra parceiros (o cadastro já cria o login da empresa) e vincula cada um a um stand.
- Dashboard com receita, número de pedidos, ranking de stands e tempo médio de preparo.

### Ciclo do pedido
```
Aguardando pagamento → Em preparo → Pronto para retirada → Finalizado
                    ↘               ↘                    ↘ Cancelado
```
Cada mudança gera um registro na timeline, avisa o visitante por **SignalR**, dispara os **webhooks** cadastrados e, quando o pedido fica pronto, envia a mensagem de **WhatsApp** (por enquanto só registrada no log; veja [Pendências](#situação-e-pendências)).

O total do pedido é sempre calculado no servidor a partir dos preços do cardápio; o valor enviado pelo navegador é ignorado.

---

## Tecnologias

| Camada | Tecnologias |
| --- | --- |
| Back-end | .NET 10, ASP.NET Core Minimal APIs, EF Core, PostgreSQL 17 |
| Tempo real | SignalR (`/hubs/orders`) |
| Segurança | JWT com refresh token rotativo, senhas com hash, permissão por papel, limite de tentativas no login, auditoria |
| Integrações | Webhooks assinados com HMAC-SHA256 (reenvio automático), servidor **MCP** para agentes de IA (`/mcp`) |
| Documentação da API | OpenAPI + Scalar (`/scalar`) |
| Front-end | React 19, Vite 7, TanStack Router, Tailwind CSS 4, shadcn/ui (Radix), Zustand, Recharts |
| Testes | xUnit — testes de domínio e testes de integração com PostgreSQL real |
| Infra | Docker e Docker Compose (front servido por nginx) |

---

## Arquitetura

O back-end segue **Clean Architecture** com **CQRS** (comandos e consultas enviados por um dispatcher):

```
EventFlow.Api  →  EventFlow.Infrastructure  →  EventFlow.Application  →  EventFlow.Domain
(endpoints,       (EF Core, PostgreSQL,        (casos de uso,             (entidades e regras,
 SignalR, MCP)     JWT, webhooks, WhatsApp)     DTOs, permissões)          sem dependências externas)
```

As regras de negócio ficam no domínio: por exemplo, `Order.Advance()` e `Order.Cancel()` decidem quais mudanças de status são válidas.

No front-end, todas as telas leem e gravam dados só pelo `useStore` ([frontend/src/lib/store.ts](frontend/src/lib/store.ts)), que conversa com a API e com o SignalR.

As posições do mapa são salvas num sistema de coordenadas lógico fixo; só a tela é redimensionada. Assim o mesmo layout aparece igual no computador e no celular.

Regras de arquitetura do projeto: [AGENTS.md](AGENTS.md).

---

## Estrutura de pastas

```
event-bite-pro/
├── backend/
│   ├── src/
│   │   ├── EventFlow.Domain/          # Entidades: Event, Stand, Product, Partner, Order, User, Webhook…
│   │   ├── EventFlow.Application/     # Features (Auth, Events, Stands, Products, Partners, Orders, Dashboard, Webhooks)
│   │   ├── EventFlow.Infrastructure/  # Banco, migrations, seed, segurança, webhooks, WhatsApp
│   │   └── EventFlow.Api/             # Endpoints REST, SignalR, MCP, Program.cs
│   ├── tests/
│   │   ├── EventFlow.Domain.Tests/
│   │   └── EventFlow.Api.IntegrationTests/
│   └── Dockerfile
├── frontend/
│   ├── src/
│   │   ├── routes/                    # Telas (home, evento, stand, pedido, me, partner, admin, docs/mcp)
│   │   ├── components/                # EventMap (mapa interativo), Nav, componentes de UI
│   │   └── lib/                       # store.ts (dados), api.ts (cliente HTTP)
│   ├── nginx.conf
│   └── Dockerfile
├── docs/                              # Guia de execução, plano de migração e especificação original
└── docker-compose.yml
```

---

## Como rodar

Guia completo, com configurações de produção e solução de problemas: [docs/COMO-RODAR.md](docs/COMO-RODAR.md).

**Pré-requisitos:** .NET SDK 10.0.404, Node.js 20+, PostgreSQL 15+ (ou só o Docker).

```sh
# 1) API — precisa do PostgreSQL em localhost:5432 (usuário/senha postgres).
#    O banco "eventflow", as tabelas e os dados de exemplo são criados sozinhos.
cd backend
dotnet run --project src/EventFlow.Api --launch-profile http

# 2) Front-end (em outro terminal)
cd frontend
npm install
npm run dev
```

| O quê | Endereço |
| --- | --- |
| Site | http://localhost:5174 |
| API | http://localhost:5080 |
| Documentação da API | http://localhost:5080/scalar |
| Saúde da API | http://localhost:5080/health |

**Com Docker:** `docker compose up --build` → site em http://localhost:8081, API em http://localhost:5080, banco em localhost:5433.

### Contas de exemplo

| Perfil | E-mail | Senha |
| --- | --- | --- |
| Admin | admin@eventflow.app | admin123 |
| Cliente | cliente@demo.com | demo123 |
| Parceiro (Burger Republic) | burger@demo.com | demo123 |

### Testes

```sh
cd backend
dotnet test          # 17 testes de domínio + 16 de integração (usam o banco eventflow_tests)

cd ../frontend
npm run build        # checa os tipos e gera o build
```

---

## API

Todas as rotas ficam em `/api`. A lista completa, com exemplos, está em `/scalar`.

| Grupo | Rotas principais |
| --- | --- |
| Autenticação | `POST /api/auth/register` · `login` · `refresh` · `logout` · `GET /api/auth/me` |
| Eventos | `GET /api/events` · `GET /api/events/{id}` · `PUT /api/events/{id}` · `DELETE /api/events/{id}` |
| Mapa | `GET /api/events/{id}/map` · `PUT /api/events/{id}/map` |
| Stands | `GET /api/stands` · `PUT /api/stands/{id}` · `PATCH /api/stands/{id}/position` · `GET /api/stands/{id}/products` |
| Produtos | `GET /api/products` · `PUT /api/products/{id}` · `DELETE /api/products/{id}` |
| Parceiros | `GET /api/partners` · `PUT /api/partners/{id}` · `PUT /api/partners/{id}/stand` |
| Pedidos | `POST /api/orders` · `GET /api/orders` · `GET /api/orders/{id}` · `POST /api/orders/lookup` |
| Status do pedido | `POST /api/orders/{id}/advance` · `POST /api/orders/{id}/cancel` · `PATCH /api/orders/{id}/status` · `PATCH /api/orders/status` (em lote) |
| Timeline | `GET /api/orders/{id}/timeline` |
| Dashboard | `GET /api/admin/dashboard` |
| Webhooks | `GET/POST /api/webhooks` · `DELETE /api/webhooks/{id}` · `GET /api/webhooks/deliveries` · `POST /api/webhooks/deliveries/retry` |
| Tempo real | SignalR em `/hubs/orders` |

### Integração com agentes de IA (MCP)

`POST /mcp` com `Authorization: Bearer <accessToken>`. Ferramentas disponíveis: `whoami`, `list_events`, `list_orders`, `get_order`, `update_order_status`, `bulk_update_order_status`, `subscribe_order_updates`, `list_order_subscriptions`, `unsubscribe_order_updates`, `list_webhook_deliveries` e outras. Documentação e exemplos na página **/docs/mcp** do site.

---

## Situação e pendências

**Pronto:** eventos, mapa interativo e editor de mapa, stands, produtos, parceiros, pedidos com timeline em tempo real, dashboard, webhooks, MCP, autenticação com JWT e testes automatizados.

**Pendente:**
- **WhatsApp real:** hoje a mensagem só aparece no log da API. Para enviar de verdade, implemente `IWhatsAppSender` (Z-API, Evolution API ou Meta Cloud API) em `backend/src/EventFlow.Infrastructure/Notifications/`.
- **Pagamento** (PIX e cartão) e **login social**.
- **Docker:** os arquivos existem, mas ainda não foram testados.
- **Próximas fases da especificação:** QR Code de retirada, controle de estoque, gamificação, heatmap e previsão de filas com IA.

---

## Documentação

| Documento | Conteúdo |
| --- | --- |
| [docs/COMO-RODAR.md](docs/COMO-RODAR.md) | Instalação, configuração, produção, migrations e problemas comuns |
