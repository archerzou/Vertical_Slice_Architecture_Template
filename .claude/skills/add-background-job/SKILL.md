---
name: add-background-job
description: Add a background job (a hosted BackgroundService) to the Vertical Slice Architecture template that runs on a schedule and safely resolves scoped services. Use when the user wants a recurring task, worker, cleanup job, or scheduled processing.
argument-hint: <what the job does, e.g. "delete expired refresh tokens every hour">
---

# Add a Background Job

Recurring work runs as a `BackgroundService` in `src/Web.Api/BackgroundJobs/`. A hosted service is a **singleton**, so scoped services (`ApplicationDbContext`, handlers) must be resolved inside a scope created per iteration.

## Files to create/modify

1. **The job** — `src/Web.Api/BackgroundJobs/{Name}BackgroundService.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Web.Api.Database;

namespace Web.Api.BackgroundJobs;

internal sealed class ExpiredRefreshTokensCleanupService(
    IServiceScopeFactory scopeFactory,
    ILogger<ExpiredRefreshTokensCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                await context.RefreshTokens
                    .Where(token => token.ExpiresOnUtc < DateTime.UtcNow)
                    .ExecuteDeleteAsync(stoppingToken);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Background job {Job} failed", nameof(ExpiredRefreshTokensCleanupService));
            }
        }
    }
}
```

2. **Registration** — in `src/Web.Api/DependencyInjection.cs` (e.g. inside `AddServices`, or a small `AddBackgroundJobs` helper wired into `AddInfrastructure`):

```csharp
services.AddHostedService<ExpiredRefreshTokensCleanupService>();
```

## Rules

- A hosted service is a singleton — **never** inject `ApplicationDbContext`, handlers, or other scoped/transient services directly. Resolve them from a per-iteration scope via `IServiceScopeFactory`. There is **no `IApplicationDbContext`** — resolve the concrete `ApplicationDbContext` (from `Web.Api.Database`).
- Wrap each iteration body in try/catch so one failure doesn't stop the loop; log with `ILogger`.
- Use `PeriodicTimer` for the schedule and always honor `stoppingToken`.
- If the work is really a use case, prefer resolving and invoking an existing command/handler (`ICommandHandler<...>` from `Web.Api.Common.Messaging`) inside the scope instead of duplicating logic.
- Build + run the tests when done.
