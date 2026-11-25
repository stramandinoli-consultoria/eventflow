# Plano de migração — EventFlow (event-bite-pro) para .NET 10 + PostgreSQL

> **Criado em:** 24/11/2026 às 19:40 (horário de Brasília, UTC−3)
> **Atualizado em:** 24/11/2026 às 19:44: incluída a regra de não mudar o visual (seção 1.1)
> **Executado em:** 24/11/2026 às 20:19 — ver a [seção 9](#9-resultado-da-execução)
> **Situação:** migração concluída na branch `migracao-dotnet` (ainda não enviada ao GitHub).

## 1. Objetivo

1. Criar um **back-end próprio em .NET 10** (Clean Architecture, Clean Code) com **PostgreSQL**.
2. Fazer o **front-end React consumir essa API**, no lugar do `localStorage` e do Supabase.
3. **Tirar o Lovable do projeto** (dependências, plugins, Supabase, Cloudflare) e aproveitar ao máximo o que ele já fez: telas, componentes, mapa, regras e webhooks.

No fim, o projeto roda inteiro na sua máquina ou no seu servidor, sem depender de nenhum serviço do Lovable.

### 1.1 Regra obrigatória: o visual não muda

A migração troca **só a parte de dados**. A aparência do sistema tem que ficar **exatamente igual** à de hoje.

**Não pode mudar:**
- **Cores:** tema, gradientes (`gradient-hero`, `text-gradient`…), cores das categorias do mapa (`standTypeColor`) e o modo escuro.
- **Letras:** fontes, tamanhos, pesos, espaçamentos e todos os textos e rótulos das telas (por exemplo os de `statusLabel` e `standTypeLabel`).
- **Layout:** posição, tamanho e ordem dos elementos, menus, cards, mapa, telas no celular e no computador, ícones, imagens e animações.

**Como garantir:**
- **Arquivos de visual intocados:** `src/styles.css`, `src/components/ui/*`, `Nav.tsx`, `EventMap.tsx` e as classes Tailwind (`className`) das telas.
- **Nas telas (`src/routes/*.tsx`), só a origem dos dados muda:** onde hoje está `useStore(...)` passa a estar um hook da API. O JSX e as classes ficam iguais.
- **Telas novas de "carregando" e de erro:** usam os mesmos componentes e o mesmo estilo que já existem.
- **Mesmas bibliotecas de visual:** Tailwind 4, `tw-animate-css`, `lucide-react`, Radix/shadcn e `sonner` ficam nas mesmas versões, para nada mudar sem querer.
- **Comparação por captura de tela:** **antes** de começar, tiro capturas de todas as telas no computador e no celular. **Depois** da migração, tiro de novo com os mesmos dados e comparo. Qualquer diferença visual é corrigida antes de entregar.

---

## 2. Situação atual (resumo da análise)

| Parte | Onde está hoje | Destino |
| --- | --- | --- |
| Eventos, stands, mapa, produtos, parceiros, usuários | `localStorage` do navegador ([src/lib/store.ts](../src/lib/store.ts)) | Tabelas no PostgreSQL + API .NET |
| Login e cadastro | Lista local, senha em texto puro ([auth.tsx](../src/routes/auth.tsx)) | API .NET com JWT e senha com hash |
| Pedidos e timeline | Supabase (`ef_orders`, `ef_order_timeline`) + Realtime | PostgreSQL + API + **SignalR** |
| Webhooks com HMAC e reenvio | Supabase + rotas TanStack ([src/lib/mcp/notify.ts](../src/lib/mcp/notify.ts)) | Serviço .NET + tarefa em segundo plano |
| Ferramentas MCP (11 tools) | `@lovable.dev/mcp-js` em `/mcp` | SDK oficial MCP em C# (`ModelContextProtocol.AspNetCore`) |
| Servidor web (SSR) | TanStack Start + Nitro + Cloudflare Workers | Front vira SPA estática (Vite) |

**Ponto que facilita muito:** todas as telas leem e gravam dados **somente pelo `useStore`** (`store.ts`). Basta trocar o que está por trás desse arquivo, e as telas mudam pouco.

---

## 3. O que será aproveitado do Lovable

| Item | Como será aproveitado |
| --- | --- |
| Todas as telas (`src/routes/*.tsx`) | Mantidas. Só muda de onde vêm os dados. |
| Componentes shadcn/ui (`src/components/ui`), Tailwind 4, tema e `styles.css` | Mantidos como estão. |
| Mapa interativo com zoom e arraste (`EventMap.tsx`) | Mantido. A regra do `AGENTS.md` continua valendo: coordenadas lógicas fixas, escala só na tela. |
| Modelos de dados (`Event`, `Stand`, `Product`, `Partner`, `Order`, `Timeline`) | Viram as entidades do domínio em C#. |
| Fluxo de status do pedido (`aguardando pagamento → em preparo → pronto → finalizado / cancelado`) | Vira regra no domínio (`Order.Advance()`, `Order.Cancel()`). |
| Dados de exemplo (3 eventos, 8 stands, 8 produtos, 3 usuários) | Viram *seed* do banco. |
| Webhooks: assinatura HMAC-SHA256 `t=…,v1=…`, fila e reenvio 30s→1h, até 6 tentativas | Mesma lógica em C#, com o mesmo formato de assinatura. |
| 11 ferramentas MCP e a página `/docs/mcp` | Reescritas em C# com os mesmos nomes e parâmetros. |
| Esquema SQL das migrations do Supabase/Drizzle | Base para o esquema do EF Core. |

**O que será removido:**
- Pacotes `@lovable.dev/vite-tanstack-config`, `@lovable.dev/mcp-js`, `@supabase/supabase-js`, `@cloudflare/vite-plugin`, `nitro`, `drizzle-*` e `postgres`.
- Pastas `.lovable/`, `supabase/` e `drizzle/`, e os arquivos `wrangler.jsonc`, `bunfig.toml` e `bun.lock`.
- `src/integrations/supabase/`, `src/server.ts`, `src/start.ts` e a rota de consentimento OAuth do Lovable.
- O *workaround* de Windows que coloquei no `vite.config.ts`, que deixa de ser necessário.

---

## 4. Arquitetura do back-end (.NET 10)

### 4.1 Estrutura de pastas

```
event-bite-pro/
├─ backend/
│  ├─ EventFlow.sln
│  ├─ src/
│  │  ├─ EventFlow.Domain/          # Entidades, enums, regras, erros de domínio (sem dependências)
│  │  ├─ EventFlow.Application/     # Casos de uso (Commands/Queries), DTOs, validação, interfaces
│  │  ├─ EventFlow.Infrastructure/  # EF Core + Npgsql, repositórios, JWT, hash, webhooks, e-mail/WhatsApp
│  │  └─ EventFlow.Api/             # Endpoints, SignalR, MCP, Swagger/OpenAPI, autenticação, CORS
│  └─ tests/
│     ├─ EventFlow.Domain.Tests/
│     ├─ EventFlow.Application.Tests/
│     └─ EventFlow.Api.IntegrationTests/  # testes com PostgreSQL real (Testcontainers ou banco local)
├─ frontend/                        # o React atual (movido para cá)
├─ docker-compose.yml               # postgres + api + front
└─ docs/
```

**Regra de dependência:** `Api → Infrastructure → Application → Domain`. O domínio não conhece banco, HTTP nem bibliotecas.

### 4.2 Padrões

- **Clean Architecture + CQRS leve:** cada caso de uso é um *Command* ou uma *Query* com seu *Handler*, por exemplo `CreateOrderCommand` e `GetEventMapQuery`. Vou usar um *mediator* próprio e simples, sem MediatR, que passou a ter licença comercial.
- **Validação:** FluentValidation nos commands.
- **Erros:** padrão `Result<T>` nos casos de uso e respostas `ProblemDetails` (RFC 9457) na API.
- **Endpoints:** Minimal APIs agrupadas por módulo (`/api/events`, `/api/orders`…).
- **Documentação:** OpenAPI nativo do .NET 10 + Scalar (interface tipo Swagger) em `/docs`.
- **Logs:** `ILogger` com saída estruturada, mais uma tabela de **auditoria** para as ações de admin e parceiro.

### 4.3 Banco de dados (PostgreSQL + EF Core 10 com migrations)

| Tabela | Campos principais |
| --- | --- |
| `users` | id, name, email (único), phone, password_hash, role (`customer`/`partner`/`admin`), partner_id |
| `events` | id, name, banner_url, starts_at, location, description, status (`upcoming`/`live`/`ended`), organizer_id |
| `partners` | id, company_name, cnpj, contact_name, email, phone, address, category, description, logo_url, event_id, stand_id |
| `stands` | id, event_id, name, category, description, cover_url, rating, x, y, width, height |
| `products` | id, stand_id, name, description, price, prep_minutes, image_url, available |
| `orders` | id, code (ex.: `ord-1042`), event_id, stand_id, customer_id, customer_name, customer_phone, total, status, created_at, updated_at |
| `order_items` | id, order_id, product_id, product_name, qty, unit_price |
| `order_timeline` | id, order_id, status, note, at |
| `webhooks` | id, owner_id, url, secret, event_id, stand_name, active, last_status, last_sent_at |
| `webhook_deliveries` / `webhook_attempts` | mesma estrutura da migration atual |
| `refresh_tokens` | id, user_id, token_hash, expires_at, revoked_at |
| `audit_logs` | id, user_id, action, entity, entity_id, data, at |

Melhorias em relação ao Supabase:
- Os itens do pedido saem do JSON e viram a tabela `order_items`.
- O **total é calculado no servidor** com o preço do banco, para que o cliente não consiga mandar o próprio preço.
- O mapa continua com coordenadas lógicas (`x`, `y`, `width`, `height`) no stand.

### 4.4 Autenticação e permissões

- Login com e-mail e senha.
- Senha com hash usando `PasswordHasher` do ASP.NET Identity (PBKDF2).
- **JWT** com validade curta, mais *refresh token*.
- Os papéis viram *policies*:

| Papel | Acesso |
| --- | --- |
| `admin` | Tudo |
| `partner` | Só o próprio stand: produtos e pedidos |
| `customer` | Só os próprios pedidos |
| Visitante (sem login) | Vê eventos, mapa e cardápio |

- Isso corrige o problema atual, em que **qualquer pessoa lê e altera todos os pedidos**.
- O login social (Google) fica como etapa opcional, depois do básico funcionando.

### 4.5 Endpoints da API

```
Auth       POST /api/auth/register · /login · /refresh · /logout      GET /api/auth/me
Eventos    GET/POST /api/events      GET/PUT/DELETE /api/events/{id}
Mapa       GET /api/events/{id}/map  PUT /api/events/{id}/map (salva o layout inteiro)
Stands     POST /api/events/{id}/stands   PUT/PATCH(posição)/DELETE /api/stands/{id}
Produtos   GET /api/stands/{id}/products  POST /api/stands/{id}/products  PUT/DELETE /api/products/{id}
Parceiros  GET/POST /api/partners   PUT/DELETE /api/partners/{id}
Pedidos    POST /api/orders   GET /api/orders/{id}   GET /api/orders (filtros: evento, stand, status, meus)
           POST /api/orders/{id}/advance   POST /api/orders/{id}/cancel   GET /api/orders/{id}/timeline
Dashboard  GET /api/admin/dashboard  (vendas, ranking de stands, tempo médio)
Webhooks   GET/POST/DELETE /api/webhooks   GET /api/webhooks/deliveries   POST /api/webhooks/deliveries/retry
Tempo real SignalR /hubs/orders  (grupos: pedido, stand, evento)
MCP        /mcp  (as 11 ferramentas atuais, autenticadas por JWT)
```

### 4.6 Tempo real e tarefas em segundo plano

- **SignalR:** quando um pedido muda de status, a API avisa o cliente daquele pedido, o painel do parceiro e o dashboard do admin. Isso substitui o Realtime do Supabase e a sincronização entre abas.
- **Webhooks:** um `BackgroundService` processa a fila de reenvio a cada 30 s. Hoje isso dependia de alguém chamar `/api/public/webhooks/retry`.
- **WhatsApp e e-mail:** entram como interfaces (`INotificationSender`). A primeira versão só **grava no log** ou usa uma implementação "falsa". A integração real (Z-API, Evolution ou Meta) entra depois, quando houver conta e token.

---

## 5. Mudanças no front-end

1. **Mover para `frontend/`** e tirar o Lovable do `vite.config.ts`, usando `@vitejs/plugin-react`, `@tailwindcss/vite`, `vite-tsconfig-paths` e `@tanstack/router-plugin` diretamente.
2. **De TanStack Start (SSR) para SPA:** as rotas `createFileRoute` continuam iguais, só sai a parte de servidor (`server.ts`, `start.ts`, rotas `/mcp` e `/api/public`). O `__root.tsx` perde o `shellComponent` e ganha um `index.html` + `main.tsx`.
3. **Novo cliente da API** (`src/api/`):
   - `http.ts`: `fetch` com JWT e renovação automática do token.
   - `events.ts`, `orders.ts`…: funções tipadas.
   - Os tipos vêm do **OpenAPI da API**, gerados com `openapi-typescript`, então o front e a API não ficam dessincronizados.
4. **Trocar o `store.ts`:**
   - Os dados passam a vir do **TanStack React Query**, que já está instalado, com hooks como `useEvents()`, `useEventMap(id)` e `useCreateOrder()`.
   - O `zustand` fica só para a sessão do usuário (token e usuário logado).
   - Apaga a sincronização por `BroadcastChannel`/`localStorage` e o `order-sync.ts`.
5. **SignalR no front** (`@microsoft/signalr`): ao receber um aviso, o front atualiza o cache do React Query. A timeline e o painel mudam na hora.
6. **Login:** `auth.tsx` passa a chamar a API, e `login.tsx` (Supabase) é removido.
7. **Ambiente:** `.env` com `VITE_API_URL=http://localhost:5080`. Em desenvolvimento, o Vite encaminha `/api` e `/hubs` para a API (*proxy*), sem problemas de CORS.

---

## 6. Etapas de execução

Cada etapa termina com o projeto compilando e testado antes de seguir.

| # | Etapa | Entrega | Como validar |
| --- | --- | --- | --- |
| 1 | Preparar repositório | Branch `migracao-dotnet`, mover front para `frontend/`, **capturas de tela de referência** de todas as telas (computador e celular) | Front roda igual ao atual |
| 2 | Esqueleto .NET 10 | Solução com 4 projetos + testes, Docker do Postgres | `dotnet build` e `dotnet test` |
| 3 | Domínio + banco | Entidades, regras de status, EF Core, migrations, seed com os dados atuais | Testes de domínio; tabelas criadas no Postgres |
| 4 | Auth | Registro, login, JWT, refresh, papéis | Testes de integração; login dos 3 usuários de exemplo |
| 5 | Eventos, stands, mapa, produtos, parceiros | CRUDs + permissões | Testes + Scalar `/docs` |
| 6 | Pedidos + timeline + SignalR | Criar pedido, avançar, cancelar, aviso em tempo real | Testes; dois navegadores vendo a mesma mudança |
| 7 | Webhooks + reenvio | Fila, HMAC, tarefa em segundo plano | Teste com servidor de webhook local |
| 8 | MCP em C# | As 11 ferramentas | Chamada às tools pelo MCP Inspector |
| 9 | Front ligado à API | Cliente HTTP, React Query, SignalR, login | Todas as telas funcionando; testar fluxo cliente → parceiro → admin; **capturas iguais às de referência** (cores, letras e layout) |
| 10 | Remover Lovable | Apagar pacotes, pastas e arquivos listados na seção 3 | `npm run build` sem nenhum pacote `@lovable`/`supabase` |
| 11 | Docker + documentação | `docker-compose up` sobe Postgres, API e front; `docs/COMO-RODAR.md` | Subir do zero numa pasta limpa |

---

## 7. Decisões que preciso que você confirme

1. **Front como SPA (sem SSR).** Recomendo: é mais simples, roda em qualquer hospedagem estática e a API cuida do resto. A alternativa é manter o TanStack Start com SSR em Node, sem Cloudflare.
2. **MCP:** reescrever as 11 ferramentas em C# (recomendado) ou deixar para depois.
3. **Dados atuais do Supabase:** os pedidos que já estão lá precisam ser importados? Se não, começo só com os dados de exemplo.
4. **WhatsApp:** começar com a implementação "falsa" (só registra) e integrar depois? Se já tiver conta Z-API, Evolution ou Meta, posso integrar direto.
5. **Repositório:** trabalhar numa branch `migracao-dotnet` e só juntar à `main` quando estiver pronto? **Atenção:** quando a `main` mudar, o Lovable deixa de conseguir editar o projeto. É o objetivo, mas é bom saber.
6. **Nome do sistema:** o código usa "EventFlow" e o MCP usa "OrderUp Events". Qual fica?

## 8. Riscos e cuidados

- **Tamanho:** é praticamente um back-end novo inteiro. Faço por etapas, e você pode testar a cada uma.
- **Lovable:** depois da migração, editar o projeto no Lovable vai quebrar o código. A versão publicada em `event-bite-pro.lovable.app` continua no ar com o código antigo até ser desativada por você.
- **Mudanças nas telas:** a troca do `store.ts` mexe nas ~12 telas que usam `useStore`. A mudança fica só na origem dos dados, e o visual é conferido com as capturas de referência (seção 1.1).
- **Senhas:** os usuários de exemplo serão recriados com hash. Senhas em texto puro deixam de existir.

---

## 9. Resultado da execução

**Decisões usadas** (as recomendações da seção 7): front como SPA sem SSR; MCP reescrito em C#; banco começa com os dados de exemplo (pedidos do Supabase não foram importados); WhatsApp só registra no log; trabalho na branch `migracao-dotnet`; nome **EventFlow**.

**O que foi feito**
- `backend/`: solução .NET 10 com `EventFlow.Domain`, `EventFlow.Application`, `EventFlow.Infrastructure`, `EventFlow.Api` e os testes. PostgreSQL com EF Core e migration `InitialCreate`.
- API REST, JWT com refresh token rotativo, permissões por papel, SignalR (`/hubs/orders`), webhooks HMAC com reenvio automático a cada 30 s, MCP com as 11 ferramentas em `/mcp`, documentação em `/scalar`, limite de tentativas no login e auditoria.
- `frontend/`: o React do Lovable sem Lovable, Supabase, Cloudflare, Nitro e Drizzle. O `store.ts` manteve a mesma interface, agora alimentado pela API e pelo SignalR.
- Guia de uso em [COMO-RODAR.md](COMO-RODAR.md).

**Diferenças em relação ao plano**
- No front, em vez de trocar as telas para hooks do React Query, o `useStore` foi mantido com a mesma interface e passou a buscar os dados na API. Assim nenhuma tela precisou mudar o JSX, o que garante a regra da seção 1.1.
- A documentação interativa da API ficou em `/scalar`, porque `/docs` já é usado pela página `/docs/mcp` do site.
- O que mudou na lógica das telas (sem alterar o visual): login, cadastro e pedido agora esperam a resposta do servidor; o cadastro de parceiro cria o login pela API; `/admin` exige login de admin.

**Verificação**
- `dotnet test`: **33 testes passando** (17 de domínio + 16 de integração com PostgreSQL real, incluindo SignalR, webhook com HMAC e MCP).
- Front: build e checagem de tipos sem erros.
- **Comparação visual (seção 1.1):** 24 capturas antes × depois (computador e celular). Home, login, evento, barraca, eventos (admin), editor de mapa e parceiros ficaram **idênticos (0,0% de diferença)**. As telas com pedidos mudaram só nos dados: antes mostravam pedidos do Supabase da nuvem, e na versão antiga a timeline aparecia duplicada.
- Teste de ponta a ponta no navegador: visitante faz pedido → parceiro confirma o pagamento → tela do visitante muda em tempo real → mapa arrastado continua salvo depois de recarregar → visitante não acessa `/admin`.

**Pendências**
- Docker: `Dockerfile`s e `docker-compose.yml` escritos, mas não testados (Docker não instalado nesta máquina).
- WhatsApp real, pagamento PIX e login social continuam para uma próxima fase.
- A versão publicada no Lovable (`event-bite-pro.lovable.app`) continua no ar com o código antigo.
