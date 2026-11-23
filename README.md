# GumpStudio API

Back-end do sistema de gerenciamento do estúdio de tatuagem, seguindo `Docs/gumpstudio-plano-arquitetura.md`:
**monólito modular**, .NET 10 / ASP.NET Core, PostgreSQL, EF Core, API REST.

## Estrutura

```
src/
  GumpStudio.Domain          entidades, enums, regras de negócio (sem dependências)
  GumpStudio.Application     casos de uso por módulo (services), DTOs, abstrações
  GumpStudio.Infrastructure  EF Core/PostgreSQL, Google (login + Calendar), JWT, armazenamento de arquivos
  GumpStudio.Api             endpoints (minimal API), auth, erros, health, OpenAPI
tests/
  GumpStudio.Tests           regras de domínio + fluxo crítico (EF InMemory)
```

Decisões da doc que o código segue: sem Generic Repository (serviços usam o `DbContext`), estoque por
movimentações imutáveis, financeiro por lançamentos imutáveis (estorno = lançamento negativo), endpoints por
caso de uso (`/api/stock-movements/usage`, `/damaged`, `/inventory-adjustment`), auditoria
(`CreatedAt/By`, `UpdatedAt/By`) e sem DELETE físico de movimentações.

## Autenticação (Google) e sessão com o Google Agenda

**Login:** o front usa "Entrar com Google" e envia o *ID token* para `POST /api/auth/google`. A API valida o token
no Google, procura o e-mail em `Users` e devolve **JWT** (curto) + **refresh token** (rotativo, só o hash fica no banco).
Não existe senha local. Só entra quem um administrador cadastrou — exceto o **primeiro admin**, definido em
`Auth:BootstrapAdminEmail` (vale só enquanto não houver usuários).

**Acesso provisório (rotativo):** a cada requisição a API confere se o usuário está ativo e dentro de
`AccessStartsAt`/`AccessExpiresAt`; passou da data, o token deixa de funcionar na hora.

**Google Agenda (Sistema → Agenda):** o administrador conecta *uma* conta Google do estúdio:
1. `GET /api/calendar/connect` → devolve a URL de consentimento;
2. o Google redireciona para `GET /api/calendar/callback`, a API troca o `code` pelo refresh token e o guarda
   **criptografado** (Data Protection).

Ao criar/alterar/cancelar uma sessão a API cria/atualiza/remove o evento. O `EventId` do evento é o id da sessão,
então reenvios nunca duplicam; se o Google falhar, a sessão é salva com `calendarSyncStatus = Failed` e pode ser
reenviada em `POST /api/calendar/sessions/{id}/sync`.

### Configurar o Google Cloud (uma vez)
1. Crie um projeto, ative a **Google Calendar API**.
2. Tela de consentimento OAuth + credencial **OAuth client ID (Web)**.
3. *Authorized redirect URIs*: `http://localhost:5080/api/calendar/callback` (e a de produção).
   *Authorized JavaScript origins*: a URL do front (para o botão de login).
4. Use o mesmo **Client ID** no front (login) e na API (`Google:ClientId`/`ClientSecret`).

## Rodando

```bash
cp .env.example .env                 # ajuste os valores
docker compose up -d db              # PostgreSQL
dotnet run --project src/GumpStudio.Api
```
Em desenvolvimento a API sobe em `http://localhost:5080` com documentação em `/scalar/v1`, e aplica as migrations
(`Database:AutoMigrate`). Segredos (`Jwt:SigningKey`, `Google:ClientSecret`) via *user-secrets* ou variáveis de ambiente:

```bash
dotnet user-secrets --project src/GumpStudio.Api set "Google:ClientId" "..."
dotnet user-secrets --project src/GumpStudio.Api set "Google:ClientSecret" "..."
dotnet user-secrets --project src/GumpStudio.Api set "Auth:BootstrapAdminEmail" "seu-email@gmail.com"
```

### Primeira migration (ainda não existe — gere antes do primeiro uso)
```bash
dotnet tool install --global dotnet-ef
dotnet ef migrations add Initial --project src/GumpStudio.Infrastructure --startup-project src/GumpStudio.Api --output-dir Persistence/Migrations
```

## Testes
```bash
dotnet test
```

## Perfis e regras de acesso
| Perfil | Pode |
|---|---|
| Admin | tudo: usuários, tatuadores, clientes, sessões, produtos, estoques, financeiro, notas, calendário |
| Tatuador (fixo/rotativo) | suas sessões, clientes que cadastrou/atende, exames desses clientes, estoques liberados, dashboard próprio |

Escrita de produtos, estoques e financeiro é restrita ao admin no MVP (a doc prevê "conforme permissão"; permissões
finas por tatuador ficam para depois).

## Health e logs
`GET /health` (verifica o PostgreSQL). Cada resposta devolve `X-Correlation-Id`; os logs trazem método, rota, status e tempo.
