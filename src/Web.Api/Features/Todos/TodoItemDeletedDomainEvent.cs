using Web.Api.Common;

namespace Web.Api.Features.Todos;

public sealed record TodoItemDeletedDomainEvent(Guid TodoItemId) : IDomainEvent;
