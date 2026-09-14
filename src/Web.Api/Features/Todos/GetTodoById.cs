using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Web.Api.Authentication;
using Web.Api.Common;
using Web.Api.Common.Endpoints;
using Web.Api.Common.Extensions;
using Web.Api.Common.Messaging;
using Web.Api.Database;

namespace Web.Api.Features.Todos;

public static class GetTodoById
{
    public sealed record Query(Guid TodoItemId) : IQuery<Response>;

    public sealed class Response
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Description { get; set; }
        public DateTime? DueDate { get; set; }
        public List<string> Labels { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    internal sealed class Handler(
        ApplicationDbContext context,
        IUserContext userContext,
        HybridCache cache)
        : IQueryHandler<Query, Response>
    {
        public async Task<Result<Response>> Handle(Query query, CancellationToken cancellationToken)
        {
            Guid userId = userContext.UserId;

            Response? todo = await cache.GetOrCreateAsync(
                TodoCacheKeys.ById(userId, query.TodoItemId),
                async cancellation => await context.TodoItems
                    .Where(todoItem => todoItem.Id == query.TodoItemId && todoItem.UserId == userId)
                    .Select(todoItem => new Response
                    {
                        Id = todoItem.Id,
                        UserId = todoItem.UserId,
                        Description = todoItem.Description,
                        DueDate = todoItem.DueDate,
                        Labels = todoItem.Labels,
                        IsCompleted = todoItem.IsCompleted,
                        CreatedAt = todoItem.CreatedAt,
                        CompletedAt = todoItem.CompletedAt
                    })
                    .SingleOrDefaultAsync(cancellation),
                cancellationToken: cancellationToken);

            if (todo is null)
            {
                return Result.Failure<Response>(TodoItemErrors.NotFound(query.TodoItemId));
            }

            return todo;
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("todos/{id:guid}", async (
                Guid id,
                IQueryHandler<Query, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var command = new Query(id);

                Result<Response> result = await handler.Handle(command, cancellationToken);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithTags(Tags.Todos)
            .RequireAuthorization();
        }
    }
}
