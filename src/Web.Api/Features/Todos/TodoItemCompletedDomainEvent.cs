using Web.Api.Common;

namespace Web.Api.Features.Todos;

public sealed record TodoItemCompletedDomainEvent(Guid TodoItemId) : IDomainEvent;
