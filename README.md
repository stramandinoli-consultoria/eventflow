# OrderUp Events

Preciso que crie um Sistema de Gestão de Eventos Gastronômicos e Bebidas, completo com todas essas informações a baixo, preciso que seja feito inteiro sem me perguntar qualquer coisa, pode fazer do inicio ao fim, com a tecnologia que quiser, só me entregue ele pronto.

## Visão Geral

Criar uma plataforma completa para gerenciamento de eventos gastronômicos, festivais, feiras e eventos de bebidas, onde:

* Organizadores criam eventos personalizados

* Empresas/stands podem vender comidas e bebidas

* Clientes podem realizar pedidos pelo sistema

* Empresas atualizam status do pedido em tempo real

* Clientes recebem notificações no sistema e WhatsApp

* Cada evento possui um mapa interativo customizável

* Existe uma timeline completa do pedido

* Sistema multi-evento e multi-empresa

---

# Objetivo do Projeto

Desenvolver um sistema web e mobile responsivo para gerenciamento de eventos gastronômicos e festivais, permitindo:

* Cadastro e gerenciamento de eventos

* Cadastro de empresas expositoras

* Criação dinâmica de mapas dos eventos

* Compra de produtos pelo cliente

* Gestão de filas e pedidos

* Notificações em tempo real

* Dashboard administrativo

* Integração com WhatsApp

* Timeline do pedido

* Gestão de barracas/stands

* Sistema escalável multi-tenant

---

# Nome Sugestão do Projeto

* EventFood

* FoodMap Events

* FestivalHub

* TasteMap

* EventFlow

* GastroPlace

* FestFood

* EventBite

---

# Estrutura do Sistema

# 1. Área Administrativa

## Funcionalidades

### Gestão de Eventos

* Criar evento

* Editar evento

* Upload de banner

* Data e horário

* Localização

* Limite de empresas

* Status do evento

* Configuração de mapa

* Configuração de setores

### Gestão de Empresas

* Cadastro de empresa

* Categoria:

  * Comida

  * Bebida

  * Chopp

  * Drinks

  * Sobremesas

  * Internacional

  * Food Truck

### Gestão de Usuários

* Clientes

* Organizadores

* Empresas

* Administradores

### Gestão Financeira

* Taxas

* Comissão

* Relatórios

* Vendas por empresa

* Ranking de vendas

---

# 2. Área das Empresas/Stands

## Funcionalidades

### Painel da Empresa

* Dashboard de vendas

* Pedidos em tempo real

* Tempo médio de preparo

* Controle de fila

### Gestão de Produtos

* Nome

* Descrição

* Categoria

* Fotos

* Preço

* Tempo de preparo

* Disponibilidade

### Gestão dos Pedidos

Status possíveis:

* Recebido

* Em preparo

* Pronto para retirada

* Finalizado

* Cancelado

### Timeline do Pedido

Cada alteração gera:

* Evento na timeline

* Notificação

* Histórico

Exemplo:

```json

[

  {

    "status": "Pedido Recebido",

    "data": "2026-05-25 18:30"

  },

  {

    "status": "Em Preparo",

    "data": "2026-05-25 18:35"

  },

  {

    "status": "Pronto para Retirada",

    "data": "2026-05-25 18:50"

  }

]

```

---

# 3. Área do Cliente

## Funcionalidades

### Cliente poderá:

* Criar conta

* Login social

* Visualizar eventos

* Visualizar mapa do evento

* Encontrar barracas

* Comprar produtos

* Acompanhar pedidos

* Receber notificações

### Timeline do Cliente

* Pedido recebido

* Pedido em preparo

* Pedido pronto

* Histórico completo

### Notificações

* Push Notification

* SignalR em tempo real

* WhatsApp

* E-mail

---

# 4. Sistema de Mapa Dinâmico do Evento

## Objetivo

Criar um construtor simples e visual de mapas para eventos, esse vai ser o diferencial do projeto, precisa ser dinamico de uma forma que quando o cliente clicar na barraca no mapa ele direcione ele para os dados desse fornecedor, mostrando o cardapio e algumas outras coisas como fotos dos pratos.

---

# Ideia do Construtor do Mapa

## Modelo Drag and Drop

O organizador poderá:

* Arrastar barracas

* Posicionar áreas

* Criar setores

* Definir tamanho do stand

* Definir áreas VIP

* Banheiros

* Palco

* Entrada

* Saída

---

# Estrutura Visual do Mapa

## Elementos do mapa

* Stand de comida

* Stand de bebida

* Chopp

* Banheiro

* Caixa

* Entrada

* Saída

* Palco

* Área kids

* Área VIP

---

# Sugestão Técnica do Mapa

## Frontend

* React Flow

  ou

* Konva.js

  ou

* Fabric.js

## Funcionalidades

* Zoom

* Arrastar elementos

* Grid automático

* Snap no grid

* Responsivo

* Exportar layout

* Salvar JSON do mapa

---

# Exemplo JSON do Mapa

```json

{

  "eventId": 1,

  "map": {

    "stands": [

      {

        "id": 1,

        "name": "Mexican Food",

        "x": 120,

        "y": 250,

        "width": 100,

        "height": 80,

        "type": "food"

      },

      {

        "id": 2,

        "name": "Beer Station",

        "x": 320,

        "y": 180,

        "width": 100,

        "height": 80,

        "type": "drink"

      }

    ]

  }

}

```

---

# Fluxo Completo do Pedido

## Fluxo

### Cliente

1. Escolhe evento

2. Visualiza mapa

3. Escolhe stand

4. Faz pedido

5. Paga

6. Aguarda preparo

### Empresa

1. Recebe pedido

2. Atualiza status

3. Marca como pronto

### Sistema

1. Atualiza timeline

2. Envia notificação

3. Envia WhatsApp

### Cliente

1. Recebe aviso

2. Vai retirar pedido

---

# Integração WhatsApp

## Sugestões

* Twilio

* Z-API

* Evolution API

* Meta WhatsApp Cloud API

## Mensagem Exemplo

```txt

Seu pedido #548 está pronto para retirada no stand Beer Station.

```

---

# Regras de Negócio

## Eventos

* Um evento possui vários stands

* Um stand pertence a uma empresa

* Um evento possui um mapa

* Um cliente pode ter vários pedidos

## Pedidos

* Pedido possui múltiplos itens

* Cada item pertence a uma empresa

* Timeline obrigatória

---

# Banco de Dados (Entidades)

# Principais tabelas

## Users

* Id

* Nome

* Email

* Telefone

* Tipo

## Events

* Id

* Nome

* Banner

* Data

* Local

* Status

## Companies

* Id

* Nome

* Categoria

## Stands

* Id

* EventId

* CompanyId

* PosX

* PosY

## Products

* Id

* StandId

* Nome

* Preco

## Orders

* Id

* UserId

* Status

* Total

## OrderItems

* Id

* OrderId

* ProductId

## OrderTimeline

* Id

* OrderId

* Status

* Data

---

# Diferenciais do Sistema

## Recursos Avançados

### IA Futuramente

* Previsão de filas

* Sugestão de stands próximos

* Heatmap do evento

### Analytics

* Stand mais vendido

* Tempo médio

* Fluxo de pessoas

### Gamificação

* Check-in

* Pontos

* Ranking

### QRCode

* Retirada do pedido

* Localização do stand

---

# Segurança

## Requisitos

* JWT

* Controle de acesso

* Multi-tenant

* Logs

* Auditoria

---

# Escalabilidade

## Arquitetura Recomendada

### Microsserviços

* Serviço de pedidos

* Serviço de notificações

* Serviço de mapa

* Serviço de pagamentos

* Serviço de usuários

---

# APIs Principais

## Eventos

```http

GET /events

POST /events

```

## Pedidos

```http

POST /orders

GET /orders/{id}

PATCH /orders/status

```

## Timeline

```http

GET /orders/{id}/timeline

```

## Mapa

```http

GET /events/{id}/map

POST /events/{id}/map

```

---

# Ideia Visual do Sistema

## Tema

* Moderno

* Festival

* FoodTech

* Dark mode opcional

## Estilo

* Cards modernos

* Mapa interativo

* Timeline animada

* Notificações em tempo real

---

# Funcionalidades Futuras

* Integração com pagamento PIX

* Integração cartão

* Autoatendimento

* Totens

* Impressão de comandas

* Dashboard em tempo real

* Controle de estoque

* Controle de filas

---

# MVP Inicial

## Fase 1

* Cadastro de eventos

* Cadastro de stands

* Cadastro de produtos

* Pedidos

* Timeline

* Notificações

* Mapa básico

## Fase 2

* WhatsApp

* Tempo real

* Dashboard

* Analytics

## Fase 3

* IA

* Heatmap

* Gamificação

* Mobile completo

---

# Prompt Final para Claude

Crie um sistema completo SaaS multi-tenant para gestão de eventos gastronômicos e festivais de comida/bebida utilizando:

O sistema deve possuir:

* Área administrativa

* Área da empresa/stores

* Área do cliente

* Sistema de pedidos

* Timeline em tempo real

* Integração WhatsApp

* Sistema de notificações

* Construtor visual de mapas drag and drop

* Mapa dinâmico por evento

* Gestão de stands

* Dashboard

* APIs REST

* Arquitetura escalável

* Boas práticas SOLID

* DDD

* Clean Architecture

* CQRS

* Docker

* Swagger

Também crie:

* Estrutura de pastas

* Modelagem do banco

* Fluxos

* Entidades

* DTOs

* Commands/Queries

* Estratégia de autenticação

* Estratégia de cache

* Estratégia de mensageria

* Estratégia de deploy

* Estrutura frontend

* Estrutura do construtor de mapas

* Exemplo visual do mapa

* Roadmap do projeto

* Estratégia SaaS multi-tenant

* Sistema pronto para produção.

---

# Como o projeto está organizado

| Pasta | O que é |
| --- | --- |
| `backend/` | API em .NET 10 (Clean Architecture + CQRS) com PostgreSQL, SignalR e servidor MCP |
| `frontend/` | Site em React + Vite + TanStack Router + Tailwind |
| `docs/` | Documentação (plano de migração e guia de como rodar) |

Guia completo: [docs/COMO-RODAR.md](docs/COMO-RODAR.md).

```sh
# 1) API (precisa do PostgreSQL rodando em localhost:5432, usuário/senha postgres)
cd backend
dotnet run --project src/EventFlow.Api --launch-profile http

# 2) Front-end (em outro terminal)
cd frontend
npm install
npm run dev        # http://localhost:5174
```

Ou tudo com Docker: `docker compose up --build` (front em http://localhost:8081).
