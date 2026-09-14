---
name: bootstrap
description: Turn this template into a fresh project — rename the solution and (optionally) the root namespace to the user's app name, and remove the sample Todos feature so they start clean. Use right after cloning when the user wants to begin their own project from the template.
argument-hint: <new project/solution name, e.g. "Acme.Billing">
---

# Bootstrap a New Project from the Template

Two jobs: **(1)** rename the template to the user's name, and **(2)** strip the sample **Todos** feature (keep Users + auth — most apps need them). Confirm the new name with the user first.

## 1. Rename

The template ships as the `VerticalSlice` solution with a single `Web.Api` project (root namespace `Web.Api`) — there are no Domain/Application/Infrastructure/SharedKernel projects.

- Rename `VerticalSlice.slnx` → `{Name}.slnx`.
- If the user wants their own root namespace (e.g. `Acme.Billing` instead of `Web.Api`), rename the `Web.Api` namespace across `src/Web.Api` and the test projects — every `namespace Web.Api.*`, every `using Web.Api.*`, the `<RootNamespace>`/`InternalsVisibleTo` entries, and the `Program`/`Web.Api` partial-class hook used by the integration tests. If they're fine with the plain `Web.Api` namespace, skip this.
- Replace the template's hardcoded identifiers: the database name and `Jwt:Issuer`/`Audience` in `appsettings*.json`, the `POSTGRES_DB`/service names + `Host=` in `docker-compose.yml`, the `README.md` title, and any `UserSecretsId` if they want a fresh one.

## 2. Remove the Todos sample

Delete the Todos slice everywhere:

- `src/Web.Api/Features/Todos/` (the whole folder — entity, errors, domain events, cache keys, and every slice file).
- `src/Web.Api/Database/Configurations/TodoItemConfiguration.cs`.
- Remove the `TodoItems` `DbSet` from `ApplicationDbContext` (there is no `IApplicationDbContext` and no `TestDbContext` to update — the concrete context is the only declaration).
- `tests/Web.Api.UnitTests/Todos/` and `tests/IntegrationTests/Todos/`.
- Drop the table with a migration (`/add-migration Remove_Todos`), or — for a brand-new database — delete `src/Web.Api/Database/Migrations/`, `dotnet ef migrations add Initial --project src/Web.Api --startup-project src/Web.Api`, and let it recreate.

## Rules

- Keep the whole single-project structure and every cross-cutting concern (`Common`, `Database`, `Authentication`, `Authorization`, `Notifications`, endpoints, rate limiting, security headers, observability) plus `Features/Users/` and its auth slices — only the *sample feature* is removed.
- Build + `dotnet test` after each stage; the ArchitectureTests and integration tests surface any dangling reference.
- Leave the `.claude/skills/` pack in place so the conventions travel with the new project.
