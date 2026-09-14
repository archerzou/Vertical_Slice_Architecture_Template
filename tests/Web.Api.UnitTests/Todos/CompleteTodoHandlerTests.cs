using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Web.Api.Authentication;
using Web.Api.Common;
using Web.Api.Database;
using Web.Api.Features.Todos;
using Web.Api.UnitTests.Abstractions;

namespace Web.Api.UnitTests.Todos;

public sealed class CompleteTodoHandlerTests : BaseHandlerTest
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task Handle_Should_ReturnNotFound_WhenTodoDoesNotExist()
    {
        // Arrange
        await using ApplicationDbContext context = CreateDbContext();
        HybridCache cache = CreateCache();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(UserId);
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();

        var handler = new CompleteTodo.Handler(context, dateTimeProvider, userContext, cache);
        var command = new CompleteTodo.Command(Guid.NewGuid());

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("TodoItems.NotFound");
    }

    [Fact]
    public async Task Handle_Should_ReturnAlreadyCompleted_WhenTodoIsCompleted()
    {
        // Arrange
        await using ApplicationDbContext context = CreateDbContext();
        Guid todoItemId = await SeedTodoAsync(context, isCompleted: true);

        HybridCache cache = CreateCache();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(UserId);
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();

        var handler = new CompleteTodo.Handler(context, dateTimeProvider, userContext, cache);
        var command = new CompleteTodo.Command(todoItemId);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("TodoItems.AlreadyCompleted");
    }

    [Fact]
    public async Task Handle_Should_CompleteTodoAndDispatchDomainEvent_WhenValid()
    {
        // Arrange
        IDomainEventsDispatcher dispatcher = Substitute.For<IDomainEventsDispatcher>();
        await using ApplicationDbContext context = CreateDbContext(dispatcher);
        Guid todoItemId = await SeedTodoAsync(context, isCompleted: false);

        HybridCache cache = CreateCache();
        IUserContext userContext = Substitute.For<IUserContext>();
        userContext.UserId.Returns(UserId);
        DateTime completedAt = DateTime.UtcNow;
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(completedAt);

        var handler = new CompleteTodo.Handler(context, dateTimeProvider, userContext, cache);
        var command = new CompleteTodo.Command(todoItemId);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        TodoItem todoItem = await context.TodoItems.SingleAsync(t => t.Id == todoItemId);
        todoItem.IsCompleted.ShouldBeTrue();
        todoItem.CompletedAt.ShouldBe(completedAt);
        await dispatcher.Received().DispatchAsync(
            Arg.Is<IEnumerable<IDomainEvent>>(events => events.Any(e => e is TodoItemCompletedDomainEvent)),
            Arg.Any<CancellationToken>());
    }

    private static async Task<Guid> SeedTodoAsync(ApplicationDbContext context, bool isCompleted)
    {
        var todoItem = new TodoItem
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Description = "Existing todo",
            Priority = Priority.Low,
            Labels = [],
            IsCompleted = isCompleted,
            CreatedAt = DateTime.UtcNow,
            CompletedAt = isCompleted ? DateTime.UtcNow : null
        };

        context.TodoItems.Add(todoItem);
        await context.SaveChangesAsync();

        return todoItem.Id;
    }
}
