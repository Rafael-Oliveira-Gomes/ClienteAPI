# GumpStudio — Plano de Desenvolvimento e Arquitetura

## 1. Objetivo

Construir um sistema web responsivo, **mobile-first**, para gerenciamento de um estúdio de tatuagem.

O sistema deve centralizar:

- Clientes
- Tatuadores
- Sessões/tatuagens
- Estoque por tatuador/estoque
- Produtos e movimentações
- Financeiro básico
- Exames/documentos
- Agendamento
- Integração com Google Agenda
- Controle de acesso

### Premissas

- Aplicação inicialmente **monolítica modular**.
- Backend em **.NET / ASP.NET Core**.
- Banco relacional, preferencialmente **PostgreSQL**.
- Frontend web responsivo, com foco em uso pelo celular.
- API REST.
- Autenticação com JWT.
- Autorização baseada em perfil.
- Docker para ambiente local.
- Arquitetura preparada para crescer sem começar com a complexidade de microserviços.

---

# 2. Arquitetura recomendada

## 2.1 Escolha: Monólito Modular

Para o tamanho inicial do projeto, **não recomendo microserviços**.

A solução pode ser estruturada como um monólito com módulos bem separados:

```text
GumpStudio
│
├── API
│   ├── Auth
│   ├── Clientes
│   ├── Tatuadores
│   ├── Sessoes
│   ├── Estoque
│   ├── Produtos
│   ├── Exames
│   ├── Financeiro
│   └── Agenda
│
├── Application
│   ├── Clientes
│   ├── Tatuadores
│   ├── Sessoes
│   ├── Estoque
│   ├── Produtos
│   ├── Exames
│   ├── Financeiro
│   └── Agenda
│
├── Domain
│   ├── Entities
│   ├── Enums
│   ├── ValueObjects
│   ├── Exceptions
│   └── Rules
│
├── Infrastructure
│   ├── Persistence
│   ├── Identity
│   ├── GoogleCalendar
│   └── Storage
│
└── Tests
    ├── Unit
    └── Integration
```

## 2.2 Camadas

### API

Responsável por:

- Controllers/endpoints
- Autenticação
- Autorização
- Validação de entrada
- HTTP
- Swagger/OpenAPI

### Application

Responsável pelos casos de uso:

- Criar cliente
- Cadastrar tatuador
- Criar produto
- Registrar entrada de estoque
- Registrar saída
- Agendar sessão
- Criar evento no Google Calendar
- Registrar movimentação financeira

### Domain

Contém as regras de negócio.

Exemplos:

- Tatuador precisa possuir uma modalidade de remuneração.
- Percentual pode ser diferente para cada tatuador.
- Produto precisa possuir unidade de medida.
- Saída de estoque não pode gerar saldo negativo.
- Reajuste de inventário permitido apenas para redução.
- Valor pago pelo produto é obrigatório em determinadas movimentações.
- Tatuador rotativo possui acesso provisório.

### Infrastructure

Responsável por:

- PostgreSQL
- EF Core
- Google Calendar
- Armazenamento de imagens/documentos
- Implementações de repositórios, quando necessário

---

# 3. Perfis de acesso

## 3.1 Administrador

Acesso completo:

- Clientes
- Tatuadores
- Sessões
- Estoque
- Produtos
- Financeiro
- Exames
- Agenda
- Usuários
- Configurações

## 3.2 Tatuador fixo

Acesso operacional:

- Próprias sessões
- Clientes relacionados
- Estoques permitidos
- Cadastro/movimentação de produtos conforme permissão
- Exames necessários
- Agenda
- Informações próprias

O administrador deve conseguir definir quais estoques o tatuador pode utilizar.

## 3.3 Tatuador rotativo

Acesso provisório.

Exemplo:

```text
Tatuador rotativo
       ↓
Conta criada
       ↓
Data inicial
       ↓
Data final
       ↓
Acesso liberado
       ↓
Data final atingida
       ↓
Acesso bloqueado
```

O acesso deve possuir:

- Status
- Data de início
- Data de expiração
- Permissões

O sistema não deve depender apenas de exclusão de usuário para controlar esse acesso.

---

# 4. Domínio principal

```text
Usuário
   │
   ├── Administrador
   └── Tatuador
           │
           ├── Fixo
           └── Rotativo

Cliente
   │
   └── Sessão
          │
          ├── Tatuador
          ├── Exames
          ├── Consumo de estoque
          └── Agenda
```

---

# 5. Módulo de Tatuadores

## Cadastro

Campos sugeridos:

- Nome
- CPF
- Telefone
- E-mail
- Data de nascimento
- Foto
- Status
- Tipo: Fixo / Rotativo
- Data de início
- Data de fim, quando rotativo

## Modelo de remuneração

Cada tatuador deve possuir uma configuração própria.

Tipos:

### Valor fixo mensal

Exemplo:

```text
R$ 2.500/mês
```

### Valor diário

Exemplo:

```text
R$ 150/dia trabalhado
```

### Percentual

Exemplo:

```text
60% tatuador
40% estúdio
```

O percentual deve ser armazenado por tatuador e não como uma configuração global.

### Regra importante

Criar uma entidade/configuração de remuneração separada permite alterar a regra no futuro sem alterar o cadastro principal do tatuador.

---

# 6. Clientes

## MVP

Cadastro:

- Nome
- CPF
- Telefone
- WhatsApp
- E-mail
- Data de nascimento
- Endereço
- Observações
- Data de cadastro

## Histórico

O cliente deve possuir uma visão de:

- Sessões
- Tatuador
- Data
- Valor
- Status
- Exames/documentos relacionados

---

# 7. Exames e documentos

## Acesso

Todos os perfis autorizados devem conseguir visualizar os exames necessários para atendimento.

## MVP

Permitir:

- Cadastro de documento/exame
- Upload
- Visualização
- Data
- Cliente relacionado
- Sessão relacionada
- Observação
- Status

Exemplo:

```text
Cliente
   ↓
Exame/documento
   ├── Arquivo
   ├── Data
   ├── Tipo
   └── Observação
```

## Segurança

Documentos devem possuir autorização de acesso.

Não deixar arquivos privados disponíveis por URL pública sem controle.

---

# 8. Sessões / Tatuagens

Esse é um dos principais módulos do sistema.

## Cadastro

Uma sessão deve possuir:

- Cliente
- Tatuador
- Data
- Horário
- Duração estimada
- Valor da tatuagem
- Status
- Observações
- Local
- Referência/desenho, se necessário
- Estoque utilizado
- Evento do Google Calendar

## Status sugeridos

```text
Agendada
Confirmada
Em andamento
Concluída
Cancelada
Não compareceu
```

## Fluxo

```text
Cliente
   ↓
Criar sessão
   ↓
Selecionar tatuador
   ↓
Selecionar data/hora
   ↓
Criar evento no Google Agenda
   ↓
Sessão realizada
   ↓
Registrar produtos utilizados
   ↓
Registrar financeiro
   ↓
Concluir sessão
```

---

# 9. Estoque

Esse deve ser um dos módulos mais importantes do sistema.

## Conceito

Não tratar estoque simplesmente como:

```text
Produto = quantidade
```

O ideal é trabalhar com **movimentações de estoque**.

```text
Produto
   ↓
Movimentações
   ├── Entrada
   ├── Uso
   ├── Danificado
   └── Ajuste negativo
```

O saldo pode ser calculado a partir das movimentações ou mantido em uma coluna de saldo com atualização transacional.

## Tipos de movimentação

### Entrada

Compra/recebimento.

Campos:

- Produto
- Quantidade
- Valor pago por unidade
- Nota fiscal
- Data
- Estoque destino
- Observação

### Uso

Produto utilizado em uma tatuagem.

Campos:

- Produto
- Quantidade
- Estoque de origem
- Sessão
- Tatuador
- Valor pago por unidade
- Valor de venda opcional
- Data

### Danificado

Produto perdido/danificado.

Campos:

- Produto
- Quantidade
- Estoque
- Motivo
- Observação

### Ajuste de inventário

No MVP:

- Somente redução
- Produto
- Quantidade
- Estoque
- Motivo
- Observação
- Usuário responsável

Não permitir ajuste positivo nessa funcionalidade.

---

# 10. Estoques

O sistema deve permitir mais de um estoque.

Exemplo:

```text
Estoque Principal
Estoque João
Estoque Maria
Estoque Sala 02
```

Ao utilizar um produto em uma tatuagem:

```text
Produto: Tinta X
Quantidade: 2
Estoque: Estoque João
Sessão: #123
```

O sistema deve exigir a escolha do estoque de origem.

## Permissões

Administrador:

- Visualiza todos
- Cria
- Edita
- Movimenta

Tatuador fixo:

- Visualiza os estoques autorizados
- Pode cadastrar/movimentar produtos conforme permissão

Tatuador rotativo:

- Acesso limitado e temporário

---

# 11. Produtos

## Cadastro

Campos:

- Nome
- Unidade de medida
- Imagem opcional
- Código opcional
- Preço pago
- Preço de venda
- Estoque mínimo
- Status

## Unidades

O sistema deve permitir:

- ML
- Par
- Grama
- Caixa
- Batoque P
- Batoque M
- Batoque G
- Batoque GG

Recomendação: não criar uma coluna para cada unidade.

Criar:

```text
UnitOfMeasure
```

ou um enum controlado.

Exemplo:

```text
ML
PAIR
GRAM
BOX
BATOQUE_P
BATOQUE_M
BATOQUE_G
BATOQUE_GG
```

---

# 12. Valor do produto

Existe uma regra importante:

## Valor pago

Obrigatório para registrar o custo da utilização/entrada conforme a regra definida.

Exemplo:

```text
Produto: Tinta preta
Quantidade: 10 ml
Custo unitário: R$ 4,50
```

## Valor de venda

Opcional.

Exemplo:

```text
Custo: R$ 4,50
Venda: R$ 10,00
```

Isso permite futuramente calcular:

- Custo
- Receita
- Margem
- Consumo
- Rentabilidade

---

# 13. Estoque mínimo

Cada produto pode possuir:

```text
Estoque mínimo = 10
Saldo atual = 7
```

Sistema:

```text
7 < 10
↓
Estoque abaixo do mínimo
↓
Gerar alerta
```

No MVP pode ser apenas um alerta visual.

Depois:

- Notificação
- E-mail
- WhatsApp
- Dashboard

---

# 14. Nota fiscal

No MVP, não é necessário integrar automaticamente com emissão fiscal.

O sistema deve permitir registrar:

- Número da nota
- Série
- Data
- Fornecedor
- Valor total
- Arquivo/XML/PDF
- Produtos relacionados

A entrada de estoque pode estar relacionada à nota fiscal.

---

# 15. Financeiro

No MVP, manter extremamente simples.

## Entradas

Exemplos:

- Pagamento de tatuagem
- Venda de produto
- Outros recebimentos

## Saídas

Exemplos:

- Compra de estoque
- Despesas
- Pagamento de tatuador
- Outros custos

## Lançamento

```text
Tipo: Entrada / Saída
Descrição
Valor
Data
Categoria
Sessão opcional
Tatuador opcional
Observação
```

## Dashboard

Mostrar:

- Entradas do período
- Saídas do período
- Saldo
- Total de sessões
- Total recebido

Não transformar o MVP em um ERP financeiro.

---

# 16. Google Agenda

## Objetivo

Ao criar uma sessão no sistema:

```text
Sessão
   ↓
Google Calendar API
   ↓
Evento criado
```

O evento deve conter:

- Nome do cliente
- Tatuador
- Data
- Horário
- Duração
- Telefone
- Observações

## Sincronização

No MVP:

### Sistema → Google Agenda

Criar:

- Evento

Atualizar:

- Data
- Horário
- Tatuador
- Informações

Cancelar:

- Evento

## Identificador

Salvar no banco:

```text
GoogleCalendarEventId
```

Isso evita criar eventos duplicados.

## Fase futura

Sincronização bidirecional:

```text
Sistema ↔ Google Calendar
```

Não considero isso obrigatório para o MVP.

---

# 17. Modelo de dados inicial

Principais entidades:

```text
User
Role
TattooArtist
ArtistCompensation
Client
TattooSession
ExamDocument
Product
Stock
StockMovement
Invoice
FinancialTransaction
GoogleCalendarEvent
```

Relacionamentos principais:

```text
TattooArtist
   └── ArtistCompensation

Client
   └── TattooSession
          ├── TattooArtist
          ├── ExamDocument
          ├── StockMovement
          └── GoogleCalendarEvent

Product
   └── StockMovement
          └── Stock

Invoice
   └── StockMovement

FinancialTransaction
   ├── Client
   ├── TattooArtist
   └── TattooSession
```

---

# 18. API — organização

Exemplo:

```text
/api/auth
/api/users
/api/artists
/api/clients
/api/sessions
/api/exams
/api/products
/api/stocks
/api/stock-movements
/api/invoices
/api/financial-transactions
/api/calendar
```

Preferir endpoints orientados a casos de uso quando fizer sentido.

Exemplo:

```http
POST /api/stock-movements/usage
POST /api/stock-movements/damaged
POST /api/stock-movements/inventory-adjustment
```

em vez de expor uma operação genérica que permita qualquer tipo de alteração.

---

# 19. Segurança

## Autenticação

- JWT
- Refresh token
- Senha com hash seguro
- Expiração de token
- Revogação quando necessário

## Autorização

Usar policies/roles.

Exemplo:

```text
Admin
Artist
```

Mas não depender apenas da role.

Também validar:

- Estoque permitido
- Sessão pertencente ao tatuador
- Conta rotativa expirada
- Permissões específicas

---

# 20. Mobile-first

Essa é uma premissa importante do produto.

Não desenvolver primeiro pensando em desktop.

## Telas prioritárias para celular

### Dashboard

```text
Olá, João

Hoje
┌───────────────────┐
│ 3 sessões         │
└───────────────────┘

┌───────────────────┐
│ Estoque baixo: 4  │
└───────────────────┘

┌───────────────────┐
│ R$ 1.250 entradas │
└───────────────────┘
```

### Tatuador

Fluxo rápido:

```text
Sessão
 ↓
Cliente
 ↓
Produtos usados
 ↓
Selecionar estoque
 ↓
Quantidade
 ↓
Confirmar
```

O objetivo é evitar formulários gigantes.

---

# 21. MVP

## MVP 1 — Fundação

- [ ] Criar solução .NET
- [ ] PostgreSQL
- [ ] Docker
- [ ] EF Core
- [ ] Migrations
- [ ] Swagger
- [ ] JWT
- [ ] Roles
- [ ] Estrutura de frontend
- [ ] Layout mobile-first
- [ ] CI básico
- [ ] Logs
- [ ] Tratamento global de exceções

---

# 22. MVP 2 — Usuários e tatuadores

- [ ] Login
- [ ] Logout/expiração
- [ ] Cadastro de administrador
- [ ] Cadastro de tatuador
- [ ] Tatuador fixo
- [ ] Tatuador rotativo
- [ ] Data de expiração
- [ ] Ativação/desativação
- [ ] Valor mensal
- [ ] Valor diário
- [ ] Percentual
- [ ] Percentual individual por tatuador
- [ ] Controle de acesso

---

# 23. MVP 3 — Clientes

- [ ] Cadastro
- [ ] Edição
- [ ] Consulta
- [ ] Busca
- [ ] Histórico
- [ ] Dados de contato
- [ ] Observações
- [ ] Histórico de sessões

---

# 24. MVP 4 — Exames/documentos

- [ ] Upload
- [ ] Visualização
- [ ] Vincular cliente
- [ ] Vincular sessão
- [ ] Tipo de documento
- [ ] Data
- [ ] Observação
- [ ] Controle de acesso

---

# 25. MVP 5 — Produtos

- [ ] Cadastro
- [ ] Edição
- [ ] Ativação/desativação
- [ ] Nome
- [ ] Unidade
- [ ] Imagem
- [ ] Código
- [ ] Preço pago
- [ ] Preço de venda
- [ ] Estoque mínimo
- [ ] Busca

---

# 26. MVP 6 — Estoque

- [ ] Cadastro de estoque
- [ ] Estoque principal
- [ ] Estoques por tatuador
- [ ] Permissões por estoque
- [ ] Entrada
- [ ] Uso
- [ ] Danificado
- [ ] Ajuste negativo
- [ ] Histórico de movimentações
- [ ] Saldo por estoque
- [ ] Valor pago obrigatório
- [ ] Valor de venda opcional
- [ ] Alerta de estoque mínimo
- [ ] Seleção de estoque na sessão

---

# 27. MVP 7 — Sessões

- [ ] Criar sessão
- [ ] Editar sessão
- [ ] Cancelar sessão
- [ ] Confirmar sessão
- [ ] Concluir sessão
- [ ] Cliente
- [ ] Tatuador
- [ ] Data
- [ ] Horário
- [ ] Valor
- [ ] Observações
- [ ] Produtos utilizados
- [ ] Histórico

---

# 28. MVP 8 — Google Agenda

- [ ] OAuth Google
- [ ] Permissões Calendar
- [ ] Configuração da conta
- [ ] Criar evento
- [ ] Atualizar evento
- [ ] Cancelar evento
- [ ] Salvar GoogleCalendarEventId
- [ ] Tratamento de falha da integração
- [ ] Evitar eventos duplicados

---

# 29. MVP 9 — Financeiro

- [ ] Entrada
- [ ] Saída
- [ ] Categorias
- [ ] Filtros por período
- [ ] Histórico
- [ ] Saldo
- [ ] Relatório simples
- [ ] Vincular sessão
- [ ] Vincular tatuador

---

# 30. MVP 10 — Nota fiscal

- [ ] Cadastro de nota
- [ ] Número
- [ ] Série
- [ ] Fornecedor
- [ ] Data
- [ ] Valor
- [ ] Upload PDF/XML
- [ ] Relacionar entrada de estoque

---

# 31. Dashboard

## Administrador

Exibir:

- Sessões do dia
- Sessões futuras
- Entradas
- Saídas
- Saldo
- Produtos abaixo do mínimo
- Últimas movimentações
- Tatuadores ativos

## Tatuador

Exibir:

- Próximas sessões
- Sessões do dia
- Clientes
- Estoques disponíveis
- Produtos abaixo do mínimo dos estoques permitidos

---

# 32. Regras de negócio importantes

## Estoque

### RN01

Uma saída nunca pode deixar o estoque negativo.

### RN02

Toda saída deve informar o estoque de origem.

### RN03

Uso deve estar vinculado a uma sessão.

### RN04

Valor pago deve ser obrigatório na movimentação de uso.

### RN05

Valor de venda pode ser nulo.

### RN06

Ajuste de inventário no MVP só pode reduzir estoque.

### RN07

Usuário só pode movimentar estoque ao qual possui acesso.

### RN08

Toda movimentação deve registrar o usuário responsável.

### RN09

Movimentações de estoque não devem ser apagadas fisicamente.

Se houver erro:

```text
Movimentação incorreta
       ↓
Nova movimentação de correção
```

Isso mantém auditoria.

---

# 33. Auditoria

Para estoque e financeiro, registrar:

```text
CreatedAt
CreatedBy
UpdatedAt
UpdatedBy
```

Para movimentações críticas:

```text
UserId
Timestamp
IP, quando aplicável
Origem
```

Não permitir DELETE físico de movimentações financeiras/estoque.

---

# 34. Testes

Como projeto de produção, criar testes principalmente nas regras de negócio.

## Unitários

Testar:

- Cálculo de saldo
- Estoque mínimo
- Saída sem saldo
- Ajuste negativo
- Percentual do tatuador
- Expiração do rotativo
- Regras financeiras
- Status da sessão

## Integração

Testar:

- API + PostgreSQL
- EF Core
- Autorização
- Estoque
- Sessões
- Google Calendar usando abstração/mock onde apropriado

## Testes críticos

```text
Criar sessão
 ↓
Registrar uso
 ↓
Reduzir estoque
 ↓
Criar financeiro
 ↓
Concluir sessão
```

Esse fluxo deve ter pelo menos um teste de integração.

---

# 35. Observabilidade

Mínimo:

- Logs estruturados
- Correlation ID
- Logs de erro
- Tempo de resposta
- Health Check
- Health Check do PostgreSQL

Futuro:

- OpenTelemetry
- Metrics
- Tracing
- Dashboard de observabilidade

---

# 36. Deploy

Inicialmente:

```text
Frontend
   ↓
Hosting

API
   ↓
Docker
   ↓
Cloud

PostgreSQL
   ↓
Managed Database
```

Não é necessário Kubernetes no MVP.

---

# 37. Prazo estimado

Você informou que é Dev Pleno e pretende desenvolver aproximadamente **3 horas por dia**.

Considerando:

```text
3h/dia
5 dias/semana
≈ 15h/semana
```

Uma estimativa realista para um MVP funcional é:

## 10 a 12 semanas

Aproximadamente:

```text
150–180 horas
```

Isso inclui desenvolvimento, testes, ajustes, integração e deploy básico.

---

# 38. Cronograma sugerido

## Semana 1 — Fundação

15h

- Arquitetura
- Solution
- Docker
- PostgreSQL
- EF Core
- Auth
- JWT
- Frontend base
- CI

## Semana 2 — Usuários/Tatuadores

15h

- Login
- Admin
- Tatuadores
- Fixo
- Rotativo
- Permissões
- Remuneração

## Semana 3 — Clientes

15h

- CRUD
- Busca
- Histórico
- Layout mobile

## Semana 4 — Produtos

15h

- Produtos
- Unidades
- Imagem
- Preços
- Estoque mínimo

## Semana 5 — Estoque

15h

- Estoques
- Permissões
- Entrada
- Saída
- Uso
- Danificado
- Ajuste
- Auditoria

## Semana 6 — Sessões

15h

- Agendamento
- Clientes
- Tatuadores
- Status
- Produtos utilizados
- Integração com estoque

## Semana 7 — Exames + documentos

15h

- Upload
- Consulta
- Vinculação
- Segurança

## Semana 8 — Google Agenda

15h

- OAuth
- Criar evento
- Atualizar
- Cancelar
- Tratamento de erros

## Semana 9 — Financeiro

15h

- Entradas
- Saídas
- Categorias
- Dashboard
- Relatórios simples

## Semana 10 — Nota fiscal + dashboard

15h

- Nota fiscal
- Upload
- Dashboard admin
- Dashboard tatuador

## Semana 11 — Testes e segurança

15h

- Unitários
- Integração
- Permissões
- Casos de estoque
- Correções

## Semana 12 — Deploy e estabilização

15h

- Deploy
- Logs
- Health checks
- Correções
- UX mobile
- Homologação

---

# 39. Backlog inicial

## Épico: Autenticação

- [ ] Login
- [ ] JWT
- [ ] Refresh token
- [ ] Roles
- [ ] Recuperação de senha
- [ ] Alteração de senha

## Épico: Tatuadores

- [ ] CRUD
- [ ] Fixo
- [ ] Rotativo
- [ ] Expiração
- [ ] Remuneração mensal
- [ ] Remuneração diária
- [ ] Percentual

## Épico: Clientes

- [ ] CRUD
- [ ] Busca
- [ ] Histórico

## Épico: Sessões

- [ ] Agendar
- [ ] Confirmar
- [ ] Cancelar
- [ ] Concluir
- [ ] Produtos utilizados
- [ ] Histórico

## Épico: Produtos

- [ ] CRUD
- [ ] Unidade
- [ ] Imagem
- [ ] Código
- [ ] Preço de compra
- [ ] Preço de venda
- [ ] Estoque mínimo

## Épico: Estoque

- [ ] Criar estoque
- [ ] Permissões
- [ ] Entrada
- [ ] Uso
- [ ] Danificado
- [ ] Ajuste negativo
- [ ] Histórico
- [ ] Alertas

## Épico: Exames

- [ ] Upload
- [ ] Consulta
- [ ] Vincular cliente
- [ ] Vincular sessão

## Épico: Financeiro

- [ ] Entrada
- [ ] Saída
- [ ] Categorias
- [ ] Saldo
- [ ] Relatórios

## Épico: Google Agenda

- [ ] OAuth
- [ ] Criar evento
- [ ] Atualizar evento
- [ ] Cancelar evento

## Épico: Nota fiscal

- [ ] Cadastro
- [ ] Upload
- [ ] Relacionamento com estoque

---

# 40. Fora do MVP

Para evitar que o projeto cresça indefinidamente, deixar para uma segunda fase:

- WhatsApp
- Lembrete automático para cliente
- Pagamento online
- PIX integrado
- Emissão de nota fiscal automática
- NF-e/NFC-e
- BI avançado
- Multiunidade
- Programa de fidelidade
- Comissões complexas
- Marketplace
- Aplicativo Android/iOS nativo
- Sincronização bidirecional completa com Google Calendar
- Controle financeiro contábil
- Integrações bancárias
- Kubernetes
- Microserviços

---

# 41. Ordem recomendada de desenvolvimento

A sequência ideal é:

```text
1. Fundação
      ↓
2. Autenticação
      ↓
3. Tatuadores
      ↓
4. Clientes
      ↓
5. Produtos
      ↓
6. Estoques
      ↓
7. Sessões
      ↓
8. Exames
      ↓
9. Financeiro
      ↓
10. Google Agenda
      ↓
11. Nota fiscal
      ↓
12. Dashboard
      ↓
13. Testes
      ↓
14. Deploy
```

A integração com Google Agenda deve ficar depois do fluxo interno de sessões estar funcionando.

---

# 42. Decisões arquiteturais importantes

## Não começar com microserviços

O domínio ainda está sendo descoberto.

Um monólito modular permite:

- Desenvolvimento mais rápido
- Deploy simples
- Debug mais simples
- Banco único
- Menor custo
- Menor complexidade operacional

## Não criar Generic Repository para tudo

Com EF Core, usar diretamente `DbContext`/repositories específicos quando fizer sentido.

Evitar abstrações que apenas escondem o EF Core.

## Estoque baseado em movimentações

Essa é uma decisão importante.

Evitar simplesmente:

```text
Product.StockQuantity--
```

O sistema precisa manter histórico.

## Financeiro baseado em lançamentos

Da mesma forma:

```text
Entrada
Saída
```

devem ser lançamentos imutáveis/auditáveis.

---

# 43. Resultado esperado do MVP

Ao final do MVP, o estúdio deverá conseguir executar o fluxo:

```text
ADMIN
 │
 ├── Cadastra tatuador
 │
 ├── Define remuneração
 │
 ├── Cria estoques
 │
 └── Cadastra produtos
          │
          ↓
      ESTOQUE
          │
          ↓
CLIENTE → SESSÃO ← TATUADOR
             │
             ├── Google Agenda
             │
             ├── Exames
             │
             └── Produtos utilizados
                     │
                     ↓
                  ESTOQUE
                     │
                     ↓
                 FINANCEIRO
```

O sistema deverá permitir que o administrador tenha uma visão central do estúdio, enquanto o tatuador consiga executar as operações necessárias pelo celular com poucos passos.

---

# 44. Primeira versão do backlog técnico

### Sprint 0

- [ ] Definir stack frontend
- [ ] Criar repositório
- [ ] Criar solution
- [ ] Configurar Docker
- [ ] PostgreSQL
- [ ] EF Core
- [ ] Swagger
- [ ] JWT
- [ ] Estrutura de módulos
- [ ] Pipeline CI
- [ ] README

### Sprint 1

- [ ] User
- [ ] Role
- [ ] Login
- [ ] Tatuador
- [ ] Remuneração

### Sprint 2

- [ ] Cliente
- [ ] Sessão
- [ ] Status da sessão

### Sprint 3

- [ ] Produto
- [ ] Estoque
- [ ] Movimentação

### Sprint 4

- [ ] Integração sessão + estoque
- [ ] Alertas
- [ ] Auditoria

### Sprint 5

- [ ] Exames
- [ ] Upload
- [ ] Permissões

### Sprint 6

- [ ] Financeiro
- [ ] Dashboard

### Sprint 7

- [ ] Google Calendar

### Sprint 8

- [ ] Nota fiscal
- [ ] Testes
- [ ] Segurança
- [ ] Deploy

---

# 45. Critério de conclusão do MVP

O MVP pode ser considerado pronto quando um usuário conseguir realizar, do início ao fim:

```text
Cadastrar cliente
      ↓
Cadastrar tatuador
      ↓
Cadastrar produto
      ↓
Cadastrar estoque
      ↓
Agendar tatuagem
      ↓
Criar evento no Google Agenda
      ↓
Realizar sessão
      ↓
Registrar produtos utilizados
      ↓
Escolher estoque de origem
      ↓
Atualizar estoque
      ↓
Registrar entrada/saída financeira
      ↓
Consultar histórico
```

E o administrador conseguir:

```text
Gerenciar usuários
Gerenciar tatuadores
Gerenciar clientes
Gerenciar sessões
Gerenciar produtos
Gerenciar estoques
Visualizar alertas
Gerenciar documentos
Gerenciar financeiro
Consultar notas fiscais
```

---

# 46. Próximo passo técnico

Antes de começar a codar as telas, definir:

1. Entidades e relacionamentos.
2. Regras de negócio do estoque.
3. Permissões por perfil.
4. Fluxo de sessão.
5. Contratos da API.
6. Estratégia de armazenamento de arquivos.
7. OAuth do Google.
8. Wireframes mobile das principais telas.

A primeira implementação recomendada é o **modelo de domínio + banco + autenticação**, seguida pelo fluxo completo **Cliente → Sessão → Uso de estoque**, porque esse fluxo valida a parte mais importante do sistema.
