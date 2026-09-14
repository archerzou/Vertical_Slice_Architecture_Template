using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Web.Api.Authentication;
using Web.Api.Common;
using Web.Api.Common.Endpoints;
using Web.Api.Common.Extensions;
using Web.Api.Common.Messaging;
using Web.Api.Database;
using Web.Api.Features.Users;

namespace Web.Api.Features.Todos;

public static class CopyTodo
{
    public sealed class Command : ICommand<Guid>
    {
        public Guid UserId { get; set; }
        public Guid TodoId { get; set; }
    }

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.UserId).NotEmpty();
            RuleFor(c => c.TodoId).NotEmpty();
        }
    }

    internal sealed class Handler(
        ApplicationDbContext context,
        IDateTimeProvider dateTimeProvider,
        IUserContext userContext)
        : ICommandHandler<Command, Guid>
    {
        public async Task<Result<Guid>> Handle(Command command, CancellationToken cancellationToken)
        {
            if (userContext.UserId != command.UserId)
            {
                return Result.Failure<Guid>(UserErrors.Unauthorized());
            }

            User? user = await context.Users.AsNoTracking()
                .SingleOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);

            if (user is null)
            {
                return Result.Failure<Guid>(UserErrors.NotFound(command.UserId));
            }

            TodoItem? existingTodo = await context.TodoItems.AsNoTracking()
                .SingleOrDefaultAsync(t => t.Id == command.TodoId && t.UserId == command.UserId, cancellationToken);

            if (existingTodo is null)
            {
                return Result.Failure<Guid>(TodoItemErrors.NotFound(command.TodoId));
            }

            var copiedTodoItem = new TodoItem
            {
                UserId = user.Id,
                Description = existingTodo.Description,
                Priority = existingTodo.Priority,
                DueDate = existingTodo.DueDate,
                Labels = [.. existingTodo.Labels], // Create a new list to avoid reference issues
                IsCompleted = false, // Reset completion status for the copy
                CreatedAt = dateTimeProvider.UtcNow
            };

            copiedTodoItem.Raise(new TodoItemCreatedDomainEvent(copiedTodoItem.Id));

            context.TodoItems.Add(copiedTodoItem);

            await context.SaveChangesAsync(cancellationToken);

            return copiedTodoItem.Id;
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public sealed class Request
        {
            public Guid UserId { get; set; }
            public Guid TodoId { get; set; }
        }

        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("todos/{todoId}/copy", async (
                Guid todoId,
                Request request,
                ICommandHandler<Command, Guid> handler,
                CancellationToken cancellationToken) =>
            {
                var command = new Command
                {
                    UserId = request.UserId,
                    TodoId = todoId
                };

                Result<Guid> result = await handler.Handle(command, cancellationToken);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithTags(Tags.Todos)
            .RequireAuthorization();
        }
    }
}
