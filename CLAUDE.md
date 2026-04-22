# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run Commands

```bash
# Start infrastructure (PostgreSQL + RabbitMQ)
docker compose up -d

# Build entire solution
dotnet build

# Run the API project
dotnet run --project WolverineOutboxDemo.Api

# Run all tests
dotnet test

# Run a specific test by name
dotnet test --filter "FullyQualifiedName~TestMethodName"
```

The solution uses `WolverineOutboxDemo.slnx` (XML-based solution format, not `.sln`).

## Architecture

This is a .NET 10 demo project showcasing the **Wolverine Outbox/Inbox pattern** with RabbitMQ and PostgreSQL. The message flow ensures exactly-once processing: writes to the database and message publishes happen atomically via the outbox table.

### Message Flow

```
HTTP Request → Endpoint (IDbContextOutbox) → DB write + Outbox message
  → Wolverine → RabbitMQ → Handler (Inbox) → DB write + cascade messages
```

`RegisterUser` → `UserRegistered` → `SendWelcomeEmail` → `WelcomeEmailSent` — handlers chain via Wolverine's cascading message pattern (returning a message from a handler automatically publishes it).

### Project Structure

- **WolverineOutboxDemo.Api** — ASP.NET Core Minimal API host. Contains endpoints, handlers, EF Core DbContext, and Wolverine configuration.
- **WolverineOutboxDemo.Contracts** — Shared message types (record types for commands/events). Referenced by the API.
- **WolverineOutboxDemo.Tests** — xUnit test project (currently empty).

### Key Wolverine Wiring (Program.cs)

- `IDbContextOutbox<AppDbContext>` — atomic DB save + message publish in one transaction (`SaveChangesAndFlushMessagesAsync`)
- `opts.UseEntityFrameworkCoreTransactions()` — integrates Wolverine with EF Core transaction lifecycle
- `opts.Policies.UseDurableOutboxOnAllSendingEndpoints()` — all outgoing messages go through outbox table first
- `opts.Policies.UseDurableInboxOnAllListeners()` — all incoming messages go through inbox table for dedup
- `opts.PersistMessagesWithPostgresql()` — Wolverine's own persistence tables in the same PostgreSQL DB
- RabbitMQ exchanges: `user-registration` (topic), `user-events` (topic), queue `send-welcome-email`

### Two Endpoint Patterns

1. `/api/users/register-outbox` — Correct: uses `IDbContextOutbox` for atomic write+publish
2. `/api/users/register-unsafe` — Anti-pattern: separate `SaveChangesAsync` + `SendAsync` (dual-write risk)

### Infrastructure

PostgreSQL 16 on `localhost:5432` (db: `wolverine_demo`), RabbitMQ 3.13 on `localhost:5672` (management UI: `localhost:15672`). Both via `docker-compose.yml`.

## Conventions

- Messages are immutable `record` types in the Contracts project
- Handlers are static methods in `Handlers/` — Wolverine discovers them by matching method signatures to message types
- Endpoints use Minimal API style with `IEndpointRouteBuilder` extension methods
- Wolverine log level is `Debug` in appsettings.json for tracing message lifecycle
