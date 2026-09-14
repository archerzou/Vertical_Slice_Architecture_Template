using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Web.Api.Authentication;
using Web.Api.Common;
using Web.Api.Common.Endpoints;
using Web.Api.Common.Extensions;
using Web.Api.Common.Messaging;
using Web.Api.Database;
using Web.Api.Features.Users;

namespace Web.Api.Features.Todos;

public static class GetTodos
{
    public sealed record Query(
        Guid UserId,
        string? Search,
        string? SortColumn,
        string? SortOrder,
        int Page,
        int PageSize) : IQuery<PagedList<Response>>;

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

    internal sealed class Handler(ApplicationDbContext context, IUserContext userContext)
        : IQueryHandler<Query, PagedList<Response>>
    {
        public async Task<Result<PagedList<Response>>> Handle(Query query, CancellationToken cancellationToken)
        {
            if (query.UserId != userContext.UserId)
            {
                return Result.Failure<PagedList<Response>>(UserErrors.Unauthorized());
            }

            IQueryable<TodoItem> todosQuery = context.TodoItems
                .Where(todoItem => todoItem.UserId == query.UserId);

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                string search = query.Search;
                todosQuery = todosQuery.Where(todoItem => todoItem.Description.Contains(search));
            }

            todosQuery = string.Equals(query.SortOrder, "desc", StringComparison.OrdinalIgnoreCase)
                ? todosQuery.OrderByDescending(GetSortProperty(query.SortColumn))
                : todosQuery.OrderBy(GetSortProperty(query.SortColumn));

            IQueryable<Response> responsesQuery = todosQuery
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
                });

            PagedList<Response> todos = await PagedList<Response>.CreateAsync(
                responsesQuery,
                query.Page,
                query.PageSize,
                cancellationToken);

            return todos;
        }

        private static Expression<Func<TodoItem, object>> GetSortProperty(string? sortColumn) =>
            sortColumn?.ToUpperInvariant() switch
            {
                "DESCRIPTION" => todoItem => todoItem.Description,
                "DUEDATE" => todoItem => todoItem.DueDate!,
                "PRIORITY" => todoItem => todoItem.Priority,
                _ => todoItem => todoItem.CreatedAt
            };
    }

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet("todos", async (
                Guid userId,
                string? search,
                string? sortColumn,
                string? sortOrder,
                int? page,
                int? pageSize,
                IQueryHandler<Query, PagedList<Response>> handler,
                CancellationToken cancellationToken) =>
            {
                var query = new Query(
                    userId,
                    search,
                    sortColumn,
                    sortOrder,
                    page ?? 1,
                    pageSize ?? 10);

                Result<PagedList<Response>> result = await handler.Handle(query, cancellationToken);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithTags(Tags.Todos)
            .RequireAuthorization();
        }
    }
}
