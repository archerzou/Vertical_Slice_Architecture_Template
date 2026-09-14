using Web.Api.Common;

namespace Web.Api.Features.Todos;

public sealed record TodoItemCreatedDomainEvent(Guid TodoItemId) : IDomainEvent;
