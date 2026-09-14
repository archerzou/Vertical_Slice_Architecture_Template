# Vertical Slice Architecture Template

A pragmatic **Vertical Slice Architecture** starter for **.NET 10**. A single Web API project, organized by feature instead of by layer — batteries included, opinionated where it matters, and easy to extend.

> Instead of spreading a feature across Domain / Application / Infrastructure layers, each use case lives in **one file** that contains its request, validation, handler, and HTTP endpoint. Changes stay local, slices stay independent, and the code reads top-to-bottom.

## Tech stack

| Area | Choice |
| --- | --- |
| Runtime | .NET 10 (net10.0) — `net8.0` / `net9.0` supported, see `Directory.Build.props` |
| Web | ASP.NET Core Minimal APIs |
| Messaging | Custom CQRS abstractions (no MediatR), auto-registered via [Scrutor](https://github.com/khellang/Scrutor) |
| Validation | FluentValidation (decorator-based) |
| Persistence | EF Core 10 + PostgreSQL (Npgsql), snake_case naming |
| Caching | `HybridCache` |
| Auth | JWT bearer + refresh tokens (with rotation), permission-based authorization |
| Observability | Serilog + OpenTelemetry (traces/metrics/logs) → Grafana / Loki / Tempo / Prometheus |
| API docs | OpenAPI + Scalar reference UI (with JWT support) |
| Testing | xUnit, Shouldly, NSubstitute, Testcontainers, NetArchTest |

## Getting started

Requirements: **.NET 10 SDK** and **Docker** (for PostgreSQL and the observability stack).

```bash
docker compose up -d                    # PostgreSQL + Grafana/Loki/Tempo/Prometheus
dotnet run --project src/Web.Api
```

Then open:

- **API reference (Scalar):** `https://localhost:5001/scalar/v1`
- **Grafana:** http://localhost:3000 (logs, traces, metrics)

The `Development` config already points at the Dockerized Postgres and OTLP collector (see `src/Web.Api/appsettings.Development.json`), so no extra setup is needed to run locally.

### Run the tests

Integration tests spin up a throwaway PostgreSQL container via Testcontainers, so **Docker must be running**:

```bash
dotnet test VerticalSlice.slnx
```

## Project layout

```
src/Web.Api/                     # the single application project (namespace Web.Api)
├─ Features/                     # one folder per feature; one file per use case
│  ├─ Todos/                     #   sample feature: entity, errors, domain events, slices
│  │  ├─ CreateTodo.cs           #   Command + Validator + Handler + Endpoint (one file)
│  │  ├─ GetTodos.cs             #   Query returning a PagedList<Response>
│  │  ├─ TodoItem.cs             #   entity
│  │  ├─ TodoItemErrors.cs       #   error catalog
│  │  └─ ...
│  └─ Users/                     #   register, login, refresh, permissions
├─ Common/                       # cross-cutting building blocks
│  ├─ Messaging/                 #   ICommand/IQuery + handler interfaces
│  ├─ Behaviors/                 #   validation + logging decorators
│  ├─ Endpoints/                 #   IEndpoint, Tags, endpoint mapping
│  ├─ Extensions/                #   migrations, observability, rate limiting, results
│  ├─ Result.cs / Error.cs       #   Result<T> + Error primitives
│  └─ PagedList.cs, IdempotencyFilter.cs, ...
├─ Database/                     # ApplicationDbContext, EF configurations, migrations, event dispatcher
├─ Authentication/               # JWT, refresh tokens, password hashing, IUserContext
├─ Authorization/                # permission-based authorization
├─ Notifications/                # IEmailSender + LoggingEmailSender
└─ Program.cs / DependencyInjection.cs
tests/
├─ Web.Api.UnitTests/            # handler + validator unit tests
├─ IntegrationTests/             # HTTP tests against a real Postgres (Testcontainers)
└─ ArchitectureTests/            # convention/architecture rules (NetArchTest)
```

## Anatomy of a slice

Every use case is a `public static class` nesting the whole slice. This is the complete `CreateTodo` feature — request, validation, business logic, and route — in one place:

```csharp
public static class CreateTodo
{
    public sealed class Command : ICommand<Guid> { /* ... */ }

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator() => RuleFor(c => c.Description).NotEmpty().MaximumLength(255);
    }

    internal sealed class Handler(ApplicationDbContext context, IUserContext userContext)
        : ICommandHandler<Command, Guid>
    {
        public async Task<Result<Guid>> Handle(Command command, CancellationToken ct)
        {
            // returns Result.Failure(...) for expected failures — no exceptions
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapPost("todos", /* ... */)
               .WithTags(Tags.Todos)
               .WithIdempotency()
               .RequireAuthorization();
    }
}
```

Handlers and endpoints are discovered and registered automatically — you never wire a slice up by hand.

## Conventions

- **One file per use case** — `Features/{Entity}/{UseCase}.cs`, `namespace Web.Api.Features.{Entity}`.
- **CQRS, no MediatR** — `ICommand`/`IQuery` + handler interfaces (`Common/Messaging`). Handlers are `internal sealed` with primary constructors, discovered by Scrutor, and wrapped by `ValidationDecorator` + `LoggingDecorator`.
- **Errors, not exceptions** — expected failures return `Result.Failure(...)` with a typed `Error` from a static `{Entity}Errors` catalog; endpoints translate via `result.Match(Results.Ok, CustomResults.Problem)` into RFC `ProblemDetails`.
- **Slice isolation** — a slice may use shared `Common`/`Database`/`Authentication`/`Authorization` code and another feature's **domain types**, but never another feature's command/handler/endpoint.
- **Direct DbContext** — inject the concrete `ApplicationDbContext` into handlers. There is no `IApplicationDbContext` or repository. Entities use FK ids (shadow relationships), configured in `Database/Configurations/`.
- **List queries** return `PagedList<T>` (search + sort + page) and project to a nested `Response` DTO — entities are never returned from the API.
- **Caching** — `HybridCache` with per-feature `{Feature}CacheKeys` helpers, invalidated by the commands that mutate the data.
- **Analyzer-strict** — `TreatWarningsAsErrors` + SonarAnalyzer. Keep code warning-clean (prefer collection expressions `[.. x]` and `ToUpperInvariant`).

## Configuration

Key settings live in `appsettings.json` (overridden per environment):

| Section | Purpose |
| --- | --- |
| `ConnectionStrings:Database` | PostgreSQL connection string |
| `Jwt` | `Secret`, `Issuer`, `Audience`, `ExpirationInMinutes` — put the secret in user-secrets for real use |
| `Cors:AllowedOrigins` | allowed browser origins |
| `RateLimiting` | `Global` and `Authentication` policies (`PermitLimit` + `WindowInSeconds`) |

## Targeting .NET 8 or 9

The template targets `net10.0` by default. To retarget, uncomment the relevant `TargetFramework` in `Directory.Build.props`, adjust package versions in `Directory.Packages.props` if needed, and switch the Dockerfile (`.NET 8`/`.NET 9` variants are included in `src/Web.Api`).

## Working with Claude Code

This repo ships with Claude Code skills that scaffold code that matches these conventions:

- `/add-feature` — a new use case (command/query + handler + validator + endpoint + tests)
- `/add-entity` — entity + error catalog + domain events + EF config + migration
- `/add-tests` — backfill unit / validator / integration tests
- `/add-migration` — create and apply an EF Core migration
- `/add-background-job` — a hosted `BackgroundService` worker
- `/bootstrap` — rename the solution and strip the sample `Todos` feature to start clean
- `/vsa-review` — review pending changes against these conventions

See `CLAUDE.md` for the full working conventions.
