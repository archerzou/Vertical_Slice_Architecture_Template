# Vertical Slice Architecture Template — notes for Claude Code

Working conventions for this repo. Match them so generated code looks hand-written.

## Project layout (one project)

Everything lives in a single `src/Web.Api` project (root namespace `Web.Api`) — there is **no** Domain/Application/Infrastructure/SharedKernel split.

- **`Features/{Entity}/`** — one folder per feature. Each **use case is one file**: `Features/{Entity}/{UseCase}.cs` is a `public static class {UseCase}` nesting the whole slice — `Command`/`Query`, `Validator` (commands only), an `internal sealed Handler`, and an `Endpoint : IEndpoint`. The entity, its `{Entity}Errors` catalog, and domain events live alongside the slices in the same feature folder.
- **`Common/`** — cross-cutting building blocks: `Result`/`Result<T>`, `Error`/`ErrorType`/`ValidationError`, `Entity`, `IDomainEvent`, `IDateTimeProvider` (`Web.Api.Common`); `ICommand`/`IQuery` + handler interfaces (`Common.Messaging`); validation + logging decorators (`Common.Behaviors`); `IEndpoint` + `Tags` + endpoint extensions (`Common.Endpoints`); `PagedList<T>`, `IdempotencyFilter`, security headers; and migration/observability/rate-limiting/result extensions (`Common.Extensions`).
- **`Database/`** — `ApplicationDbContext`, EF configurations in `Database/Configurations/`, migrations in `Database/Migrations/`, the domain-event dispatcher.
- **`Authentication/`** / **`Authorization/`** — JWT + refresh tokens, `IUserContext`, permission-based authorization.
- **`Notifications/`** — `IEmailSender` + `LoggingEmailSender`.
- **`BackgroundJobs/`** — hosted `BackgroundService` workers (add as needed; see `/add-background-job`).

## Conventions

- **One file per use case.** `Features/{Entity}/{UseCase}.cs`, namespace `Web.Api.Features.{Entity}`, a `public static class {UseCase}` with nested `Command`/`Query`, `Validator`, `internal sealed Handler`, and `Endpoint`. No separate `Endpoints/` folder.
- **CQRS, no MediatR** — `ICommand`/`ICommand<T>` + `ICommandHandler<...>`, `IQuery<T>` + `IQueryHandler<Q,R>` (`Web.Api.Common.Messaging`). Handlers are `internal sealed` with primary constructors, auto-registered by Scrutor scanning; wrapped by `ValidationDecorator` + `LoggingDecorator`. Validators come from `AddValidatorsFromAssembly(includeInternalTypes: true)`, endpoints from `AddEndpoints`. Never wire a slice up by hand.
- **No cross-feature coupling.** A slice may use the shared `Common`/`Database`/`Authentication`/`Authorization` code and another feature's **domain types** (entity, `{Entity}Errors`, domain events), but never another feature's `Command`/`Query`/`Handler`/`Validator`/`Endpoint`.
- **Errors, not exceptions** — return `Result.Failure(...)` with an `Error` for expected failures. Error codes are `"{FeaturePlural}.{Reason}"` from static `{Entity}Errors` factories. Endpoints translate with `result.Match(Results.Ok, CustomResults.Problem)`.
- **Endpoints** implement `IEndpoint` (nested in the slice), resolve the handler interface from DI, tag with `Tags`, and protect with `.RequireAuthorization()` / `.HasPermission("users:access")`; opt into dedupe with `.WithIdempotency()` (honors an `Idempotency-Key` header).
- **Validation** — FluentValidation `AbstractValidator<Command>` nested as `Validator`; runs in the validation decorator (commands only — queries have no validator).
- **Data — inject the concrete `ApplicationDbContext`** (from `Web.Api.Database`) directly into handlers. **There is no `IApplicationDbContext`.** Entities use FK ids (shadow relationships), no navigations; configurations in `Database/Configurations/` are auto-applied.
- **List queries** return `PagedList<T>` (search + sort + page); queries project to a nested `Response` DTO with `.Select(...)`, never returning entities.
- **Caching** — HybridCache with per-feature `{Feature}CacheKeys` helpers + invalidation in the commands that mutate the cached data.
- **Observability** — Serilog + OpenTelemetry export over OTLP to the Grafana/Loki/Tempo stack (`docker compose up -d`; Grafana at http://localhost:3000).

## Build & test

- `dotnet build VerticalSlice.slnx`
- `dotnet test VerticalSlice.slnx` — integration tests use Testcontainers, so Docker must be running.
- **Analyzer-strict**: `TreatWarningsAsErrors` + SonarAnalyzer. Keep code warning-clean; prefer collection expressions (`[.. x]`) and `ToUpperInvariant`.

## Skills (prefer these for common tasks)

`/add-feature` (use case) · `/add-entity` · `/add-tests` · `/add-migration` · `/add-background-job` · `/bootstrap` (rename + strip the Todos sample) · `/vsa-review` (convention review).
