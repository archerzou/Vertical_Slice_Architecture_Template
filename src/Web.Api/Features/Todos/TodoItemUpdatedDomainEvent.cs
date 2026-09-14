using Web.Api.Common;

namespace Web.Api.Features.Todos;

public sealed record TodoItemUpdatedDomainEvent(Guid TodoItemId) : IDomainEvent;
