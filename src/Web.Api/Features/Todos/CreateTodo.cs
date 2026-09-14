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

public static class CreateTodo
{
    public sealed class Command : ICommand<Guid>
    {
        public Guid UserId { get; set; }
        public string Description { get; set; }
        public DateTime? DueDate { get; set; }
        public List<string> Labels { get; set; } = [];
        public Priority Priority { get; set; }
    }

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.UserId).NotEmpty();
            RuleFor(c => c.Priority).IsInEnum();
            RuleFor(c => c.Description).NotEmpty().MaximumLength(255);
            RuleFor(c => c.DueDate).GreaterThanOrEqualTo(DateTime.Today).When(x => x.DueDate.HasValue);
        }
    }

    internal sealed class Handler(
        ApplicationDbContext context,
        IDateTimeProvider dateTimeProvider,
        IUserContext userContext) : ICommandHandler<Command, Guid>
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

            var todoItem = new TodoItem
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Description = command.Description,
                Priority = command.Priority,
                DueDate = command.DueDate,
                Labels = command.Labels,
                IsCompleted = false,
                CreatedAt = dateTimeProvider.UtcNow
            };

            todoItem.Raise(new TodoItemCreatedDomainEvent(todoItem.Id));

            context.TodoItems.Add(todoItem);

            await context.SaveChangesAsync(cancellationToken);

            return todoItem.Id;
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public sealed class Request
        {
            public Guid UserId { get; set; }
            public string Description { get; set; }
            public DateTime? DueDate { get; set; }
            public List<string> Labels { get; set; } = [];
            public int Priority { get; set; }
        }

        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("todos", async (
                Request request,
                ICommandHandler<Command, Guid> handler,
                CancellationToken cancellationToken) =>
            {
                var command = new Command
                {
                    UserId = request.UserId,
                    Description = request.Description,
                    DueDate = request.DueDate,
                    Labels = request.Labels,
                    Priority = (Priority)request.Priority
                };

                Result<Guid> result = await handler.Handle(command, cancellationToken);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithTags(Tags.Todos)
            .WithIdempotency()
            .RequireAuthorization();
        }
    }
}
